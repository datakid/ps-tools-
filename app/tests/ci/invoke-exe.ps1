param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string[]]$Arguments = @(),
    [string]$Log,
    [int]$TimeoutSec = 300
)

$ErrorActionPreference = 'Stop'
if (-not $Log) { $Log = [IO.Path]::GetTempFileName() }
$logDir = Split-Path -Parent $Log
if ($logDir -and -not (Test-Path -LiteralPath $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }
$errLog = $Log + '.err'

$quoted = foreach ($a in $Arguments) { if ($a -match '[\s"]' -and $a -notmatch '^".*"$') { '"' + ($a -replace '"', '\"') + '"' } else { $a } }
$p = Start-Process -FilePath $Exe -ArgumentList ($quoted -join ' ') -PassThru -NoNewWindow -RedirectStandardOutput $Log -RedirectStandardError $errLog
if (-not $p.WaitForExit($TimeoutSec * 1000)) {
    try { & taskkill.exe /T /F /PID $p.Id | Out-Null } catch { }
    Write-Host ("TIMEOUT after {0} s: {1}" -f $TimeoutSec, $Exe)
    exit 124
}
$p.WaitForExit()

if (Test-Path -LiteralPath $Log) { Get-Content -LiteralPath $Log -Encoding UTF8 | ForEach-Object { Write-Host $_ } }
if ((Test-Path -LiteralPath $errLog) -and (Get-Item -LiteralPath $errLog).Length -gt 0) {
    Get-Content -LiteralPath $errLog -Encoding UTF8 | ForEach-Object { Write-Host ('stderr: ' + $_) }
}
exit $p.ExitCode
