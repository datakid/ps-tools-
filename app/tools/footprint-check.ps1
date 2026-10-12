param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\out\PSTools.exe'),
    [switch]$IncludeTweaks
)

$ErrorActionPreference = 'Stop'
$Exe = (Resolve-Path $Exe).Path
$data = Join-Path $env:LOCALAPPDATA 'PSTools'
$menuKey = 'HKCU:\Software\Classes\Directory\Background\shell\PSTools'
$advKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'
$classicKey = 'HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}'

function Invoke-Tool([string[]]$Arguments) {
    $out = [IO.Path]::GetTempFileName()
    try {
        $p = Start-Process -FilePath $Exe -ArgumentList $Arguments -Wait -PassThru -NoNewWindow -RedirectStandardOutput $out
        [pscustomobject]@{ Code = $p.ExitCode; Text = (Get-Content -LiteralPath $out -Raw) }
    }
    finally { Remove-Item -LiteralPath $out -Force -ErrorAction SilentlyContinue }
}

function Get-Snapshot {
    $adv = Get-ItemProperty $advKey -ErrorAction SilentlyContinue
    $keys = @(Get-ChildItem 'HKCU:\Software\Classes' -Recurse -ErrorAction SilentlyContinue | ForEach-Object { $_.Name } | Sort-Object)
    $files = @()
    if (Test-Path $data) { $files = @(Get-ChildItem $data -Recurse -Force | ForEach-Object { $_.FullName } | Sort-Object) }
    [pscustomobject]@{
        Keys = $keys
        Files = $files
        Values = @("Hidden=$($adv.Hidden)", "HideFileExt=$($adv.HideFileExt)", "Classic=$(Test-Path $classicKey)")
    }
}

function Get-Difference($before, $after, $label) {
    Compare-Object -ReferenceObject (@('-') + @($before)) -DifferenceObject (@('-') + @($after)) |
        ForEach-Object { '{0}: {1} {2}' -f $label, $_.SideIndicator, $_.InputObject }
}

if ((Test-Path $menuKey) -or (Test-Path $data)) {
    Write-Host 'PS Tools is already installed or has data on this account. Run PSTools.exe --uninstall first, then run this check.' -ForegroundColor Yellow
    exit 2
}

$work = Join-Path ([IO.Path]::GetTempPath()) ('pstools-check-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$before = Get-Snapshot

try {
    $r = Invoke-Tool @('--install', '--quiet')
    if (-not (Test-Path $menuKey)) { throw 'Install did not create the Explorer menu key.' }

    $r = Invoke-Tool @('new-dated-folder', ('"{0}"' -f $work), '--quiet')
    if (-not (Test-Path (Join-Path $data 'undo.txt'))) { throw 'An action that changes files did not write its undo journal.' }

    if ($IncludeTweaks) {
        $null = Invoke-Tool @('toggle-hidden', ('"{0}"' -f $work), '--quiet')
        $null = Invoke-Tool @('toggle-ext', ('"{0}"' -f $work), '--quiet')
    }

    (Invoke-Tool @('--status', '--quiet')).Text | Write-Host

    $r = Invoke-Tool @('--uninstall', '--quiet')
    $r.Text | Write-Host
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}

$after = Get-Snapshot
$diff = @(
    Get-Difference $before.Keys $after.Keys 'registry'
    Get-Difference $before.Files $after.Files 'files'
    Get-Difference $before.Values $after.Values 'settings'
)

if ($diff.Count -gt 0) {
    Write-Host 'Leftovers after uninstall:' -ForegroundColor Red
    $diff | Write-Host
    exit 1
}

Write-Host 'Clean: nothing PS Tools created or changed remains.' -ForegroundColor Green
exit 0
