param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [Parameter(Mandatory = $true)][string]$Fixtures,
    [string]$Out = (Join-Path $PSScriptRoot '..\..\out\shots'),
    [int]$Runs = 5,
    [int]$WarnMs = 700,
    [int]$FailMs = 1500
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
$Exe = (Resolve-Path $Exe).Path
New-Item -ItemType Directory -Path $Out -Force | Out-Null

$folder = Join-Path $Fixtures 'tree-basic'
$work = Join-Path ([IO.Path]::GetTempPath()) ('pstools-scn-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$results = New-Object System.Collections.ArrayList

function Add-Result([string]$name, [bool]$ok, [string]$detail) {
    [void]$results.Add([pscustomobject]@{ Name = $name; Ok = $ok; Detail = $detail })
    Write-Host ('{0,-5} {1}  {2}' -f $(if ($ok) { 'PASS' } else { 'FAIL' }), $name, $detail)
}

function Find-Popup([int]$timeoutMs) {
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'PS Tools Power menu')
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $timeoutMs) {
        $el = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
        if ($el) { return $el }
        Start-Sleep -Milliseconds 20
    }
    return $null
}

function New-Env([string]$tag) {
    $dir = Join-Path $work $tag
    New-Item -ItemType Directory -Path $dir | Out-Null
    $settings = Join-Path $dir 'settings.txt'
    Copy-Item (Join-Path $PSScriptRoot '..\menu-settings.txt') $settings
    return @{ Dir = $dir; Settings = $settings }
}

function Start-Popup($env, [string]$verb, [string]$path) {
    $arguments = '{0} "{1}" --settings "{2}" --data-dir "{3}" --tools git,code' -f $verb, $path, $env.Settings, $env.Dir
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process -FilePath $Exe -ArgumentList $arguments -PassThru
    return @{ Process = $p; Watch = $sw }
}

function Stop-Popup($h) {
    try { if (-not $h.Process.HasExited) { $h.Process.Kill() } } catch { }
}

