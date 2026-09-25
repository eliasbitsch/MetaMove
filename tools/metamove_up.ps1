<#
.SYNOPSIS
  Brings MetaMove up on the real GoFa - one command, ends with a checklist.

.DESCRIPTION
  1. Checks the robot network (NIC 192.168.125.100, firewall for EGM UDP 6515).
  2. Reads the controller over RWS (one session, read-only): state, mode, program pointer.
  3. Starts the ROS side in WSL/Docker (compose service "robot" = metamove_real.launch.py).
  4. Starts the EGM bridge in its own window, restarted automatically if it exits.
  5. Opens the robot console, forwards the Quest over USB when one is connected.
  6. Prints a checklist and the robot's own status ("why is it not moving").

  Nothing here moves the robot: the path starts paused, and the bridge only passes
  commands while RAPID runs and a fresh path arrives.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\metamove_up.ps1
  powershell -ExecutionPolicy Bypass -File tools\metamove_up.ps1 -RosIp 10.0.0.5   # Quest over WLAN
#>
param(
    [string]$RobotIp = '192.168.125.1',
    [string]$PcIp = '192.168.125.100',
    [int]$EgmPort = 6515,
    [string]$RosIp = '',            # IP the Quest should use; empty = USB (adb reverse)
    [switch]$NoConsole,
    [switch]$NoBridge
)
$ErrorActionPreference = 'Continue'
$repo = Split-Path $PSScriptRoot -Parent
$results = [System.Collections.Generic.List[object]]::new()
function Check([string]$name, [bool]$ok, [string]$detail) {
    $results.Add([pscustomobject]@{ Ok = $ok; Name = $name; Detail = $detail })
    $mark = if ($ok) { '[ OK ]' } else { '[FAIL]' }
    $color = if ($ok) { 'Green' } else { 'Red' }
    Write-Host ("{0} {1,-22} {2}" -f $mark, $name, $detail) -ForegroundColor $color
}

Write-Host "`n== MetaMove up ==" -ForegroundColor Cyan

# --- 1. robot network ---------------------------------------------------------
$nic = Get-NetIPAddress -AddressFamily IPv4 -IPAddress $PcIp -ErrorAction SilentlyContinue
Check 'PC robot NIC' ($null -ne $nic) $(if ($nic) { "$PcIp on $($nic.InterfaceAlias)" } else {
    "missing - as admin: New-NetIPAddress -InterfaceAlias Ethernet -IPAddress $PcIp -PrefixLength 24" })
$fw = Get-NetFirewallRule -Direction Inbound -Enabled True -Action Allow -ErrorAction SilentlyContinue |
      Get-NetFirewallPortFilter -ErrorAction SilentlyContinue |
      Where-Object { $_.Protocol -eq 'UDP' -and @($_.LocalPort) -contains "$EgmPort" } | Select-Object -First 1
Check 'Firewall EGM UDP' ($null -ne $fw) $(if ($fw) { "$EgmPort inbound allowed" } else {
    "as admin: New-NetFirewallRule -DisplayName 'MetaMove EGM $EgmPort' -Direction Inbound -Protocol UDP -LocalPort $EgmPort -Action Allow" })

