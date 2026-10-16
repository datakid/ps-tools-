param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\out\PSTools.exe')
)

$path = (Resolve-Path $Exe).Path
$json = & $path --dump-snippets | Out-String
if ([string]::IsNullOrWhiteSpace($json)) {
    Write-Host 'PSTools.exe --dump-snippets printed nothing.'
    exit 1
}

$map = $json | ConvertFrom-Json

$failed = 0
$checked = 0
foreach ($id in ($map.PSObject.Properties.Name | Sort-Object)) {
    $code = ([string]$map.$id).Replace('{path}', 'C:\Test Folder\it''''s')
    $tokens = $null
    $errors = $null
    [void][System.Management.Automation.Language.Parser]::ParseInput($code, [ref]$tokens, [ref]$errors)
    $checked++
    if ($errors.Count -gt 0) {
        $failed++
        Write-Host ("FAIL  {0}" -f $id)
        foreach ($e in $errors) { Write-Host ("      line {0}: {1}" -f $e.Extent.StartLineNumber, $e.Message) }
    }
}

Write-Host ''
if ($failed -gt 0) {
    Write-Host ("{0} of {1} snippets have parse errors." -f $failed, $checked)
    exit 1
}
Write-Host ("All {0} snippets parse with 0 errors." -f $checked)
exit 0