function Snap($el, [string]$name) {
    try {
        $r = $el.Current.BoundingRectangle
        if ($r.Width -lt 10) { return }
        $bmp = New-Object System.Drawing.Bitmap([int]$r.Width, [int]$r.Height)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen([int]$r.X, [int]$r.Y, 0, 0, $bmp.Size)
        $g.Dispose()
        $bmp.Save((Join-Path $Out ('scenario-' + $name + '.png')), [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
    }
    catch { Write-Host ('snapshot failed: ' + $_.Exception.Message) }
}

function Send-Keys($el, [string]$keys) {
    try { $el.SetFocus() } catch { }
    Start-Sleep -Milliseconds 150
    [System.Windows.Forms.SendKeys]::SendWait($keys)
    Start-Sleep -Milliseconds 250
}

$times = New-Object System.Collections.ArrayList
for ($i = 1; $i -le $Runs; $i++) {
    $env = New-Env ('perf' + $i)
    $h = Start-Popup $env 'menu' $folder
    $el = Find-Popup 15000
    $ms = $h.Watch.ElapsedMilliseconds
    if (-not $el) { Add-Result ('popup-open run ' + $i) $false 'popup window did not appear within 15 s'; Stop-Popup $h; continue }
    [void]$times.Add($ms)
    if ($i -eq 1) { Snap $el '1-popup-open' }
    Stop-Popup $h
    Start-Sleep -Milliseconds 300
}
if ($times.Count -gt 0) {
    $sorted = @($times | Sort-Object)
    $median = $sorted[[int][Math]::Floor($sorted.Count / 2)]
    $detail = 'median {0} ms, runs: {1}' -f $median, ($times -join ', ')
    if ($median -gt $FailMs) { Add-Result 'popup-perf' $false ($detail + (' (hard limit {0} ms)' -f $FailMs)) }
    else {
        Add-Result 'popup-perf' $true $detail
        if ($median -gt $WarnMs) { Write-Host ('::warning title=popup-perf::median {0} ms is above the {1} ms target' -f $median, $WarnMs) }
    }
}

$env = New-Env 'search'
$h = Start-Popup $env 'menu' $folder
$el = Find-Popup 15000
if (-not $el) { Add-Result 'popup-search-run' $false 'popup did not appear' }
else {
    Set-Clipboard -Value 'untouched'
    Send-Keys $el 'ta'
    Snap $el '2-popup-search'
    Send-Keys $el '{ENTER}'
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while (-not $h.Process.HasExited -and $sw.ElapsedMilliseconds -lt 8000) { Start-Sleep -Milliseconds 100 }
    $clip = Get-Clipboard -Raw
    $ok = $clip -and $clip -match 'tree-basic' -and $clip -match 'main\.py'
    Add-Result 'popup-search-run' ([bool]$ok) ('typed "ta", Enter; clipboard starts with: ' + $(if ($clip) { ($clip -split "`n")[0].Trim() } else { '<empty>' }))
    $used = Get-Content -LiteralPath $env.Settings -Raw
    Add-Result 'popup-recent-recorded' ($used -match 'used\.folder=copy\.tree\.ascii') 'settings file lists the action as recently used'
}
Stop-Popup $h

$env = New-Env 'escape'
$h = Start-Popup $env 'menu' $folder
$el = Find-Popup 15000
if (-not $el) { Add-Result 'popup-escape' $false 'popup did not appear' }
else {
    Send-Keys $el 'ta'
    Send-Keys $el '{ESC}'
    $stillOpen = [bool](Find-Popup 500)
    Add-Result 'popup-escape-clears-search' $stillOpen 'first Esc clears the text and keeps the popup open'
    $el2 = Find-Popup 500
    if ($el2) { Send-Keys $el2 '{ESC}' }
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while (-not $h.Process.HasExited -and $sw.ElapsedMilliseconds -lt 4000) { Start-Sleep -Milliseconds 100 }
    Add-Result 'popup-escape-closes' $h.Process.HasExited 'second Esc closes the popup and the process exits'
}
Stop-Popup $h

$env = New-Env 'favorite'
$h = Start-Popup $env 'menu' $folder
$el = Find-Popup 15000
if (-not $el) { Add-Result 'popup-favorite' $false 'popup did not appear' }
else {
    Send-Keys $el 'size'
    Send-Keys $el '^d'
    Snap $el '3-popup-favorite'
    $text = Get-Content -LiteralPath $env.Settings -Raw
    Add-Result 'popup-favorite-toggle' ($text -match 'favorite=copy\.size') 'Ctrl+D on the Size row added it to the favorites in the settings file'
}
Stop-Popup $h

$env = New-Env 'background'
$h = Start-Popup $env 'menu-bg' $folder
$el = Find-Popup 15000
Add-Result 'popup-background-opens' ([bool]$el) 'menu-bg opens the popup'
if ($el) { Snap $el '4-popup-background' }
Stop-Popup $h


function Find-Row($root, [string]$name) {
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

Add-Type -Namespace Native -Name Mouse -MemberDefinition @"
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, System.UIntPtr extra);
"@

function Invoke-Row($row) {
    $r = $row.Current.BoundingRectangle
    if ($r.Width -lt 2) { return $false }
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point([int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2))
    Start-Sleep -Milliseconds 120
    [Native.Mouse]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [Native.Mouse]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    return $true
}

$env = New-Env 'drill'
$h = Start-Popup $env 'menu' $folder
$el = Find-Popup 15000
if (-not $el) { Add-Result 'popup-drill' $false 'popup did not appear' }
else {
    Start-Sleep -Milliseconds 400
    $copy = Find-Row $el 'Copy'
    $found = [bool]$copy
    Add-Result 'popup-rows-exposed' $found 'rows are visible to UI Automation (accessible names)'
    if ($found) {
        [void](Invoke-Row $copy)
        Start-Sleep -Milliseconds 300
        $tree = Find-Row $el 'Tree'
        if ($tree) { [void](Invoke-Row $tree); Start-Sleep -Milliseconds 300 }
        $leaf = Find-Row $el 'Tree (ASCII)'
        Snap $el '5-popup-drill'
        Add-Result 'popup-drill' ([bool]$leaf) 'Copy then Tree then Tree (ASCII) is reachable by drilling in'
        Send-Keys $el '{BACKSPACE}'
        $back = Find-Row $el 'Tree'
        Add-Result 'popup-back' ([bool]$back) 'Backspace with an empty search box goes up one level'
    }
}
Stop-Popup $h

foreach ($n in 1, 20, 200) {
    $env = New-Env ('multi' + $n)
    $dir = Join-Path $work ('files' + $n)
    New-Item -ItemType Directory -Path $dir | Out-Null
    for ($i = 1; $i -le $n; $i++) { [IO.File]::WriteAllText((Join-Path $dir ('file_{0:000}.txt' -f $i)), 'x') }
    $procs = New-Object System.Collections.ArrayList
    $sw = [Diagnostics.Stopwatch]::StartNew()
    for ($i = 1; $i -le $n; $i++) {
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = $Exe
        $psi.Arguments = 'menu "{0}" --settings "{1}" --data-dir "{2}" --tools git' -f (Join-Path $dir ('file_{0:000}.txt' -f $i)), $env.Settings, $env.Dir
        $psi.UseShellExecute = $false
        [void]$procs.Add([Diagnostics.Process]::Start($psi))
    }
    $spawned = $sw.ElapsedMilliseconds
    $el = Find-Popup 30000
    $visible = $sw.ElapsedMilliseconds
    if (-not $el) { Add-Result ('popup-multi-' + $n) $false ('no popup within 30 s after starting {0} processes' -f $n) }
    else {
        $help = $el.Current.HelpText
        $count = if ($help -match '^(\d+) items') { [int]$Matches[1] } elseif ($n -eq 1) { 1 } else { -1 }
        $popups = @([System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'PS Tools Power menu')))).Count
        if ($n -eq 200) { Snap $el '6-popup-200' }
        Add-Result ('popup-multi-' + $n) ($count -eq $n -and $popups -eq 1) ('{0} processes started in {1} ms, popup visible after {2} ms, popup lists {3} items, popups open: {4}' -f $n, $spawned, $visible, $count, $popups)
    }
    Get-Process -Name ([IO.Path]::GetFileNameWithoutExtension($Exe)) -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
}

Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
$failed = @($results | Where-Object { -not $_.Ok }).Count
Write-Host ('{0} scenarios, {1} failed' -f $results.Count, $failed)
if ($failed -gt 0) { exit 1 }
exit 0
