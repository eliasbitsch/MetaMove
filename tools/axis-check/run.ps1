# Unity <-> URDF axis check: MoveIt FK vs. the GoFa prefab the headset spawns.
#
# Needs: ROS container up with move_group running (metamove_sim_playback.launch.py),
#        Unity Editor closed (batchmode holds the project).
# Prints AXIS_OK / AXIS_MISMATCH; exit code 0 / 1.
param(
    [string]$Container = 'metamove-ros2-ros2-1',
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$project = Join-Path $here '..\..\unity-quest' | Resolve-Path
$tmp = Join-Path $env:TEMP 'metamove-axis-check'
New-Item -ItemType Directory -Force $tmp | Out-Null

# 1. Reference FK inside the container.
$wslSrc = (wsl -e wslpath -a ($here -replace '\\', '/')) + '/fk_ref.py'
wsl -e docker cp $wslSrc "${Container}:/tmp/fk_ref.py"
$json = wsl -e docker exec $Container bash -lc 'source /opt/ros/jazzy/setup.bash; source /opt/metamove_ws/install/setup.bash; python3 /tmp/fk_ref.py'
if ($LASTEXITCODE -ne 0) { Write-Error 'fk_ref.py failed - is move_group running?' }
[IO.File]::WriteAllText("$tmp\fk_ref.json", ($json -join "`n"))

# 2. Same configs through the Unity rig.
$configs = python -c "import json,sys; d=json.load(open(sys.argv[1])); print(''.join(k+' '+' '.join(map(str,v['q']))+'\n' for k,v in d.items()), end='')" "$tmp\fk_ref.json"
[IO.File]::WriteAllText("$tmp\configs.txt", ($configs -join "`n") + "`n")
& $UnityExe -batchmode -nographics -projectPath $project `
    -executeMethod MetaMove.EditorTools.AxisConventionCheck.Run `
    -axisIn "$tmp\configs.txt" -axisOut "$tmp\unity_fk.txt" -logFile "$tmp\unity.log" | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Error "Unity exited $LASTEXITCODE - see $tmp\unity.log (exit 1 with a lockfile error = Editor still open)" }

# 3. Compare.
python "$here\compare.py" "$tmp\fk_ref.json" "$tmp\unity_fk.txt"
exit $LASTEXITCODE
