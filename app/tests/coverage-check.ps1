param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$Doc = (Join-Path $PSScriptRoot '..\..\docs\v4\POWER_ACTIONS.md'),
    [string]$Tests = (Join-Path $PSScriptRoot 'action-tests.json'),
    [string]$Out = (Join-Path $PSScriptRoot '..\out\coverage')
)

$ErrorActionPreference = 'Stop'
$Exe = (Resolve-Path $Exe).Path
New-Item -ItemType Directory -Path $Out -Force | Out-Null
$catalogFile = Join-Path $Out 'catalog.json'
& (Join-Path $PSScriptRoot 'ci\invoke-exe.ps1') -Exe $Exe -Arguments @('--list-actions', '--json', '--quiet', '--out', $catalogFile) -Log (Join-Path $Out 'list.log') | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host 'could not list actions'; exit 1 }
Add-Type -AssemblyName System.Web.Extensions
$ser = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$ser.MaxJsonLength = [int]::MaxValue
$catalog = $ser.DeserializeObject([IO.File]::ReadAllText($catalogFile))
$known = @{}
foreach ($a in $catalog['actions']) { $known[$a['id']] = $a['id']; foreach ($al in $a['aliases']) { $known[$al] = $a['id'] } }

$text = [IO.File]::ReadAllText($Doc)
$tableIds = New-Object System.Collections.Generic.List[string]
$testIds = New-Object System.Collections.Generic.List[string]
foreach ($line in ($text -split "`n")) {
    if ($line -match '^\|\s*`([a-z0-9]+(?:\.[a-z0-9-]+)+)`\s*\|') {
        $tableIds.Add($Matches[1])
        if ($line -match '\[T\]') { $testIds.Add($Matches[1]) }
    }
}
$prose = [regex]::Matches($text, '`((?:clip|rename|new|organize|fix|links|disk|clean|system|open|dev|git|image|media|archive|search|inspect|compare|copy)\.[a-z0-9]+(?:[.-][a-z0-9-]+)*)`') | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique
$clipKeys = @('sort','unique','trim','remove-blank','json-pretty','json-minify','base64-encode','base64-decode','url-encode','url-decode','lower','upper','kebab','snake','camel','paths-fwd','quote-lines','to-ps-array','guid','timestamp','count')
foreach ($k in $clipKeys) { $testIds.Add('clip.' + $k) }
$testIds.Add('rename.lower'); $testIds.Add('rename.kebab'); $testIds.Add('rename.snake'); $testIds.Add('rename.title')

$missing = @(($tableIds + $prose) | Where-Object { $_ } | Select-Object -Unique | Where-Object { -not $known.ContainsKey($_) })
$tests = $ser.DeserializeObject([IO.File]::ReadAllText($Tests))
$tested = @{}
foreach ($t in $tests) { $tid = [string]$t['id']; if ($tid -and $known.ContainsKey($tid)) { $tested[$known[$tid]] = $true } }
$untested = @($testIds | Where-Object { $_ } | Select-Object -Unique | Where-Object { $known.ContainsKey($_) -and -not $tested.ContainsKey($known[$_]) })

Write-Host ('doc ids: {0} table + {1} in prose; catalog: {2} actions; tests: {3}' -f $tableIds.Count, @($prose).Count, $catalog['count'], @($tests).Count)
foreach ($m in $missing) { Write-Host ('MISSING action: ' + $m) }
foreach ($u in $untested) { Write-Host ('MISSING test:   ' + $u) }
Write-Host ('{0} ids without an action, {1} test-required ids without a test' -f $missing.Count, $untested.Count)
if ($missing.Count -gt 0 -or $untested.Count -gt 0) { exit 1 }
exit 0
