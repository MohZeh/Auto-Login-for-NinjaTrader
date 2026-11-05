param(
    [ValidateSet('Simulation', 'AllModes')][string]$Scope = 'Simulation',
    [switch]$AcceptHumanPass,
    [switch]$SettingsPassed,
    [switch]$ShortcutPassed,
    [switch]$WatcherPassed,
    [switch]$LivePassed
)
. (Join-Path $PSScriptRoot 'common.ps1')
$identity = Get-ApplicationIdentity
$runtime = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'NinjaTraderAutoLogin\evidence'
$runs = @()
$allowed = @('SchemaVersion', 'RunId', 'Candidate', 'HelperSha256', 'StartedUtc', 'CompletedUtc', 'Trigger', 'NinjaTraderPath', 'NinjaTraderVersion', 'ProcessStartedUtc', 'ProcessId', 'RequestedMode', 'Outcome', 'LoginSubmissions', 'SimulationSelections', 'LiveSelections', 'HumanAccepted', 'Events', 'ActionCompletionUncertain')
if (Test-Path -LiteralPath $runtime) {
    foreach ($file in Get-ChildItem -LiteralPath $runtime -Filter 'run-*.json' -File) {
        $value = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
        if ($value.Candidate -ne $identity.version -or $value.HelperSha256 -ne $identity.helperSha256) { continue }
        foreach ($key in $value.PSObject.Properties.Name) { if ($key -notin $allowed) { throw 'Unexpected run metadata property.' } }
        if ($value.SchemaVersion -ne 3) { throw 'Unexpected current run schema.' }
        $runs += [pscustomobject]@{ Path = $file.FullName; Data = $value }
    }
}
$successful = @($runs | Where-Object {
    $_.Data.Outcome -eq 'Success' -and $_.Data.LoginSubmissions -eq 1 -and $_.Data.ProcessId -gt 0 -and
    $_.Data.CompletedUtc -and -not $_.Data.ActionCompletionUncertain -and (
        ($_.Data.RequestedMode -eq 'Simulation' -and $_.Data.SimulationSelections -eq 1 -and $_.Data.LiveSelections -eq 0) -or
        ($_.Data.RequestedMode -eq 'Live' -and $_.Data.LiveSelections -eq 1 -and $_.Data.SimulationSelections -eq 0))
} | Sort-Object { $_.Data.StartedUtc } -Descending)
$simulation = @($successful | Where-Object { $_.Data.RequestedMode -eq 'Simulation' })
$shortcut = $simulation | Where-Object { $_.Data.Trigger -eq 'ShortcutOrLaunchButton' } | Select-Object -First 1
$watcher = $simulation | Where-Object { $_.Data.Trigger -eq 'Watcher' } | Select-Object -First 1
$live = $successful | Where-Object { $_.Data.RequestedMode -eq 'Live' } | Select-Object -First 1
$faults = @()
if (Test-Path -LiteralPath $runtime) {
    $faults = @(Get-ChildItem -LiteralPath $runtime -Filter 'app-fault-*.json' -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json } | Where-Object { $_.Candidate -eq $identity.version })
}
$summary = [ordered]@{
    version = $identity.version; helperSha256 = $identity.helperSha256; scope = $Scope
    collectedAtUtc = [DateTime]::UtcNow.ToString('o')
    shortcutMachinePass = [bool]$shortcut; watcherMachinePass = [bool]$watcher; liveMachinePass = [bool]$live
    faultCount = $faults.Count; humanAcceptance = 'PENDING'
    runs = @($runs | ForEach-Object { [ordered]@{ runId = $_.Data.RunId; trigger = $_.Data.Trigger; mode = $_.Data.RequestedMode; outcome = $_.Data.Outcome; loginSubmissions = $_.Data.LoginSubmissions; simulationSelections = $_.Data.SimulationSelections; liveSelections = $_.Data.LiveSelections; actionCompletionUncertain = $_.Data.ActionCompletionUncertain } })
}
$private = Join-Path $ProjectRoot '.local\validation'
New-Item -ItemType Directory -Path $private -Force | Out-Null
$acceptancePath = Join-Path $private 'human-acceptance.json'
if (-not $AcceptHumanPass -and (Test-Path -LiteralPath $acceptancePath)) {
    $accepted = Get-Content -LiteralPath $acceptancePath -Raw | ConvertFrom-Json
    if ($accepted.status -eq 'PASS' -and $accepted.version -eq $identity.version -and $accepted.helperSha256 -eq $identity.helperSha256 -and ($accepted.scope -eq $Scope -or $accepted.scope -eq 'AllModes')) {
        $summary.humanAcceptance = if ($faults.Count -eq 0) { 'PASS' } else { 'RECORDED_PASS_NEW_FAULTS' }
    }
}
if ($AcceptHumanPass) {
    if (-not $SettingsPassed -or -not $ShortcutPassed -or -not $WatcherPassed -or -not $shortcut -or -not $watcher) { throw 'Human acceptance and matching fresh Simulation records for both triggers are required.' }
    if ($Scope -eq 'AllModes' -and (-not $LivePassed -or -not $live)) { throw 'Live Native and Human acceptance is required for AllModes.' }
    if ($faults.Count -gt 0) { throw 'Current application faults require review.' }
    $selected = @($shortcut, $watcher)
    if ($Scope -eq 'AllModes') { $selected += $live }
    $copies = Join-Path $private 'accepted-runs'
    New-Item -ItemType Directory -Path $copies -Force | Out-Null
    $records = @($selected | ForEach-Object {
        $name = [IO.Path]::GetFileName($_.Path)
        Copy-Item -LiteralPath $_.Path -Destination (Join-Path $copies $name) -Force
        [ordered]@{ runId = $_.Data.RunId; file = 'accepted-runs/' + $name; sha256 = (Get-FileHash -LiteralPath $_.Path).Hash }
    })
    [ordered]@{
        status = 'PASS'; version = $identity.version; helperSha256 = $identity.helperSha256; scope = $Scope
        settingsUi = $true; shortcut = $true; watcher = $true; simulation = $true; live = ($Scope -eq 'AllModes')
        liveStatus = if ($Scope -eq 'AllModes') { 'PASS' } else { 'NOT_TESTED_UNAVAILABLE_ACCOUNT' }
        acceptedAtUtc = [DateTime]::UtcNow.ToString('o'); records = $records
        source = 'Direct Human acceptance plus exact-build Native records'
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $acceptancePath -Encoding UTF8
    $summary.humanAcceptance = 'PASS'
}
$summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $private 'native-summary.json') -Encoding UTF8
$summary | ConvertTo-Json -Depth 6
