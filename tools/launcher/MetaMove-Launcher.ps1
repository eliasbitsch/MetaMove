<#
.SYNOPSIS
  Opens the MetaMove launcher window (Electron).

.DESCRIPTION
  Installs the Electron dependencies on first run, then starts the window without a
  console. -InstallShortcut puts a "MetaMove" icon on the desktop that runs this script.

  The launcher itself is still in demo mode: see scripts\launcher.ps1.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\launcher\MetaMove-Launcher.ps1
  powershell -ExecutionPolicy Bypass -File tools\launcher\MetaMove-Launcher.ps1 -InstallShortcut
#>
param([switch]$InstallShortcut)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot

if ($InstallShortcut) {
    $lnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'MetaMove.lnk'
    $sh = (New-Object -ComObject WScript.Shell).CreateShortcut($lnk)
    $sh.TargetPath = "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
    $sh.Arguments = "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$here\MetaMove-Launcher.ps1`""
    $sh.WorkingDirectory = $here
    $sh.IconLocation = "$here\assets\metamove.ico"
    $sh.Description = 'MetaMove launcher - GoFa CRB 15000 + Quest 3'
    $sh.Save()
    Write-Host "Shortcut: $lnk"
    return
}

$electron = Join-Path $here 'node_modules\electron\dist\electron.exe'
if (-not (Test-Path $electron)) {
    Write-Host 'First run: installing Electron ...'
    Push-Location $here
    try { & npm.cmd install --no-audit --no-fund } finally { Pop-Location }
}

# VS Code terminals export this, and it turns electron.exe into plain Node.
Remove-Item Env:ELECTRON_RUN_AS_NODE -ErrorAction SilentlyContinue
Start-Process -FilePath $electron -ArgumentList "`"$here`"" -WorkingDirectory $here
