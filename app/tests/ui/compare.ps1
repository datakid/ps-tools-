param(
    [Parameter(Mandatory = $true)][string]$Shots,
    [Parameter(Mandatory = $true)][string]$Baseline,
    [Parameter(Mandatory = $true)][string]$Out,
    [switch]$LintWarnOnly,
    [switch]$DiffFails
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing, System.Windows.Forms, System.Web.Extensions
Add-Type -ReferencedAssemblies 'System.Drawing', 'System.Windows.Forms', 'System.Web.Extensions', 'System.Core' -Path (Join-Path $PSScriptRoot 'UiLint.cs')

if (Test-Path -LiteralPath $Out) { Remove-Item -LiteralPath $Out -Recurse -Force }
New-Item -ItemType Directory -Path $Out -Force | Out-Null
$baselineDir = if (Test-Path -LiteralPath $Baseline) { (Resolve-Path $Baseline).Path } else { Join-Path $Out 'no-baseline' }

$report = [UiLint]::Execute((Resolve-Path $Shots).Path, $baselineDir, $Out)
[IO.File]::WriteAllText((Join-Path $Out 'report.json'), [UiLint]::ToJson($report), (New-Object System.Text.UTF8Encoding($false)))

$md = New-Object System.Text.StringBuilder
[void]$md.AppendLine(('Images checked: {0}' -f $report.Images))
$byRule = $report.Issues | Group-Object Rule | Sort-Object Name
foreach ($g in $byRule) { [void]$md.AppendLine(('- {0}: {1}' -f $g.Name, $g.Count)) }
$statuses = $report.Diffs | Group-Object Status
foreach ($g in $statuses) { [void]$md.AppendLine(('- diff {0}: {1}' -f $g.Name, $g.Count)) }
[IO.File]::WriteAllText((Join-Path $Out 'report.md'), $md.ToString(), (New-Object System.Text.UTF8Encoding($false)))

$html = New-Object System.Text.StringBuilder
[void]$html.AppendLine('<!doctype html><meta charset="utf-8"><title>UI report</title><style>body{font:14px Segoe UI,sans-serif;margin:24px}img{max-width:48%;border:1px solid #ccc}td{vertical-align:top;padding:6px}</style><h1>UI report</h1><table>')
foreach ($d in $report.Diffs) {
    [void]$html.AppendLine(('<tr><td colspan="3"><b>{0}</b> {1} ({2:P2})</td></tr><tr><td><img src="../{0}"></td><td><img src="diff/{0}"></td></tr>' -f $d.Image, $d.Status, $d.Ratio))
}
[void]$html.AppendLine('</table><h2>Issues</h2><ul>')
foreach ($i in $report.Issues) { [void]$html.AppendLine(('<li>{0} [{1}] {2}</li>' -f [Net.WebUtility]::HtmlEncode($i.Image), $i.Rule, [Net.WebUtility]::HtmlEncode($i.Message))) }
[void]$html.AppendLine('</ul>')
[IO.File]::WriteAllText((Join-Path $Out 'report.html'), $html.ToString(), (New-Object System.Text.UTF8Encoding($false)))

Write-Host ('Images checked: {0}' -f $report.Images)
foreach ($i in $report.Issues) { Write-Host ('LINT {0,-15} {1}: {2}' -f $i.Rule, $i.Image, $i.Message) }
foreach ($d in $report.Diffs) { if ($d.Status -ne 'same') { Write-Host ('DIFF {0,-12} {1} ({2:P2})' -f $d.Status, $d.Image, $d.Ratio) } }
Write-Host ('{0} lint issues, {1} images differ from baseline, {2} new' -f $report.Issues.Count, @($report.Diffs | Where-Object { $_.Status -in 'changed', 'size-differs' }).Count, @($report.Diffs | Where-Object { $_.Status -eq 'new' }).Count)

$fail = $false
if ($report.Issues.Count -gt 0) {
    if ($LintWarnOnly) { Write-Host '::warning title=ui-lint::pixel/layout lint reported issues (warn-only for the unchanged v3 UI)' }
    else { $fail = $true }
}
$changed = @($report.Diffs | Where-Object { $_.Status -in 'changed', 'size-differs' }).Count
if ($changed -gt 0) {
    if ($DiffFails) { $fail = $true }
    else { Write-Host '::warning title=ui-diff::screenshots differ from the baseline (warn-only before Phase 6)' }
}
if ($fail) { exit 1 }
exit 0
