. (Join-Path $PSScriptRoot 'common.ps1')
$identity = Get-ApplicationIdentity
$suiteRoot = Join-Path $ProjectRoot ('artifacts\portability-tests\' + [Guid]::NewGuid().ToString('N'))
$original = Join-Path $suiteRoot 'Original folder'
$relocated = Join-Path $suiteRoot "Moved folder, مسیر ' جدید"
$links = Join-Path $suiteRoot 'Shortcuts'
foreach ($directory in @($original, $links, (Join-Path $original 'assets'))) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
Copy-Item -LiteralPath (Join-Path $ProjectRoot $identity.executable) -Destination (Join-Path $original 'NinjaTraderAutoLogin.exe')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'setup-shortcuts.ps1') -Destination $original
Copy-Item -LiteralPath (Join-Path $ProjectRoot 'assets\settings.ico') -Destination (Join-Path $original 'assets')
$results = @()
$initialDirectory = Get-Location

function Assert-TestShortcut([string]$Name, [string]$Target, [string]$Argument, [string]$Icon) {
    $shortcut = [NinjaTraderAutoLogin.Shortcuts.ShortcutFile]::Read((Join-Path $links $Name))
    if (-not [String]::Equals($shortcut.TargetPath, $Target, [StringComparison]::OrdinalIgnoreCase) -or
        $shortcut.Arguments -ne $Argument -or -not [IO.File]::Exists($shortcut.TargetPath) -or
        -not [String]::Equals($shortcut.WorkingDirectory, (Split-Path -Parent $Target), [StringComparison]::OrdinalIgnoreCase) -or
        $shortcut.IconPath -notlike ('*' + $Icon + '*')) { throw 'Shortcut identity mismatch.' }
}

try {
    $workingDirectory = Get-Location
    try {
        Set-Location -LiteralPath $suiteRoot
        & (Join-Path $original 'setup-shortcuts.ps1') -ShortcutDirectory $links | Out-Null
    } finally { Set-Location -LiteralPath $workingDirectory.Path }
    $exe = Join-Path $original 'NinjaTraderAutoLogin.exe'
    Assert-TestShortcut 'Auto Login for NinjaTrader.lnk' $exe '--launch' 'NinjaTraderAutoLogin.exe'
    Assert-TestShortcut 'Auto Login for NinjaTrader Settings.lnk' $exe '--settings' 'settings.ico'
    $results += 'Standalone setup creates both owned shortcuts without a build tree or current-directory dependency'

    # Both absolute targets are checked before this directory rename; no external tree is moved.
    $boundary = [IO.Path]::GetFullPath($suiteRoot).TrimEnd('\') + '\'
    foreach ($target in @($original, $relocated)) {
        if (-not [IO.Path]::GetFullPath($target).StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase)) { throw 'Relocation fixture escaped its test directory.' }
    }
    Rename-Item -LiteralPath $original -NewName ([IO.Path]::GetFileName($relocated))
    & (Join-Path $relocated 'setup-shortcuts.ps1') -ShortcutDirectory $links | Out-Null
    $exe = Join-Path $relocated 'NinjaTraderAutoLogin.exe'
    Assert-TestShortcut 'Auto Login for NinjaTrader.lnk' $exe '--launch' 'NinjaTraderAutoLogin.exe'
    Assert-TestShortcut 'Auto Login for NinjaTrader Settings.lnk' $exe '--settings' 'settings.ico'
    $results += 'Owned shortcuts are repaired after moving to a path with spaces, Unicode, an apostrophe and a comma'

    $legacyPath = Join-Path $links 'Auto Login for NinjaTrader.lnk'
    $legacy = [NinjaTraderAutoLogin.Shortcuts.ShortcutFile]::Read($legacyPath)
    $legacy.Description = 'NinjaTrader Auto Login'
    [NinjaTraderAutoLogin.Shortcuts.ShortcutFile]::Write($legacyPath, $legacy)
    & (Join-Path $relocated 'setup-shortcuts.ps1') -ShortcutDirectory $links | Out-Null
    $results += 'Existing helper shortcuts using the previous description are recognized'

    $foreignPath = Join-Path $links 'Auto Login for NinjaTrader Settings.lnk'
    $foreign = [NinjaTraderAutoLogin.Shortcuts.ShortcutFile]::Read($foreignPath)
    $foreign.TargetPath = Join-Path $suiteRoot 'Other.exe'
    [NinjaTraderAutoLogin.Shortcuts.ShortcutFile]::Write($foreignPath, $foreign)
    $before = @{}
    foreach ($file in Get-ChildItem -LiteralPath $links -File) { $before[$file.Name] = (Get-FileHash -LiteralPath $file.FullName).Hash }
    foreach ($remove in @($false, $true)) {
        $rejected = $false
        try { & (Join-Path $relocated 'setup-shortcuts.ps1') -ShortcutDirectory $links -Remove:$remove | Out-Null }
        catch { if ($_.Exception.Message -notlike '*another application*') { throw }; $rejected = $true }
        if (-not $rejected) { throw 'An unrelated shortcut was accepted.' }
        if (@(Get-ChildItem -LiteralPath $links -File).Count -ne $before.Count) { throw 'A shortcut was removed despite failed preflight.' }
        foreach ($file in Get-ChildItem -LiteralPath $links -File) {
            if ($before[$file.Name] -ne (Get-FileHash -LiteralPath $file.FullName).Hash) { throw 'A shortcut changed despite failed preflight.' }
        }
    }
    $results += 'Unrelated shortcuts prevent both replacement and removal before any writes'

    $owned = [NinjaTraderAutoLogin.Shortcuts.ShortcutFile]::Read($foreignPath)
    $owned.TargetPath = $exe
    [NinjaTraderAutoLogin.Shortcuts.ShortcutFile]::Write($foreignPath, $owned)
    & (Join-Path $relocated 'setup-shortcuts.ps1') -ShortcutDirectory $links -Remove | Out-Null
    if (@(Get-ChildItem -LiteralPath $links -File).Count -ne 0) { throw 'Owned shortcuts were not removed.' }
    $results += 'Only the two recognized helper shortcuts are removed'
} finally { Set-Location -LiteralPath $initialDirectory.Path }

[ordered]@{ result = 'PASS'; checks = $results; helperSha256 = $identity.helperSha256; uiExecuted = $false; registryTouched = $false; credentialAccess = $false; desktopTouched = $false } |
    ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $suiteRoot 'results.json') -Encoding UTF8
Write-Output ('PASS: ' + $results.Count + ' portability checks; workspace fixtures only, no application or credential access.')
