param([ValidateSet('Simulation', 'AllModes')][string]$Scope = 'Simulation')
. (Join-Path $PSScriptRoot 'common.ps1')
$identity = Get-ApplicationIdentity
$private = Join-Path $ProjectRoot '.local\validation'
$acceptance = Get-Content -LiteralPath (Join-Path $private 'human-acceptance.json') -Raw | ConvertFrom-Json
if ($acceptance.status -ne 'PASS' -or $acceptance.version -ne $identity.version -or $acceptance.helperSha256 -ne $identity.helperSha256 -or -not $acceptance.settingsUi -or -not $acceptance.shortcut -or -not $acceptance.watcher -or -not $acceptance.simulation) { throw 'Matching Native/Human acceptance is incomplete.' }
if ($Scope -eq 'AllModes' -and ($acceptance.scope -ne 'AllModes' -or -not $acceptance.live)) { throw 'Live has not passed Native/Human validation.' }
$tests = Get-Content -LiteralPath (Join-Path $ProjectRoot 'artifacts\validation\test-summary.json') -Raw | ConvertFrom-Json
if ($tests.helperSha256 -ne $identity.helperSha256 -or $tests.result -ne 'PASS') { throw 'Headless verification does not match the accepted executable.' }
$triggers = @()
$modes = @()
foreach ($record in $acceptance.records) {
    if ($record.file -notmatch '^accepted-runs/run-\d{8}-\d{6}-\d{3}-[a-f0-9]{8}\.json$') { throw 'Invalid accepted-record path.' }
    $path = Join-Path $private $record.file
    if ((Get-FileHash -LiteralPath $path).Hash -ne $record.sha256) { throw 'An accepted record changed.' }
    $run = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $correct = ($run.RequestedMode -eq 'Simulation' -and $run.SimulationSelections -eq 1 -and $run.LiveSelections -eq 0) -or ($run.RequestedMode -eq 'Live' -and $run.LiveSelections -eq 1 -and $run.SimulationSelections -eq 0)
    if ($run.Candidate -ne $identity.version -or $run.HelperSha256 -ne $identity.helperSha256 -or $run.Outcome -ne 'Success' -or $run.LoginSubmissions -ne 1 -or $run.ActionCompletionUncertain -or -not $correct) { throw 'Accepted Native action does not match.' }
    if ($run.RequestedMode -eq 'Simulation') { $triggers += $run.Trigger }
    $modes += $run.RequestedMode
}
if ('Watcher' -notin $triggers -or 'ShortcutOrLaunchButton' -notin $triggers -or ($Scope -eq 'AllModes' -and 'Live' -notin $modes)) { throw 'Accepted mode/trigger coverage is incomplete.' }
[ordered]@{
    status = 'PASS'; scope = $Scope; version = $identity.version; helperSha256 = $identity.helperSha256
    verifiedAtUtc = [DateTime]::UtcNow.ToString('o'); rebuilt = $false; uiExecutedByAgent = $false
    liveStatus = $acceptance.liveStatus
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $private 'final-verification.json') -Encoding UTF8
Write-Output ('PASS: accepted executable and evidence unchanged; scope ' + $Scope + '. No rebuild or UI execution.')
