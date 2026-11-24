param([switch]$Once)
$ErrorActionPreference = 'Stop'
$culture = [Globalization.CultureInfo]::InvariantCulture
do {
    $names = @(docker ps -a --filter label=com.docker.compose.project=rac-isolated --format '{{.Names}}')
    if ($LASTEXITCODE -ne 0 -or $names.Count -eq 0) { throw 'RAC containers unavailable' }
    $running = @(docker ps --filter label=com.docker.compose.project=rac-isolated --format '{{.Names}}')
    $lines = @()
    if ($running.Count -gt 0) {
        $stats = @(docker stats --no-stream --format '{{json .}}' $running)
        if ($LASTEXITCODE -ne 0) { throw 'Docker stats failed' }
        foreach ($line in $stats) {
            $s = $line | ConvertFrom-Json
            if ($s.Name -notmatch '^rac-isolated-[a-z0-9-]+$') { continue }
            $cpu = [double]::Parse($s.CPUPerc.TrimEnd('%'), $culture)
            $mem = [double]::Parse($s.MemPerc.TrimEnd('%'), $culture)
            $lines += 'rac_container_cpu_percent{container="' + $s.Name + '"} ' + $cpu.ToString($culture)
            $lines += 'rac_container_memory_percent{container="' + $s.Name + '"} ' + $mem.ToString($culture)
        }
    }
    foreach ($name in $names) {
        if ($name -notmatch '^rac-isolated-[a-z0-9-]+$') { continue }
        $count = docker inspect $name --format '{{.RestartCount}}'
        if ($LASTEXITCODE -ne 0) { throw 'Inspect failed' }
        $lines += 'rac_container_restart_count{container="' + $name + '"} ' + [int]$count
        $state = [int]($running -contains $name)
        $lines += 'rac_container_running{container="' + $name + '"} ' + $state
    }
    $lines | Write-Output
    if (-not $Once) { Start-Sleep -Seconds 30 }
} while (-not $Once)
