<#
.SYNOPSIS
  Backend for the MetaMove launcher. One action in, one JSON object out.

.DESCRIPTION
  status  - fast checks of every component (polled every 0.5-2 s). The robot's state comes
            from /robot/status over rosbridge. RWS only for one thing: before the headset
            is told to press Play, the program pointer must be verified to be on
            MetaMoveJointStream/MetaJointMain (at most every 3 s, one cookie jar kept
            across polls = one controller session; RWS allows 70 in total).
  start   - runs tools\metamove_up.ps1 in the background and returns at once; the status
            polls show the bring-up
  stop    - stops the ROS container, the EGM bridge (and its restart loop), the console,
            and the app on the headset
  quest   - opens the app on the headset (adb, USB or an adb-connected Quest)

  Needs an elevated launcher (metamove_up sets the Quest port forward).

.EXAMPLE
  powershell -NoProfile -File launcher.ps1 -Action status
#>
param(
    [ValidateSet('status', 'start', 'stop', 'quest')]
    [string]$Action = 'status'
)
$ErrorActionPreference = 'SilentlyContinue'
$ProgressPreference = 'SilentlyContinue'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$package = 'com.MetaMove.Lab'
$stateFile = Join-Path $env:TEMP 'metamove-launcher.json'
$compose = 'cd /mnt/c/git/MetaMove && FOXGLOVE_HOST_PORT=18765 docker compose -f ros2/docker/docker-compose.yml'