# --- 2. controller over RWS (one session) --------------------------------------
$ping = Test-Connection -ComputerName $RobotIp -Count 1 -Quiet -ErrorAction SilentlyContinue
Check 'Controller reachable' $ping $RobotIp
if ($ping) {
    $jar = Join-Path $env:TEMP 'metamove-rws.jar'
    Remove-Item $jar -ErrorAction SilentlyContinue
    function Rws([string]$path) {
        $out = & curl.exe -sk --anyauth -u 'Default User:robotics' -c $jar -b $jar --max-time 8 `
                -H 'Accept: application/hal+json;v=2.0' "https://$RobotIp$path"
        try { return ($out | ConvertFrom-Json) } catch { return $null }
    }
    $st = (Rws '/rw/panel/ctrl-state').state[0].ctrlstate
    $op = (Rws '/rw/panel/opmode').state[0].opmode
    $pp = (Rws '/rw/rapid/tasks/T_ROB1/pcp').state[0]
    Check 'Controller state' ($st -eq 'motoron') "$st / $op"
    $ppOk = $pp.routinename -eq 'MetaJointMain'
    Check 'Program pointer' $ppOk $(if ($ppOk) { 'MetaMoveJointStream/MetaJointMain' } else {
        "$($pp.modulemame)/$($pp.routinename) - pendant: PP to routine MetaJointMain (not 'PP to Main')" })
}

# --- 3. ROS side in WSL/Docker --------------------------------------------------
$compose = 'cd /mnt/c/git/MetaMove && FOXGLOVE_HOST_PORT=18765 docker compose -f ros2/docker/docker-compose.yml'
# Keep WSL alive: without an open session the VM may idle out and take Docker with it.
if (-not (Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like '*metamove-keepalive*' })) {
    Start-Process wsl.exe -ArgumentList '-e', 'sh', '-c', 'exec -a metamove-keepalive sleep infinity' -WindowStyle Hidden
}
wsl -e sh -c "$compose stop ros2 >/dev/null 2>&1; $compose up -d robot 2>&1" | Out-Null
$deadline = (Get-Date).AddSeconds(90)
do {
    Start-Sleep 3
    $rb = (Test-NetConnection 127.0.0.1 -Port 9090 -WarningAction SilentlyContinue).TcpTestSucceeded
    $tcp = (Test-NetConnection 127.0.0.1 -Port 10000 -WarningAction SilentlyContinue).TcpTestSucceeded
} until (($rb -and $tcp) -or (Get-Date) -gt $deadline)
Check 'ROS launch (robot)' ($rb -and $tcp) $(if ($rb -and $tcp) { 'rosbridge :9090, ROS-TCP :10000' } else {
    "not up - wsl -e docker logs metamove-ros2-robot-1" })

# --- 4. EGM bridge, supervised -----------------------------------------------------
if (-not $NoBridge) {
    $running = Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like '*egm_bridge_servo.py*' }
    if ($running) {
        Check 'EGM bridge' $true "already running (pid $($running.ProcessId -join ','))"
    } else {
        $loop = @"
`$host.UI.RawUI.WindowTitle = 'MetaMove EGM bridge'
while (`$true) {
  python -u '$repo\bridge\egm-bridge\egm_bridge_servo.py' --host $PcIp --port $EgmPort --rosbridge-host 127.0.0.1 -v
  if (`$LASTEXITCODE -eq 0) { break }
  Write-Host "bridge exited (`$LASTEXITCODE) - restarting in 2 s" -ForegroundColor Yellow
  Start-Sleep 2
}
"@
        Start-Process powershell.exe -ArgumentList '-NoExit', '-NoProfile', '-Command', $loop
        Start-Sleep 6
        $running = Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like '*egm_bridge_servo.py*' }
        Check 'EGM bridge' ($null -ne $running) 'own window, restarts itself'
    }
}

# --- 5. console + Quest ------------------------------------------------------------
if (-not $NoConsole -and -not (Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like '*robot_console.py*' })) {
    Start-Process python.exe -ArgumentList "`"$repo\tools\robot-console\robot_console.py`"" -WindowStyle Hidden
}
$adb = Get-ChildItem 'C:\Program Files\Unity\Hub\Editor\*\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe' -ErrorAction SilentlyContinue | Select-Object -Last 1
if ($adb) {
    $dev = (& $adb.FullName devices | Select-String "\tdevice$")
    if ($dev) {
        $target = if ($RosIp) { "${RosIp}:10000" } else { '127.0.0.1:10000' }
        if (-not $RosIp) { & $adb.FullName reverse tcp:10000 tcp:10000 | Out-Null }
        $tmp = Join-Path $env:TEMP 'ros_ip.txt'; [IO.File]::WriteAllText($tmp, "$target`n")
        & $adb.FullName push $tmp /sdcard/Android/data/com.MetaMove.Lab/files/ros_ip.txt 2>&1 | Out-Null
        Check 'Quest' $true "ROS endpoint $target $(if (-not $RosIp) { '(USB)' })"
    } else {
        Check 'Quest' $false 'no device on adb - plug in, or pass -RosIp for WLAN'
    }
}

# --- 6. the robot's own view ---------------------------------------------------------
Start-Sleep 3
$status = wsl -e sh -c "docker exec metamove-ros2-robot-1 bash -lc 'source /opt/ros/jazzy/setup.bash; timeout 5 ros2 topic echo --once --field data /robot/status 2>/dev/null'"
Write-Host ''
if ($status) {
    $s = ($status | Where-Object { $_ -match '^\{' } | Select-Object -First 1) | ConvertFrom-Json
    $c = if ($s.moving) { 'Green' } else { 'Yellow' }
    Write-Host ("Robot: {0} - {1}" -f $(if ($s.moving) { 'MOVING' } else { 'STOPPED' }), $s.reason) -ForegroundColor $c
    if ($s.hint) { Write-Host "  next: $($s.hint)" -ForegroundColor $c }
} else {
    Write-Host 'Robot: no /robot/status yet (ROS still starting?)' -ForegroundColor Yellow
}
$failed = @($results | Where-Object { -not $_.Ok }).Count
Write-Host ("`n{0} of {1} checks OK" -f ($results.Count - $failed), $results.Count) -ForegroundColor $(if ($failed) { 'Yellow' } else { 'Green' })
exit $failed
