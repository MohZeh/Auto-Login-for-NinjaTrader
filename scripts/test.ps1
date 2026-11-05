param([ValidateSet('Release', 'Debug')][string]$Configuration = 'Release', [switch]$NoBuild)
. (Join-Path $PSScriptRoot 'common.ps1')
if (-not $NoBuild) { & (Join-Path $ProjectRoot 'build.ps1') -Configuration $Configuration }
$identity = Get-ApplicationIdentity
$validation = Join-Path $ProjectRoot 'artifacts\validation'
$tests = Get-ProjectOutput 'NinjaTraderAutoLogin.Tests' $Configuration
foreach ($project in @('NinjaTraderAutoLogin.Tests', 'NinjaTraderAutoLogin.Tools')) {
    $copy = Join-Path (Get-ProjectOutput $project $Configuration) 'NinjaTraderAutoLogin.exe'
    if ((Get-FileHash -LiteralPath $copy).Hash -ne $identity.helperSha256) { throw 'A test/tool references a different application build.' }
}
$testTemp = Join-Path $ProjectRoot 'artifacts\test-temp'
New-Item -ItemType Directory -Path $testTemp -Force | Out-Null
$previousTemp = $env:TEMP
$previousTmp = $env:TMP
try {
    $env:TEMP = $testTemp
    $env:TMP = $testTemp
    & (Join-Path $tests 'NinjaTraderAutoLogin.Tests.exe') (Join-Path $validation 'headless-checks.json') (Join-Path $tests 'fixtures')
    if ($LASTEXITCODE -ne 0) { throw 'Headless checks failed.' }
    $fakeTarget = Join-Path (Get-ProjectOutput 'NinjaTraderAutoLogin.FakeTarget' $Configuration) 'NinjaTrader.exe'
    & (Join-Path $tests 'NinjaTraderAutoLogin.Tests.exe') --fake-target $fakeTarget $validation
    if ($LASTEXITCODE -ne 0) { throw 'Fake-target checks failed.' }
} finally { $env:TEMP = $previousTemp; $env:TMP = $previousTmp }
$tools = Join-Path (Get-ProjectOutput 'NinjaTraderAutoLogin.Tools' $Configuration) 'NinjaTraderAutoLogin.Tools.exe'
$fontOutput = & $tools font-probe
if ($LASTEXITCODE -ne 0) { throw 'Embedded-font probe failed.' }
$font = $fontOutput | ConvertFrom-Json
$font | Add-Member -NotePropertyName helperSha256 -NotePropertyValue $identity.helperSha256
$font | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $validation 'embedded-font-probe.json') -Encoding UTF8
& $tools layout-audit (Join-Path $validation 'layout-audit.json')
if ($LASTEXITCODE -ne 0) { throw 'Layout calculation failed.' }
$audit = Get-Content -LiteralPath (Join-Path $validation 'layout-audit.json') -Raw | ConvertFrom-Json
if ($audit.GeometryIssues -ne 0 -or $audit.CasesWithAnyTextWidthRisk -ne 0 -or $audit.ResizeGeometryIssues -ne 0 -or $audit.ResizeTextRisks -ne 0) { throw 'Layout audit contains failures.' }
$checks = Get-Content -LiteralPath (Join-Path $validation 'headless-checks.json') -Raw | ConvertFrom-Json
[ordered]@{
    version = $identity.version; helperSha256 = $identity.helperSha256
    checks = @($checks.Checks).Count; calculatedScreenCases = $audit.Cases; calculatedManualSizeCases = $audit.ResizeCases
    result = 'PASS'; fakeTargetExecuted = $true; realNinjaTraderExecuted = $false; nativeAcceptance = 'NOT_ESTABLISHED_BY_HEADLESS_TESTS'
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $validation 'test-summary.json') -Encoding UTF8
