# Recuperacao local do RAC em modo PAPER, sem autorizar negociacao.
# Execucao: powershell -ExecutionPolicy Bypass -File .\tools\Start-RacPaperLocal.ps1
[CmdletBinding()]
param([string]$Release = 'mvp-1.5.15')
$ErrorActionPreference = 'Stop'
$stage = Join-Path $env:LOCALAPPDATA ('RAC\staging\' + $Release)
$api = Join-Path $stage 'api'
$web = Join-Path $stage 'web'
if (!(Test-Path (Join-Path $api 'RAC.Api.dll')) -or !(Test-Path (Join-Path $web 'RAC.Web.dll'))) {
    throw "Publicacao nao encontrada em $stage"
}
$config = Get-Content -Raw (Join-Path $api 'appsettings.json')
foreach ($flag in @('RealOrdersEnabled', 'TestnetOrdersEnabled')) {
    if ($config -notmatch ('"' + $flag + '"\s*:\s*false\b')) {
        throw "Inicio bloqueado: $flag nao esta explicitamente false."
    }
}
$logs = Join-Path $env:LOCALAPPDATA 'RAC\logs'
New-Item -ItemType Directory -Force -Path $logs | Out-Null
$env:RAC_SQLSERVER_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Database=RAC;Integrated Security=True;TrustServerCertificate=True;'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
foreach ($service in @(
    @{Name='API'; Port=5080; Folder=$api; Assembly='RAC.Api.dll'},
    @{Name='WEB'; Port=5081; Folder=$web; Assembly='RAC.Web.dll'}
)) {
    $listeners = @(Get-NetTCPConnection -LocalPort $service.Port -State Listen -ErrorAction SilentlyContinue)
    if ($listeners.Count -gt 0) {
        Write-Output "$($service.Name): porta $($service.Port) ja ocupada; nenhuma instancia duplicada criada."
        continue
    }
    $env:ASPNETCORE_URLS = "http://127.0.0.1:$($service.Port)"
    $p = Start-Process -FilePath 'dotnet' -ArgumentList $service.Assembly -WorkingDirectory $service.Folder -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $logs "$Release-$($service.Name)-$stamp.out.log") `
        -RedirectStandardError (Join-Path $logs "$Release-$($service.Name)-$stamp.err.log")
    Write-Output "$($service.Name): iniciado PID $($p.Id)."
}
Start-Sleep -Seconds 4
$health = Invoke-RestMethod -Uri 'http://127.0.0.1:5080/api/health' -TimeoutSec 8
if ($health.status -ne 'ok' -or $health.realOrdersEnabled -ne $false -or $health.testnetOrderEnabled -ne $false) {
    throw 'Health inesperado; verifique os processos. Nao autorize ordens.'
}
$proxy = Invoke-RestMethod -Uri 'http://127.0.0.1:5081/api/health' -TimeoutSec 8
if ($proxy.status -ne 'ok' -or $proxy.realOrdersEnabled -ne $false -or $proxy.testnetOrderEnabled -ne $false) {
    throw 'Proxy Web nao validado; nenhuma permissao de operacao foi alterada.'
}
$cn = New-Object System.Data.SqlClient.SqlConnection($env:RAC_SQLSERVER_CONNECTION)
try {
    $cn.Open()
    $cmd = $cn.CreateCommand()
    $cmd.CommandText = 'SELECT TOP 1 Running,RiskLocked,RiskReason FROM rac.PaperAccounts ORDER BY Id'
    $rd = $cmd.ExecuteReader()
    if ($rd.Read()) {
        Write-Output ("PAPER Running={0} RiskLocked={1} Reason={2}" -f $rd.GetValue(0), $rd.GetValue(1), $rd.GetValue(2))
    }
    $rd.Close()
}
finally { $cn.Close() }
Write-Output 'API e Web saudaveis. Apenas localhost. Nenhuma trava de risco modificada.'
