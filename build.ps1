param(
    [ValidateSet('Release', 'Debug')][string]$Configuration = 'Release',
    [switch]$NoRestore
)
. (Join-Path $PSScriptRoot 'scripts\common.ps1')
$sdk = Get-ProjectSdk
$solution = Join-Path $ProjectRoot 'NinjaTraderLogin.sln'
if (-not $NoRestore) {
    & $sdk restore $solution --locked-mode --packages (Join-Path $ProjectRoot '.local\nuget')
    if ($LASTEXITCODE -ne 0) { throw 'Package restore failed.' }
}
& $sdk build $solution --configuration $Configuration --no-restore --no-incremental -m:1 -nr:false --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$app = Join-Path (Get-ProjectOutput 'NinjaTraderAutoLogin' $Configuration) 'NinjaTraderAutoLogin.exe'
$validation = Join-Path $ProjectRoot 'artifacts\validation'
New-Item -ItemType Directory -Path $validation -Force | Out-Null
$sources = @(Get-PublicProjectFiles | Where-Object { $_ -match '(^src/|^tests/|^tools/|^scripts/|^assets/|\.props$|\.csproj$|\.sln$|^global\.json$|^NuGet\.Config$|^build\.ps1$)' })
$identity = [ordered]@{
    version = (Get-Item -LiteralPath $app).VersionInfo.ProductVersion
    executable = $app.Substring($ProjectRoot.Length + 1).Replace('\', '/')
    configuration = $Configuration
    helperSha256 = (Get-FileHash -LiteralPath $app).Hash
    sdk = (& $sdk --version)
    builtAtUtc = [DateTime]::UtcNow.ToString('o')
    sourceInputs = @($sources | ForEach-Object { [ordered]@{ file = $_; sha256 = (Get-FileHash -LiteralPath (Join-Path $ProjectRoot $_)).Hash } })
    nativeAcceptance = 'NOT_ESTABLISHED_BY_BUILD'
}
$identity | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $validation 'build-identity.json') -Encoding UTF8
Write-Output ('Build complete: ' + $identity.version + '. No application UI or Native login was executed.')
