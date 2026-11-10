param([ValidateSet('Release', 'Debug')][string]$Configuration = 'Release', [switch]$NoBuild)
. (Join-Path $PSScriptRoot 'common.ps1')
. (Join-Path $PSScriptRoot 'publication-hygiene.ps1')
if (-not $NoBuild) { & (Join-Path $ProjectRoot 'build.ps1') -Configuration $Configuration }
$identity = Get-ApplicationIdentity
$appOutput = Get-ProjectOutput 'NinjaTraderAutoLogin' $Configuration
$selectedExecutable = [IO.Path]::GetFullPath((Join-Path $appOutput 'NinjaTraderAutoLogin.exe'))
$identifiedExecutable = [IO.Path]::GetFullPath((Join-Path $ProjectRoot $identity.executable))
if ($identity.configuration -ne $Configuration) { throw 'Packaging configuration does not match the build identity.' }
if (-not [String]::Equals($selectedExecutable, $identifiedExecutable, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Packaging executable path does not match the build identity.'
}
if ((Get-FileHash -LiteralPath $selectedExecutable).Hash -ne $identity.helperSha256) {
    throw 'Packaging executable hash does not match the build identity.'
}
$packages = Join-Path $ProjectRoot 'artifacts\packages'
$archive = Join-Path $packages ('NinjaTraderAutoLogin-' + $identity.version + '-win-x64.zip')
if (Test-Path -LiteralPath $archive) { throw 'The versioned archive already exists; preserve it or select a new version.' }
$stage = Join-Path $ProjectRoot ('artifacts\staging\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
$applicationEntries = @('NinjaTraderAutoLogin.exe', 'NinjaTraderAutoLogin.exe.config', 'assets/launch.ico', 'assets/settings.ico', 'licenses/Vazir-OFL.txt')
foreach ($name in $applicationEntries) {
    $target = Join-Path $stage $name
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $appOutput $name) -Destination $target
}
$documentEntries = @('README.md', 'README.fa.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'SECURITY.md')
foreach ($name in $documentEntries) { Copy-Item -LiteralPath (Join-Path $ProjectRoot $name) -Destination $stage }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'setup-shortcuts.ps1') -Destination $stage
if ((Get-FileHash -LiteralPath (Join-Path $stage 'NinjaTraderAutoLogin.exe')).Hash -ne $identity.helperSha256) {
    throw 'Staged executable hash does not match the build identity.'
}
New-Item -ItemType Directory -Path $packages -Force | Out-Null
$temporaryArchive = $stage + '.zip'
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $temporaryArchive
Assert-PublicationArchive -ArchivePath $temporaryArchive -ExpectedEntries ($applicationEntries + $documentEntries + 'setup-shortcuts.ps1') -SourceRoot $ProjectRoot
Move-Item -LiteralPath $temporaryArchive -Destination $archive
(Get-FileHash -LiteralPath $archive).Hash + '  ' + [IO.Path]::GetFileName($archive) | Set-Content -LiteralPath ($archive + '.sha256') -Encoding ASCII
Write-Output $archive
