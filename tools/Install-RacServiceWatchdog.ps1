# Registers a low-privilege watchdog for local API/Web availability only.
# Never changes PAPER risk, SQL data or Binance permissions.
[CmdletBinding()]
param([switch]$Remove)
$ErrorActionPreference = 'Stop'
$taskName = 'RAC-MVP1516-Watchdog'
if ($Remove) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction Stop
    Write-Output 'RAC watchdog task removed; services unaffected.'
    return
}
$watchdog = Join-Path $PSScriptRoot 'Watch-RacServices.ps1'
if (-not (Test-Path $watchdog)) { throw 'Watchdog script missing.' }
if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) {
    throw 'Task already exists; refusing to overwrite existing configuration.'
}
$exe = Join-Path $PSHOME 'powershell.exe'
$arguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + $watchdog + '"'
$action = New-ScheduledTaskAction -Execute $exe -Argument $arguments -WorkingDirectory $PSScriptRoot
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(2) -RepetitionInterval (New-TimeSpan -Minutes 2) -RepetitionDuration (New-TimeSpan -Days 365)
$principal = New-ScheduledTaskPrincipal -UserId ([System.Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Description 'Recover missing RAC local API/Web only; never release PAPER risk lock' | Out-Null
Write-Output "Scheduled task $taskName registered with current-user privileges."
