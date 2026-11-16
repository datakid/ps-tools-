param(
    [Parameter(Mandatory = $true)][string]$Script,
    [hashtable]$Params = @{},
    [Parameter(Mandatory = $true)][string]$Log
)

$ErrorActionPreference = 'Continue'
$logDir = Split-Path -Parent $Log
if ($logDir -and -not (Test-Path -LiteralPath $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }

$code = 0
$lines = New-Object System.Collections.Generic.List[string]
try {
    $global:LASTEXITCODE = 0
    & $Script @Params *>&1 | ForEach-Object {
        $text = ($_ | Out-String).TrimEnd()
        $lines.Add($text)
        Write-Host $text
    }
    $code = $global:LASTEXITCODE
    if ($null -eq $code) { $code = 0 }
}
catch {
    $text = 'EXCEPTION: ' + ($_ | Out-String)
    $lines.Add($text)
    Write-Host $text
    $code = 1
}
$lines.Add('exit code ' + $code)
Write-Host ('exit code ' + $code)
[IO.File]::WriteAllText($Log, ($lines -join "`r`n"), (New-Object System.Text.UTF8Encoding($false)))
exit $code