function Now { [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds() }
# The launcher polls this script every 0.5-2 s and calls can overlap, so the state file
# must survive a concurrent read/write: an empty or half-written file once turned the
# state into $null, which was then saved back empty for good (lost program-pointer
# cache, Quest IP, status cache -> "program pointer not readable").
function Get-State {
    if (Test-Path $stateFile) {
        try {
            $j = Get-Content $stateFile -Raw -ErrorAction Stop | ConvertFrom-Json
            if ($j -is [pscustomobject]) { return $j }
        } catch {}
    }
    [pscustomobject]@{ startedAt = $null; stoppingAt = $null; questAt = $null }
}
function Save-State($s) {
    if ($null -eq $s) { return }
    $tmp = "$stateFile.$PID.tmp"
    try {
        $s | ConvertTo-Json -Depth 5 | Set-Content $tmp -Encoding utf8
        Move-Item $tmp $stateFile -Force          # atomic replace on the same volume
    } catch { Remove-Item $tmp -ErrorAction SilentlyContinue }
}

function Test-Port([int]$port, [string]$hostName = '127.0.0.1', [int]$ms = 250) {
    $c = New-Object Net.Sockets.TcpClient
    try { return $c.ConnectAsync($hostName, $port).Wait($ms) -and $c.Connected } catch { return $false } finally { $c.Dispose() }
}

function Get-Adb {
    if ($script:s -and $script:s.adb -and (Test-Path $script:s.adb)) { return $script:s.adb }
    $a = Get-ChildItem 'C:\Program Files\Unity\Hub\Editor\*\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe' |
         Select-Object -Last 1
    if ($a) {
        # Remember it: the Program Files glob is the slowest part of a status poll.
        if ($script:s) { $script:s | Add-Member -NotePropertyName adb -NotePropertyValue $a.FullName -Force; Save-State $script:s }
        return $a.FullName
    }
    $c = Get-Command adb.exe; if ($c) { return $c.Source }
    $p = Join-Path $repo 'tools\platform-tools\adb.exe'; if (Test-Path $p) { return $p }
    return $null
}

# One message from a ROS topic via rosbridge (JSON string payload), or $null.
function Read-RosString([string]$topic, [int]$ms = 2500) {
    $ws = New-Object Net.WebSockets.ClientWebSocket
    $ct = (New-Object Threading.CancellationTokenSource $ms).Token
    try {
        $ws.ConnectAsync([Uri]'ws://127.0.0.1:9090', $ct).Wait()
        $sub = [Text.Encoding]::UTF8.GetBytes((@{ op = 'subscribe'; topic = $topic; type = 'std_msgs/String' } | ConvertTo-Json -Compress))
        $ws.SendAsync([ArraySegment[byte]]$sub, 'Text', $true, $ct).Wait()
        $buf = New-Object byte[] 8192
        $r = $ws.ReceiveAsync([ArraySegment[byte]]$buf, $ct); $r.Wait()
        $msg = [Text.Encoding]::UTF8.GetString($buf, 0, $r.Result.Count) | ConvertFrom-Json
        return ($msg.msg.data | ConvertFrom-Json)
    } catch { return $null } finally { try { $ws.Dispose() } catch {} }
}

function Get-Procs { Get-CimInstance Win32_Process -Filter "Name='python.exe' OR Name='powershell.exe'" }

$s = Get-State
$result = @{ ok = $true; demo = $false; action = $Action }

# (Re)start the app on the headset: push this PC's ROS address, force-stop, launch.
# Returns $null on success, else why not. Used by 'quest' (Restart app) and by 'start'.
function Start-QuestApp {
    $adb = Get-Adb
    $dev = if ($adb) { & $adb devices | Select-String "`tdevice$" } else { $null }
    if (-not $dev -and $adb -and $s.questIp) {
        # Wireless: works once the Quest had 'adb tcpip 5555' over USB since its last boot.
        & $adb connect "$($s.questIp):5555" | Out-Null
        $dev = & $adb devices | Select-String "`tdevice$"
    }
    if (-not $dev) {
        return $(if ($s.questIp) {
            "Quest at $($s.questIp) not reachable over wireless adb - plug it in once (USB) and run: adb tcpip 5555"
        } else { 'No headset on adb - plug the Quest in (USB), or open the app in the headset once' })
    } else {
        # Tell the app where ROS is: this PC's WLAN address (wireless demo), so a new DHCP
        # address only needs one press of this button. No WLAN -> USB via adb reverse.
        $wlan = Get-NetIPAddress -AddressFamily IPv4 -PrefixOrigin Dhcp -ErrorAction SilentlyContinue |
                Where-Object { $_.InterfaceAlias -match 'Wi-?Fi|WLAN' } | Select-Object -First 1
        $rosIp = if ($wlan) { "$($wlan.IPAddress):10000" } else { '127.0.0.1:10000' }
        & $adb reverse tcp:10000 tcp:10000 | Out-Null
        if ($wlan) {
            # Hosts without WSL mirrored networking reach ROS through a port forward; it is
            # bound to the address, so follow a changed one.
            $fwd = netsh interface portproxy show v4tov4 | Select-String '\s10000\s'
            if ($fwd -and -not ($fwd | Select-String ([regex]::Escape($wlan.IPAddress)))) {
                netsh interface portproxy add v4tov4 listenaddress=$($wlan.IPAddress) listenport=10000 `
                    connectaddress=127.0.0.1 connectport=10000 | Out-Null
            }
        }
        $tmp = Join-Path $env:TEMP 'metamove-ros_ip.txt'
        [IO.File]::WriteAllText($tmp, $rosIp)
        & $adb shell am force-stop $package | Out-Null
        & $adb push $tmp "/sdcard/Android/data/$package/files/ros_ip.txt" | Out-Null
        $result.rosIp = $rosIp
        & $adb shell monkey -p $package -c android.intent.category.LAUNCHER 1 | Out-Null
        $s.questAt = Now; Save-State $s
        return $null
    }
}

switch ($Action) {
    'start' {
        $up = Join-Path $repo 'tools\metamove_up.ps1'
        Start-Process powershell.exe -WindowStyle Hidden -ArgumentList @(
            '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$up`"", '-Hidden', '-NoConsole') | Out-Null
        # The headset app comes up with the stack (it retries until ROS answers). Not being
        # able to reach the Quest does not fail the start - it is reported and the
        # Restart app button does it later.
        $qe = Start-QuestApp
        if ($qe) { $result.questError = $qe }
        $s.startedAt = Now; $s.stoppingAt = $null; Save-State $s
    }
    'stop' {
        $s.stoppingAt = Now; $s.questAt = $null; Save-State $s
        # EGM bridge: its restart loop first (a PowerShell window), then the bridge itself.
        foreach ($p in Get-Procs) {
            if ($p.CommandLine -like '*egm_bridge_servo.py*' -or $p.CommandLine -like '*metamove-egm-bridge-loop*' -or
                $p.CommandLine -like '*robot_console.py*') {
                Stop-Process -Id $p.ProcessId -Force
            }
        }
        wsl -u root -e sh -c "$compose stop robot >/dev/null 2>&1" | Out-Null
        $adb = Get-Adb
        if ($adb) { & $adb shell am force-stop $package 2>$null | Out-Null }
    }
    'quest' {
        $err = Start-QuestApp
        if ($err) { $result.ok = $false; $result.error = $err }
    }
}

# --- status (kept under ~1 s: .NET calls instead of CIM / Get-Net* cmdlets) -----------------
$states = [ordered]@{}
$stopping = $s.stoppingAt -and ((Now) - $s.stoppingAt) -lt 8000
$starting = $s.startedAt -and ((Now) - $s.startedAt) -lt 180000
function During([bool]$up) { if ($up) { 'on' } elseif ($stopping) { 'stopping' } elseif ($starting) { 'starting' } else { 'off' } }

# Robot network: the address the controller sends EGM to, on a live link.
$nicState = 'fault'
foreach ($ni in [Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces()) {
    if ($ni.GetIPProperties().UnicastAddresses | Where-Object { $_.Address.ToString() -eq '192.168.125.100' }) {
        $nicState = if ($ni.OperationalStatus -eq 'Up') { 'on' } else { 'off' }
    }
}
$states.nic = $nicState

$rosUp = Test-Port 9090
# ROS-TCP: NEVER probe it by connecting. ros_tcp_endpoint sends everything ROS -> Unity to
# the NEWEST connection, so every probe steals the headset's return channel (twin, ghost,
# working area freeze while Quest -> ROS still works). Only look for a local listener.
$tcpUp = [bool]([Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners() |
    Where-Object { $_.Port -eq 10000 -and ([Net.IPAddress]::IsLoopback($_.Address) -or $_.Address.Equals([Net.IPAddress]::Any)) })
$status = if ($rosUp) { Read-RosString '/robot/status' } else { $null }
# A missed read (rosbridge slow to deliver the first message) must not flicker the UI:
# keep the last good status for 5 s.
if ($status) {
    $s | Add-Member -NotePropertyName lastStatus -NotePropertyValue $status -Force
    $s | Add-Member -NotePropertyName lastStatusAt -NotePropertyValue (Now) -Force
    Save-State $s
} elseif ($rosUp -and $s.lastStatus -and ((Now) - $s.lastStatusAt) -lt 5000) {
    $status = $s.lastStatus
}
$bridgeUp = $status -and $status.reason -ne 'EGM bridge not running'
$states.ros = During $rosUp
$states.tcp = During $tcpUp
$states.bridge = During $bridgeUp

# Program pointer over RWS - one session, the cookie jar survives between polls.
function Get-ProgramPointer {
    $now = Now
    if ($s.ppAt -and ($now - $s.ppAt) -lt 3000) { return $s.pp }
    $jar = Join-Path $env:TEMP 'metamove-launcher-rws.jar'
    $base = 'https://192.168.125.1'
    $auth = @('-sk', '--anyauth', '-u', 'Default User:robotics', '-c', $jar, '-b', $jar, '--max-time', '2',
              '-H', 'Accept: application/hal+json;v=2.0')
    function Get-Rws([string]$path) { try { return (& curl.exe @auth "$base$path" | ConvertFrom-Json).state[0] } catch { return $null } }
    $pcp = Get-Rws '/rw/rapid/tasks/T_ROB1/pcp'
    $pp = [ordered]@{ ok = $false; where = $null; set = $false }
    if ($pcp) {
        $pp.where = "$($pcp.modulemame)/$($pcp.routinename)"
        $pp.ok = $pcp.modulemame -eq 'MetaMoveJointStream' -and $pcp.routinename -eq 'MetaJointMain'
        if (-not $pp.ok) {
            # Allowed only in AUTO with RAPID stopped - exactly the moment before Play.
            $op = (Get-Rws '/rw/panel/opmode').opmode
            $ex = (Get-Rws '/rw/rapid/execution').ctrlexecstate
            if ($op -eq 'AUTO' -and $ex -eq 'stopped') {
                $post = $auth + @('-o', 'NUL', '-X', 'POST', '-H', 'Content-Type: application/x-www-form-urlencoded;v=2.0')
                & curl.exe @post "$base/rw/mastership/edit/request" | Out-Null
                try { & curl.exe @post -d 'routine=MetaJointMain&userlevel=FALSE' "$base/rw/rapid/tasks/T_ROB1/pcp/routine" | Out-Null }
                finally { & curl.exe @post "$base/rw/mastership/edit/release" | Out-Null }
                $pcp = Get-Rws '/rw/rapid/tasks/T_ROB1/pcp'
                if ($pcp) { $pp.where = "$($pcp.modulemame)/$($pcp.routinename)" }
                $pp.ok = $pcp -and $pcp.modulemame -eq 'MetaMoveJointStream' -and $pcp.routinename -eq 'MetaJointMain'
                $pp.set = $pp.ok
            }
        }
    }
    $s | Add-Member -NotePropertyName pp -NotePropertyValue ([pscustomobject]$pp) -Force
    $s | Add-Member -NotePropertyName ppAt -NotePropertyValue $now -Force
    Save-State $s
    return $s.pp
}

# The robot, as robot_status sees it: 'on' = ready or moving.
if ($status) {
    $result.robotReason = $status.reason
    $result.robotHint = $status.hint
    # About to ask for Play: only if the pointer is verified on MetaJointMain. Otherwise
    # the wrong program would start (e.g. MainModule/main after a mode switch).
    if ($status.reason -in 'RAPID stopped', 'no EGM packets from the controller') {
        $pp = Get-ProgramPointer
        $result.programPointer = $pp
        if (-not $pp.ok) {
            $result.robotReason = 'wrong program pointer'
            $result.robotHint = if ($pp.where) {
                "Program pointer is on $($pp.where) - do NOT press Play. Key switch to AUTO (RAPID stopped): the launcher sets it, or on the pendant: PP to routine > MetaJointMain"
            } else { 'Program pointer not readable over RWS - check it on the pendant before pressing Play' }
        } elseif ($pp.set) {
            $result.robotHint = 'Program pointer set to MetaJointMain over RWS - press Play'
        }
    }
    # Right after Start the bridge is simply not up yet (ROS comes first, 30-60 s) - that
    # is progress, not a fault. Red only if it is still missing once the start is over.
    if ($starting -and -not $stopping -and $status.reason -eq 'EGM bridge not running') {
        $result.robotReason = 'waiting for the EGM bridge'
        $result.robotHint = $null
    }
    $waiting = 'no EGM packets from the controller', 'RAPID stopped', 'motors off', 'wrong program pointer',
               'waiting for the EGM bridge'
    $states.robot = if ($result.robotReason -in $waiting) { 'starting' }
                    elseif ($status.level -eq 'error') { 'fault' } else { 'on' }
} else {
    $states.robot = During $false
}

# Headset app: any established connection to ROS-TCP :10000 is the app (Wi-Fi directly,
# USB through adb reverse). The headset itself: that, or a device on adb.
$rosConns = @([Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpConnections() |
    Where-Object { $_.State -eq 'Established' -and ($_.LocalEndPoint.Port -eq 10000 -or $_.RemoteEndPoint.Port -eq 10000) })
$appUp = $rosConns.Count -gt 0
# Remember where the headset is (the app's WLAN connection) - 'quest' reaches it over
# wireless adb with that, without anyone looking up the Quest's IP.
$questConn = $rosConns | Where-Object { $_.LocalEndPoint.Port -eq 10000 -and -not [Net.IPAddress]::IsLoopback($_.RemoteEndPoint.Address) } |
             Select-Object -First 1
if ($questConn -and $s.questIp -ne $questConn.RemoteEndPoint.Address.ToString()) {
    $s | Add-Member -NotePropertyName questIp -NotePropertyValue $questConn.RemoteEndPoint.Address.ToString() -Force
    Save-State $s
}
$onAdb = $false
if (-not $appUp) {
    $adb = Get-Adb
    $onAdb = $adb -and (& $adb devices | Select-String "`tdevice$")
    # Headset known from an earlier app connection: try wireless adb, at most every 20 s
    # (a failed connect costs a few seconds of this poll).
    if (-not $onAdb -and $adb -and $s.questIp -and (-not $s.adbTryAt -or ((Now) - $s.adbTryAt) -gt 20000)) {
        $s | Add-Member -NotePropertyName adbTryAt -NotePropertyValue (Now) -Force
        Save-State $s
        & $adb connect "$($s.questIp):5555" 2>$null | Out-Null
        $onAdb = [bool](& $adb devices | Select-String "`tdevice$")
    }
}
$states.quest = if ($appUp -or $onAdb) { 'on' } else { 'off' }
$states.app = if ($appUp) { 'on' } elseif ($s.questAt -and ((Now) - $s.questAt) -lt 15000) { 'starting' } else { 'off' }

# The colleagues' dashboard - observed, not controlled.
$states.dash = if (Test-Port 8080) { 'on' } else { 'off' }

if ($stopping -and -not $rosUp -and -not $bridgeUp) { $s.stoppingAt = $null; $s.startedAt = $null; Save-State $s }

$result.running = [bool]($rosUp -or $bridgeUp -or ($starting -and -not $stopping))
$result.components = @($states.GetEnumerator() | ForEach-Object { [ordered]@{ id = $_.Key; state = $_.Value } })
$result | ConvertTo-Json -Depth 4 -Compress
