. (Join-Path $PSScriptRoot 'common.ps1')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$suiteRoot = Join-Path $ProjectRoot ('artifacts\packaging-tests\' + [Guid]::NewGuid().ToString('N'))
$results = @()

function New-PackagingFixture([string]$Name) {
    $root = Join-Path $suiteRoot $Name
    foreach ($directory in @('scripts', 'artifacts\validation', 'artifacts\bin\NinjaTraderAutoLogin\Release\net48\assets', 'artifacts\bin\NinjaTraderAutoLogin\Release\net48\licenses')) {
        New-Item -ItemType Directory -Path (Join-Path $root $directory) -Force | Out-Null
    }
    foreach ($script in @('common.ps1', 'package.ps1', 'publication-hygiene.ps1', 'setup-shortcuts.ps1')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $script) -Destination (Join-Path $root 'scripts') }
    $relative = 'artifacts/bin/NinjaTraderAutoLogin/Release/net48/NinjaTraderAutoLogin.exe'
    $exe = Join-Path $root $relative
    [IO.File]::WriteAllBytes($exe, [Text.Encoding]::UTF8.GetBytes('PACKAGING-TEST-FIXTURE-NOT-AN-EXECUTABLE'))
    foreach ($file in @('NinjaTraderAutoLogin.exe.config', 'assets\launch.ico', 'assets\settings.ico', 'licenses\Vazir-OFL.txt')) {
        Set-Content -LiteralPath (Join-Path (Split-Path $exe -Parent) $file) -Value 'fixture' -Encoding UTF8
    }
    foreach ($file in @('README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'SECURITY.md')) {
        Set-Content -LiteralPath (Join-Path $root $file) -Value 'fixture' -Encoding UTF8
    }
    [ordered]@{ version = '0.0.0-fixture'; configuration = 'Release'; executable = $relative; helperSha256 = (Get-FileHash -LiteralPath $exe).Hash; sourceInputs = @() } |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $root 'artifacts\validation\build-identity.json') -Encoding UTF8
    return $root
}

function Assert-PackageRejected([string]$Root, [string]$Configuration, [string]$Expected) {
    $rejected = $false
    try { & (Join-Path $Root 'scripts\package.ps1') -Configuration $Configuration -NoBuild | Out-Null }
    catch {
        if ($_.Exception.Message -notlike ('*' + $Expected + '*')) { throw }
        $rejected = $true
    }
    if (-not $rejected) { throw ('Packaging accepted an invalid fixture: ' + $Expected) }
}

$root = New-PackagingFixture 'configuration'
Assert-PackageRejected $root 'Debug' 'configuration does not match'
if (Test-Path -LiteralPath (Join-Path $root 'artifacts\staging')) { throw 'Invalid configuration created a staging directory.' }
$results += 'Mismatched configuration rejected before staging'

$root = New-PackagingFixture 'path'
$identityFile = Join-Path $root 'artifacts\validation\build-identity.json'
$identity = Get-Content -LiteralPath $identityFile -Raw | ConvertFrom-Json
$copy = Join-Path $root 'artifacts\validated-copy.exe'
Copy-Item -LiteralPath (Join-Path $root $identity.executable) -Destination $copy
$identity.executable = 'artifacts/validated-copy.exe'
$identity | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $identityFile -Encoding UTF8
Assert-PackageRejected $root 'Release' 'path does not match'
$results += 'Different selected executable path rejected even with equal file bytes'

$root = New-PackagingFixture 'hash'
Add-Content -LiteralPath (Join-Path $root 'artifacts\bin\NinjaTraderAutoLogin\Release\net48\NinjaTraderAutoLogin.exe') -Value 'changed'
Assert-PackageRejected $root 'Release' 'identity no longer matches'
$results += 'Changed executable rejected'

$root = New-PackagingFixture 'private-content'
[IO.File]::WriteAllText((Join-Path $root 'README.md'), 'Captured checkout: ' + $root)
Assert-PackageRejected $root 'Release' 'Host-specific content'
if (Test-Path -LiteralPath (Join-Path $root 'artifacts\packages\NinjaTraderAutoLogin-0.0.0-fixture-win-x64.zip')) { throw 'Rejected archive reached the release directory.' }
$results += 'Private source paths rejected before a release archive is promoted'

$root = New-PackagingFixture 'binary-private-content'
$binary = Join-Path $root 'artifacts\bin\NinjaTraderAutoLogin\Release\net48\NinjaTraderAutoLogin.exe'
[IO.File]::WriteAllBytes($binary, [Text.Encoding]::Unicode.GetBytes('Captured profile: ' + [Environment]::GetFolderPath('UserProfile')))
$identityFile = Join-Path $root 'artifacts\validation\build-identity.json'
$identity = Get-Content -LiteralPath $identityFile -Raw | ConvertFrom-Json
$identity.helperSha256 = (Get-FileHash -LiteralPath $binary).Hash
$identity | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $identityFile -Encoding UTF8
Assert-PackageRejected $root 'Release' 'Host-specific content'
$results += 'UTF-16 private paths in executable bytes rejected'

$root = New-PackagingFixture 'valid'
& (Join-Path $root 'scripts\package.ps1') -NoBuild | Out-Null
$archive = Join-Path $root 'artifacts\packages\NinjaTraderAutoLogin-0.0.0-fixture-win-x64.zip'
$expected = Get-Content -LiteralPath (Join-Path $root 'artifacts\validation\build-identity.json') -Raw | ConvertFrom-Json
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    $stream = $zip.GetEntry('NinjaTraderAutoLogin.exe').Open()
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
    finally { $sha.Dispose(); $stream.Dispose() }
    if ($hash -ne $expected.helperSha256) { throw 'Archive contains an unexpected executable.' }
}
finally { $zip.Dispose() }
$archiveHash = (Get-FileHash -LiteralPath $archive).Hash
if ((Get-Content -LiteralPath ($archive + '.sha256') -Raw).Trim() -ne ($archiveHash + '  ' + [IO.Path]::GetFileName($archive))) { throw 'Archive checksum mismatch.' }
$results += 'Matching build packages exact executable bytes and a valid archive checksum'
Assert-PackageRejected $root 'Release' 'archive already exists'
if ((Get-FileHash -LiteralPath $archive).Hash -ne $archiveHash) { throw 'Existing archive changed.' }
$results += 'Existing versioned archive preserved'

. (Join-Path $PSScriptRoot 'publication-hygiene.ps1')
$unexpected = Join-Path $suiteRoot 'unexpected.zip'
$extra = Join-Path $suiteRoot 'settings.json'
Set-Content -LiteralPath $extra -Value '{}' -Encoding UTF8
Compress-Archive -LiteralPath $extra -DestinationPath $unexpected
$rejected = $false
try { Assert-PublicationArchive -ArchivePath $unexpected -ExpectedEntries @('README.md') -SourceRoot $suiteRoot }
catch { if ($_.Exception.Message -notlike '*unexpected or missing*') { throw }; $rejected = $true }
if (-not $rejected) { throw 'An unexpected archive entry was accepted.' }
$results += 'Unexpected archive entries rejected'

[ordered]@{ result = 'PASS'; checks = $results; uiExecuted = $false; fixturesContainRealExecutables = $false } |
    ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $suiteRoot 'results.json') -Encoding UTF8
Write-Output ('PASS: ' + $results.Count + ' packaging checks; no application or credential access.')
