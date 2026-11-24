param([switch]$Once)
$ErrorActionPreference = 'Stop'
$dir = Join-Path $env:LOCALAPPDATA 'RAC\monitoring'
New-Item -ItemType Directory -Force $dir | Out-Null
do {
    $lines = @(& (Join-Path $PSScriptRoot 'Get-RacContainerMetrics.ps1') -Once)
    if ($lines.Count -eq 0) { throw 'No metrics collected' }
    $lines += 'rac_container_collection_timestamp_seconds ' + [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $tmp = Join-Path $dir 'rac.prom.tmp'
    $dest = Join-Path $dir 'rac.prom'
    [IO.File]::WriteAllText($tmp,(($lines -join "`n") + "`n"),(New-Object System.Text.UTF8Encoding($false)))
    if ([IO.File]::Exists($dest)) { [IO.File]::Replace($tmp,$dest,($dest + ".bak")) } else { [IO.File]::Move($tmp,$dest) }
    if (-not $Once) { Start-Sleep -Seconds 30 }
} while (-not $Once)
