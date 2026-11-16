param(
    [string]$File = (Join-Path $PSScriptRoot '..\actions.json')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Web.Extensions
$ser = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$ser.MaxJsonLength = [int]::MaxValue
$doc = $ser.DeserializeObject([IO.File]::ReadAllText($File, [Text.Encoding]::UTF8))
$failed = 0
$count = 0
foreach ($a in $doc['actions']) {
    $count++
    $cmd = [string]$a['command']
    if ($a['shell'] -eq 'builtin' -or -not $cmd) { continue }
    if ($a['shell'] -eq 'cmd') { continue }
    $prepared = [regex]::Replace($cmd, "'\{(?:paths?|dir|name|stem|ext|parent|clip|date|time|now|gitroot|input:[^}]+)\}'|\{(?:paths?|dir|name|stem|ext|parent|clip|date|time|now|gitroot|input:[^}]+)\}", "'C:\x'")
    $errors = $null
    [void][System.Management.Automation.Language.Parser]::ParseInput($prepared, [ref]$null, [ref]$errors)
    if ($errors -and $errors.Count -gt 0) {
        $failed++
        Write-Host ('PARSE ERROR in {0}: line {1}: {2}' -f $a['id'], $errors[0].Extent.StartLineNumber, $errors[0].Message)
    }
}
Write-Host ('{0} actions checked, {1} parse errors' -f $count, $failed)
if ($failed -gt 0) { exit 1 }
exit 0
