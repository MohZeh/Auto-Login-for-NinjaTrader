. (Join-Path $PSScriptRoot 'common.ps1')
. (Join-Path $PSScriptRoot 'publication-hygiene.ps1')
$files = Get-PublicProjectFiles
$problems = @()
foreach ($file in $files) {
    if ($file -match '(^\.local/|^evidence/|^bin/|^obj/|^artifacts/|\.lnk$|\.bundle$|\.zip$|\.exe$|\.pdb$|^CONTINUATION\.md$)') { $problems += ('Local-only artifact: ' + $file) }
    try { Assert-PublicationContent -Bytes ([IO.File]::ReadAllBytes((Join-Path $ProjectRoot $file))) -Name $file -SourceRoot $ProjectRoot }
    catch { $problems += $_.Exception.Message }
}
foreach ($font in @(@{ name = 'Vazir-Regular.ttf'; key = 'regularSha256' }, @{ name = 'Vazir-Bold.ttf'; key = 'boldSha256' })) {
    $provenance = Get-Content -LiteralPath (Join-Path $ProjectRoot 'assets\fonts\source.json') -Raw | ConvertFrom-Json
    if ((Get-FileHash -LiteralPath (Join-Path $ProjectRoot ('assets\fonts\' + $font.name))).Hash -ne $provenance.($font.key)) { $problems += ('Font provenance mismatch: ' + $font.name) }
}
if ($problems.Count -gt 0) { throw ($problems -join [Environment]::NewLine) }
Write-Output ('PASS: ' + $files.Count + ' publication files checked; local artifacts excluded; font provenance intact. This is a limited scan, not a full secret audit.')
