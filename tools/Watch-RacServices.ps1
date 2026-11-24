# Single-pass, fail-closed local service watchdog.
# Recommended by Task Scheduler every 2 minutes. Never unlocks or starts PAPER.
# -DryRun with alternate ports is safe for fault-injection tests.
[CmdletBinding()]
param(
    [ValidateRange(1,65535)][int]$ApiPort = 5080,
    [ValidateRange(1,65535)][int]$WebPort = 5081,
    [string]$Release = 'mvp-1.5.21-3c09b6c',
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'
$logDir = Join-Path $env:LOCALAPPDATA 'RAC\logs'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$logFile = Join-Path $logDir 'mvp1516-watchdog.log'
$mutex = [System.Threading.Mutex]::new($false, 'Local\RAC-MVP1516-WATCHDOG')
$ownsMutex = $false
try {
    $ownsMutex = $mutex.WaitOne(0)
    if (-not $ownsMutex) { Write-Output 'WATCHDOG_ALREADY_RUNNING'; return }
    $services = @(
        @{ Name='API'; Port=$ApiPort; Url="http://127.0.0.1:$ApiPort/api/health" },
        # Probe the static Web independently of the API. An API outage must
        # not make an otherwise healthy Web listener block safe API recovery.
        @{ Name='WEB'; Port=$WebPort; Url="http://127.0.0.1:$WebPort/" }
    )
    $missing = @()
    $occupiedUnhealthy = @()
    foreach ($service in $services) {
        $listeners = @(Get-NetTCPConnection -LocalPort $service.Port -State Listen -ErrorAction SilentlyContinue)
        $healthy = $false
        if ($listeners.Count -gt 0) {
            try {
                if ($service.Name -eq 'API') {
                    $h = Invoke-RestMethod -Uri $service.Url -TimeoutSec 5
                    $healthy = ($h.status -eq 'ok' -and $h.project -eq 'RAC' -and
                        $h.realOrdersEnabled -eq $false -and $h.testnetOrderEnabled -eq $false)
                } else {
                    $page = Invoke-WebRequest -UseBasicParsing -Uri $service.Url -TimeoutSec 5
                    $healthy = ($page.StatusCode -eq 200 -and
                        $page.Content.Contains('id="officePhotoreal"'))
                }
            } catch { $healthy = $false }
        }
        if ($healthy) { Write-Output ("HEALTHY " + $service.Name); continue }
        if ($listeners.Count -gt 0) {
            $occupiedUnhealthy += $service.Name
        } else { $missing += $service.Name }
    }
    if ($occupiedUnhealthy.Count -gt 0) {
        $message = 'FAIL_CLOSED_UNHEALTHY_LISTENER: ' + ($occupiedUnhealthy -join ',') + '; manual review required; no processes terminated.'
        Write-Output $message
        if (-not $DryRun) {
            Add-Content -Path $logFile -Value ("{0} {1}" -f (Get-Date).ToString('o'), $message)
            throw $message
        }
        return
    }
    if ($missing.Count -eq 0) { return }
    if ($DryRun) {
        Write-Output ('DRY_RUN_WOULD_RECOVER ' + ($missing -join ','))
        return
    }
    if ($ApiPort -ne 5080 -or $WebPort -ne 5081) {
        throw 'Alternate ports are permitted only with -DryRun.'
    }
    $startup = Join-Path $PSScriptRoot 'Start-RacPaperLocal.ps1'
    if (-not (Test-Path $startup)) { throw 'Recovery script missing' }
    $incident = 'RECOVER_MISSING ' + ($missing -join ',')
    Write-Output $incident
    Add-Content -Path $logFile -Value ("{0} {1}" -f (Get-Date).ToString('o'), $incident)
    try {
        & $startup -Release $Release
        # The start script checks both health endpoints and reads PAPER lock status.
        if (-not $?) { throw 'Recovery script failed' }
        Add-Content -Path $logFile -Value ("{0} RECOVERY_OK" -f (Get-Date).ToString('o'))
    }
    catch {
        # Never write exception messages to disk: they may include secrets.
        Add-Content -Path $logFile -Value ("{0} RECOVERY_FAILED {1}" -f (Get-Date).ToString('o'), $_.Exception.GetType().Name)
        throw
    }
}
finally {
    if ($ownsMutex) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
