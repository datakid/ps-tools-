param(
    [string]$Out = (Join-Path $PSScriptRoot '..\out\commands-list.json')
)

$ErrorActionPreference = 'Stop'
$names = New-Object System.Collections.Generic.HashSet[string] ([StringComparer]::OrdinalIgnoreCase)
foreach ($c in (Get-Command -CommandType Cmdlet, Function -Module 'Microsoft.PowerShell.*' -ErrorAction SilentlyContinue)) {
    if ($c.Name -match '^[A-Za-z]+-[A-Za-z0-9]+$') { [void]$names.Add($c.Name) }
}
foreach ($m in 'NetTCPIP', 'NetAdapter', 'ScheduledTasks', 'Storage', 'SmbShare', 'DnsClient', 'NetSecurity', 'Defender', 'Appx', 'BitLocker', 'LocalAccounts', 'PSReadLine', 'Microsoft.WSMan.Management', 'CimCmdlets', 'PackageManagement', 'PowerShellGet') {
    foreach ($c in (Get-Command -Module $m -ErrorAction SilentlyContinue)) {
        if ($c.Name -match '^[A-Za-z]+-[A-Za-z0-9]+$') { [void]$names.Add($c.Name) }
    }
}
$sorted = @($names | Sort-Object)
$dir = Split-Path -Parent $Out
if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
$json = '[' + (($sorted | ForEach-Object { '"' + $_ + '"' }) -join ',') + ']'
[IO.File]::WriteAllText($Out, $json, (New-Object Text.UTF8Encoding($false)))
Write-Host ('{0} command names written to {1}' -f $sorted.Count, $Out)
