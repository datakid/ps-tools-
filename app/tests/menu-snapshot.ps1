param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$Out = (Join-Path $PSScriptRoot '..\out\menu-snapshot'),
    [switch]$Update
)

$ErrorActionPreference = 'Stop'
$Exe = (Resolve-Path $Exe).Path
$settings = Join-Path $PSScriptRoot 'menu-settings.txt'
$golden = Join-Path $PSScriptRoot 'golden'
$invoke = Join-Path $PSScriptRoot 'ci\invoke-exe.ps1'
New-Item -ItemType Directory -Path $Out -Force | Out-Null
$dataDir = Join-Path ([IO.Path]::GetTempPath()) ('pstools-menu-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $dataDir | Out-Null

$jobs = @(
    @{ Name = 'menu-dump.txt'; Args = @('--dump-menu', '--exe', '<EXE>') },
    @{ Name = 'tree-folder.txt'; Args = @('--dump-tree', '--ctx', 'folder', '--paths', 'C:\x\src', '--dirs', 'C:\x\src', '--tools', 'git,code') },
    @{ Name = 'tree-background.txt'; Args = @('--dump-tree', '--ctx', 'background', '--tools', 'git,code') },
    @{ Name = 'tree-file-image.txt'; Args = @('--dump-tree', '--ctx', 'file', '--paths', 'C:\x\a.png', '--tools', 'git,code') },
    @{ Name = 'tree-file-multi.txt'; Args = @('--dump-tree', '--ctx', 'file', '--paths', 'C:\x\a.txt|C:\x\b.txt', '--tools', 'git') },
    @{ Name = 'tree-drive.txt'; Args = @('--dump-tree', '--ctx', 'drive', '--paths', 'C:\', '--tools', 'git') },
    @{ Name = 'tree-folder-notools.txt'; Args = @('--dump-tree', '--ctx', 'folder', '--paths', 'C:\x\src', '--dirs', 'C:\x\src', '--tools', 'none') }
)

$failed = 0
foreach ($job in $jobs) {
    $actual = Join-Path $Out $job.Name
    $arguments = $job.Args + @('--settings', $settings, '--data-dir', $dataDir, '--quiet', '--out', $actual)
    & $invoke -Exe $Exe -Arguments $arguments -Log (Join-Path $Out ($job.Name + '.log')) | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Host ("FAIL {0}: exit code {1}" -f $job.Name, $LASTEXITCODE); $failed++; continue }
    $text = (([IO.File]::ReadAllText($actual)) -replace "`r`n", "`n").TrimEnd("`n") + "`n"
    $goldenPath = Join-Path $golden $job.Name
    if ($Update) {
        [IO.File]::WriteAllText($goldenPath, $text, (New-Object System.Text.UTF8Encoding($false)))
        Write-Host ("updated {0}" -f $job.Name)
        continue
    }
    if (-not (Test-Path -LiteralPath $goldenPath)) { Write-Host ("FAIL {0}: golden missing" -f $job.Name); $failed++; continue }
    $expected = (([IO.File]::ReadAllText($goldenPath)) -replace "`r`n", "`n").TrimEnd("`n") + "`n"
    if ($expected -ne $text) {
        Write-Host ("FAIL {0}: differs from golden" -f $job.Name)
        $a = $expected -split "`n"
        $b = $text -split "`n"
        $n = [Math]::Max($a.Count, $b.Count)
        $shown = 0
        for ($i = 0; $i -lt $n -and $shown -lt 12; $i++) {
            $x = if ($i -lt $a.Count) { $a[$i] } else { '<missing>' }
            $y = if ($i -lt $b.Count) { $b[$i] } else { '<missing>' }
            if ($x -ne $y) { Write-Host ("  line {0}: expected [{1}] got [{2}]" -f ($i + 1), $x, $y); $shown++ }
        }
        $failed++
    }
    else { Write-Host ("PASS {0}" -f $job.Name) }
}
Remove-Item -LiteralPath $dataDir -Recurse -Force -ErrorAction SilentlyContinue
if ($failed -gt 0) { exit 1 }
exit 0
