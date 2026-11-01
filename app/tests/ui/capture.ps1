param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [Parameter(Mandatory = $true)][string]$Out,
    [string]$Fixtures,
    [string]$Accent = '0067C0'
)

$ErrorActionPreference = 'Stop'
$Exe = (Resolve-Path $Exe).Path
if (Test-Path -LiteralPath $Out) { Remove-Item -LiteralPath $Out -Recurse -Force }
New-Item -ItemType Directory -Path $Out -Force | Out-Null
$invoke = Join-Path $PSScriptRoot '..\ci\invoke-exe.ps1'

$runs = @(
    @{ Theme = 'light'; Scale = '1'; Only = $null },
    @{ Theme = 'dark'; Scale = '1'; Only = $null },
    @{ Theme = 'light'; Scale = '1.5'; Only = 'main-actions,main-menu,dialog-info' },
    @{ Theme = 'dark'; Scale = '1.5'; Only = 'main-actions,main-menu,dialog-info' }
)

$failed = 0
foreach ($run in $runs) {
    $arguments = @('--screenshot', $Out, '--theme', $run.Theme, '--scale', $run.Scale, '--accent', $Accent)
    if ($Fixtures) { $arguments += @('--fixtures', $Fixtures) }
    if ($run.Only) { $arguments += @('--only', $run.Only) }
    Write-Host ("=== screenshots: theme={0} scale={1}" -f $run.Theme, $run.Scale)
    & $invoke -Exe $Exe -Arguments $arguments -Log (Join-Path $Out ("stdout.{0}.{1}.txt" -f $run.Theme, $run.Scale)) -TimeoutSec 240
    if ($LASTEXITCODE -ne 0) { $failed++; Write-Host ("harness exit code {0}" -f $LASTEXITCODE) }
}

Add-Type -AssemblyName System.Web.Extensions
$ser = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$ser.MaxJsonLength = [int]::MaxValue
$all = New-Object System.Collections.ArrayList
foreach ($file in (Get-ChildItem -LiteralPath $Out -Filter 'manifest.*.json' | Sort-Object Name)) {
    foreach ($entry in $ser.DeserializeObject([IO.File]::ReadAllText($file.FullName))) { [void]$all.Add($entry) }
}
[IO.File]::WriteAllText((Join-Path $Out 'manifest.json'), $ser.Serialize($all), (New-Object System.Text.UTF8Encoding($false)))
Write-Host ("manifest.json: {0} screenshots" -f $all.Count)

if ($failed -gt 0 -or $all.Count -eq 0) { exit 1 }
exit 0
