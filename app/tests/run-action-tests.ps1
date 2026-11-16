param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [Parameter(Mandatory = $true)][string]$Fixtures,
    [string]$Report = (Join-Path $PSScriptRoot '..\out\action-report.json'),
    [string]$Log = (Join-Path $PSScriptRoot '..\out\logs\action-tests.txt'),
    [string]$Goldens = (Join-Path $PSScriptRoot 'golden'),
    [switch]$UpdateGoldens
)

$ErrorActionPreference = 'Stop'
$Exe = (Resolve-Path $Exe).Path
$spec = Join-Path $PSScriptRoot 'action-tests.json'
$arguments = @('--test-actions', $Fixtures, '--spec', $spec, '--goldens', $Goldens, '--report', $Report)
if ($UpdateGoldens) { $arguments += '--update-goldens' }
$reportDir = Split-Path -Parent $Report
if ($reportDir -and -not (Test-Path -LiteralPath $reportDir)) { New-Item -ItemType Directory -Path $reportDir -Force | Out-Null }

& (Join-Path $PSScriptRoot 'ci\invoke-exe.ps1') -Exe $Exe -Arguments $arguments -Log $Log
exit $LASTEXITCODE
