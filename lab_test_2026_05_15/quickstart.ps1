# Lab test quickstart — Windows PowerShell
# Use:  .\quickstart.ps1 -path rws    OR   -path egm
#
# Runs the appropriate setup sequence and the latency probe.
# Manual steps still required: load RAPID module, motors on, play (see README).

param(
    [Parameter(Mandatory=$true)][ValidateSet('rws','egm')][string]$path,
    [string]$RobotIp = '192.168.125.1',
    [int]$RwsPort = 443,
    [double]$Duration = 5.0,
    [double]$Magnitude = 0.02
)

$CN = (wsl docker ps --format '{{.Names}}' | Select-String 'metamove' | Select-Object -First 1).ToString().Trim()
if (-not $CN) {
    Write-Error "No metamove container running. Start docker compose first."
    exit 1
}
Write-Host "Container: $CN"

# Copy probe script into container
$probeWin = Join-Path $PSScriptRoot 'latency_probe.py'
Get-Content $probeWin -Raw | wsl docker exec -i $CN bash -c "cat > /tmp/latency_probe.py"
Write-Host "Probe script copied"

if ($path -eq 'rws') {
    Write-Host ""
    Write-Host "=== Path A: Python RWS ==="
    Write-Host "Verify GoFa has MetaMoveCorePers.mod loaded + Running before running probe."
    Write-Host ""
    Write-Host "Start ROS Servo (separate terminal):"
    Write-Host "  wsl docker exec -it $CN bash -lc 'source /opt/metamove_ws/install/setup.bash && ros2 launch abb_crb15000_moveit metamove_servo.launch.py'"
    Write-Host ""
    Write-Host "Start RWS Bridge (separate terminal):"
    Write-Host "  wsl docker exec -it $CN bash -lc 'source /opt/metamove_ws/install/setup.bash && ros2 launch metamove_bridge sim_servo.launch.py rws_ip:=$RobotIp rws_port:=$RwsPort'"
    Write-Host ""
    Write-Host "Then call start_servo:"
    Write-Host "  wsl docker exec -it $CN bash -lc 'source /opt/metamove_ws/install/setup.bash && ros2 service call /servo_node/start_servo std_srvs/srv/Trigger'"
    Write-Host ""
    Read-Host "Press Enter when servo + bridge are running and you are ready for the probe"
}
elseif ($path -eq 'egm') {
    Write-Host ""
    Write-Host "=== Path B: Unity EGM ==="
    Write-Host "Verify on Unity side: Scene_Robot is open, ROSConnection points to host.docker.internal:10000, Play pressed."
    Write-Host "Verify on GoFa: MetaMoveCore.mod (EGM version) loaded + Running, Pendant shows 'EGM Connected'."
    Write-Host ""
    Write-Host "Start ROS Servo (separate terminal):"
    Write-Host "  wsl docker exec -it $CN bash -lc 'source /opt/metamove_ws/install/setup.bash && ros2 launch abb_crb15000_moveit metamove_servo.launch.py'"
    Write-Host ""
    Write-Host "Then call start_servo:"
    Write-Host "  wsl docker exec -it $CN bash -lc 'source /opt/metamove_ws/install/setup.bash && ros2 service call /servo_node/start_servo std_srvs/srv/Trigger'"
    Write-Host ""
    Read-Host "Press Enter when ready for the probe"
}

Write-Host ""
Write-Host "Running latency probe ($Duration seconds, +Z $Magnitude m/s) ..."
wsl docker exec $CN bash -lc "source /opt/metamove_ws/install/setup.bash && python3 /tmp/latency_probe.py --path $path --duration $Duration --magnitude $Magnitude"

Write-Host ""
Write-Host "Done. Write results to results.md in this folder."
