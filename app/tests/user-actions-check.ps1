param(
    [string]$Examples = (Join-Path $PSScriptRoot '..\..\docs\examples\actions'),
    [string]$Exe = (Join-Path $PSScriptRoot '..\out\PSTools.exe'),
    [int]$Expected = 10,
    [string]$ExeRunner = ''
)

$ErrorActionPreference = 'Stop'
$modern = $PSVersionTable.PSVersion.Major -ge 6
if (-not $modern) {
    Add-Type -AssemblyName System.Web.Extensions
    $ser = New-Object System.Web.Script.Serialization.JavaScriptSerializer
    $ser.MaxJsonLength = [int]::MaxValue
}
$work = Join-Path ([IO.Path]::GetTempPath()) ('ua-check-' + [guid]::NewGuid().ToString('N'))
$dest = Join-Path $work 'actions'
New-Item -ItemType Directory -Path $dest -Force | Out-Null
Copy-Item -Path (Join-Path $Examples '*') -Destination $dest -Recurse
$out = Join-Path $work 'list.json'

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $(if ($ExeRunner) { $ExeRunner } else { $Exe })
$prefix = $(if ($ExeRunner) { '"' + $Exe + '" ' } else { '' })
$psi.Arguments = $prefix + '--list-actions --json --data-dir "' + $work + '" --out "' + $out + '"'
$psi.UseShellExecute = $false
$psi.CreateNoWindow = $true
$p = [System.Diagnostics.Process]::Start($psi)
if (-not $p.WaitForExit(60000)) { try { $p.Kill() } catch { } Write-Host 'PSTools.exe did not finish'; exit 1 }

$text = [IO.File]::ReadAllText($out, [Text.Encoding]::UTF8)
$doc = if ($modern) { ConvertFrom-Json -InputObject $text -AsHashtable } else { $ser.DeserializeObject($text) }
$mine = @($doc['actions'] | Where-Object { [string]$_['id'] -like 'user.*' })
$failed = 0
if ($mine.Count -ne $Expected) { Write-Host ("FAIL  expected {0} user actions, the app loaded {1}" -f $Expected, $mine.Count); $failed++ }
foreach ($pr in @($doc['problems'])) { if ([string]$pr -match '(?i)actions') { Write-Host ('FAIL  problem: ' + $pr); $failed++ } }
foreach ($a in $mine) {
    foreach ($f in 'title', 'menu', 'description') { if (-not [string]$a[$f]) { Write-Host ("FAIL  {0} has no {1}" -f $a['id'], $f); $failed++ } }
}

foreach ($file in (Get-ChildItem -Path $dest -Recurse -Filter *.ps1)) {
    $t = [IO.File]::ReadAllText($file.FullName)
    $names = 'paths?|dir|name|stem|ext|parent|clip|date|time|now|gitroot|input:[^{}]+'
    $prepared = [regex]::Replace($t, ("'\{(?:" + $names + ")\}'|\{(?:" + $names + ")\}"), "'C:\x'")
    $errors = $null
    [void][System.Management.Automation.Language.Parser]::ParseInput($prepared, [ref]$null, [ref]$errors)
    if ($errors -and $errors.Count -gt 0) { Write-Host ("FAIL  {0}: line {1}: {2}" -f $file.Name, $errors[0].Extent.StartLineNumber, $errors[0].Message); $failed++ }
}
Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
Write-Host ('{0} example actions loaded, {1} problems' -f $mine.Count, $failed)
if ($failed -gt 0) { exit 1 }
exit 0
