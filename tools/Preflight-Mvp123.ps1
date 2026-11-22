param(
    [string]$Stage = (Join-Path $env:LOCALAPPDATA 'RAC\staging\mvp-1.2.3')
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
function SqlScalar([string]$query) {
    $result = & sqlcmd -S '(localdb)\MSSQLLocalDB' -E -d RAC -l 8 -b -W -h -1 -Q "SET NOCOUNT ON;$query" 2>&1
    if ($LASTEXITCODE -ne 0) { throw "SQL preflight failed: $result" }
    return (@($result | Where-Object { $_.ToString().Trim() -ne '' }) -join ' ').Trim()
}
$gitStatus = @(git -C $repo status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'Cannot read repository state.' }
$api = Join-Path $Stage 'api\RAC.Api.dll'
$web = Join-Path $Stage 'web\RAC.Web.dll'
if (!(Test-Path $api) -or !(Test-Path $web)) { throw 'Release artifacts missing.' }
$processes = @(Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
    Where-Object { $_.CommandLine -match 'RAC\.(Api|Web)\.dll --urls http://localhost:508[01]' })
if ($processes.Count -ne 2) { throw 'Unexpected RAC process inventory.' }
$health = Invoke-RestMethod 'http://localhost:5080/api/health' -TimeoutSec 7
if ($health.status -ne 'ok' -or $health.realOrdersEnabled -ne $false -or
    $health.testnetOrderEnabled -ne $false) { throw 'Unsafe API health.' }
$paper = SqlScalar "SELECT CONCAT(CAST(Running AS int),'|',CAST(RiskLocked AS int)) FROM rac.PaperAccounts WHERE Id=1"
$testnet = SqlScalar "SELECT CONCAT(CAST(Enabled AS int),'|',CAST(CircuitLocked AS int),'|',CAST(EmergencyStopped AS int)) FROM rac.TestnetExecutionStates WHERE Id=1"
$orders = SqlScalar 'SELECT COUNT(*) FROM rac.TestnetOrders'
if ($paper -ne '1|0' -or $testnet -ne '0|0|0' -or $orders -ne '0') {
    throw "Safety gate failed: PAPER=$paper TESTNET=$testnet ORDERS=$orders"
}
[pscustomobject]@{
    Preflight = 'PASS'; BranchHead = (git -C $repo rev-parse --short HEAD)
    Paper = $paper; Testnet = $testnet; TestnetOrders = $orders
    RunningRacProcesses = $processes.Count
    ApiSha256 = (Get-FileHash $api -Algorithm SHA256).Hash
    WebSha256 = (Get-FileHash $web -Algorithm SHA256).Hash
    Staging = $Stage; ChangedFiles = $gitStatus.Count
    DeploymentPerformed = $false
} | Format-List
# Read-only safety gate: does NOT deploy, restart, update SQL, or send orders.
