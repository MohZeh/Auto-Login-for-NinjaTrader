$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$ProjectRoot = Split-Path -Parent $PSScriptRoot

function Get-ProjectSdk {
    $localSdk = Join-Path $ProjectRoot '.local\tooling\dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $localSdk) { return $localSdk }
    $command = Get-Command dotnet -ErrorAction Stop
    return $command.Source
}

function Get-ProjectOutput([string]$Name, [string]$Configuration = 'Release') {
    return Join-Path $ProjectRoot ('artifacts\bin\' + $Name + '\' + $Configuration + '\net48')
}

function Get-ApplicationIdentity {
    $path = Join-Path $ProjectRoot 'artifacts\validation\build-identity.json'
    if (-not (Test-Path -LiteralPath $path)) { throw 'Run build.ps1 first.' }
    $identity = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $app = Join-Path $ProjectRoot $identity.executable
    if ((Get-FileHash -LiteralPath $app).Hash -ne $identity.helperSha256) { throw 'The build identity no longer matches the executable.' }
    foreach ($inputFile in $identity.sourceInputs) {
        if ((Get-FileHash -LiteralPath (Join-Path $ProjectRoot $inputFile.file)).Hash -ne $inputFile.sha256) {
            throw ('A build input changed; rebuild before validating: ' + $inputFile.file)
        }
    }
    return $identity
}

function Get-PublicProjectFiles {
    # Deleted historical index entries are excluded during the one-time cleanup.
    $paths = @(git -C $ProjectRoot ls-files --cached --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { throw 'Unable to read repository inventory.' }
    return @($paths | Sort-Object -Unique | Where-Object { Test-Path -LiteralPath (Join-Path $ProjectRoot $_) -PathType Leaf })
}

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
