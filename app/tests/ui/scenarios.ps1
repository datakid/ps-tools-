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


function Get-RowNames($root) {
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::MenuItem)
    $items = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
    $rows = @()
    foreach ($i in $items) { $rows += [pscustomobject]@{ Name = $i.Current.Name; Y = $i.Current.BoundingRectangle.Y } }
    return @($rows | Sort-Object Y | ForEach-Object { $_.Name })
}

function Go-To-Row($el, [string]$name) {
    $names = Get-RowNames $el
    $headers = @('Recent', 'Suggested', 'Favorites')
    $selectable = @($names | Where-Object { $headers -notcontains $_ })
    $index = [array]::IndexOf($selectable, $name)
    if ($index -lt 0) { return $false }
    if ($index -gt 0) { Send-Keys $el ('{DOWN ' + $index + '}') }
    return $true
}

$env = New-Env 'drill'
$h = Start-Popup $env 'menu' $folder
$el = Find-Popup 15000
if (-not $el) { Add-Result 'popup-drill' $false 'popup did not appear' }
else {
    Start-Sleep -Milliseconds 400
    $rootNames = Get-RowNames $el
    Add-Result 'popup-rows-exposed' ($rootNames.Count -gt 5) ('rows are visible to UI Automation: ' + (($rootNames | Select-Object -First 6) -join ', '))
    $ok1 = Go-To-Row $el 'Copy'
    if ($ok1) { Send-Keys $el '{RIGHT}' }
    $inCopy = Get-RowNames $el
    $ok2 = ($inCopy -contains 'Paths') -and ($inCopy -notcontains 'Recent')
    if ($ok2 -and (Go-To-Row $el 'Tree')) { Send-Keys $el '{RIGHT}' }
    $inTree = Get-RowNames $el
    Snap $el '5-popup-drill'
    Add-Result 'popup-drill' (($inTree -contains 'Tree (ASCII)') -and ($inTree -notcontains 'Recent') -and ($inTree -notcontains 'Paths')) ('drilled Copy > Tree with the keyboard: ' + ($inTree -join ', '))
    Send-Keys $el '{BACKSPACE}'
    $back = Get-RowNames $el
    Add-Result 'popup-back' (($back -contains 'Paths') -and ($back -notcontains 'Tree (ASCII)')) ('Backspace with an empty search box goes up one level: ' + (($back | Select-Object -First 5) -join ', '))
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


function Find-Window([string]$name, [int]$timeoutMs) {
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $timeoutMs) {
        $el = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
        if ($el) { return $el }
        Start-Sleep -Milliseconds 100
    }
    return $null
}

function Find-Dialog($owner, [string]$name, [int]$timeoutMs) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $timeoutMs) {
        $el = Find-Named $owner $name $null
        if (-not $el) { $el = Find-Window $name 200 }
        if ($el) { return $el }
        Start-Sleep -Milliseconds 150
    }
    return $null
}

function Find-Named($root, [string]$name, $type) {
    $c1 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
    if ($type) {
        $c2 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $type)
        $c1 = New-Object System.Windows.Automation.AndCondition($c1, $c2)
    }
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c1)
}

function Click-Button($root, [string]$name) {
    $b = Find-Named $root $name ([System.Windows.Automation.ControlType]::Button)
    if (-not $b) { return $false }
    $p = $b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $p.Invoke()
    return $true
}

Add-Type -Namespace Native -Name Mouse -MemberDefinition @"
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, System.UIntPtr extra);
"@

function Click-Element($el, [double]$fraction) {
    $r = $el.Current.BoundingRectangle
    if ($r.Width -lt 2) { return $false }
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point([int]($r.X + $r.Width * $fraction), [int]($r.Y + $r.Height / 2))
    Start-Sleep -Milliseconds 150
    [Native.Mouse]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [Native.Mouse]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 250
    return $true
}

function Set-Combo($dlg, [string]$name, [string]$text) {
    $el = Find-Named $dlg $name $null
    if (-not $el) { return 'combo not found' }
    Write-Host ('combo ' + $name + ': ' + $el.Current.ControlType.ProgrammaticName + ' class=' + $el.Current.ClassName)
    foreach ($c in $el.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)) { Write-Host ('   child: ' + $c.Current.ControlType.ProgrammaticName + ' name=' + $c.Current.Name + ' class=' + $c.Current.ClassName) }
    try { $el.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($text); return 'value' } catch { }
    try {
        $el.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
        Start-Sleep -Milliseconds 400
        $cond = New-Object System.Windows.Automation.AndCondition((New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $text)), (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)))
        $item = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
        if ($item) { $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); return 'list' }
        return 'list item not found'
    } catch { return 'failed: ' + $_.Exception.Message }
}

function Press-Button($root, [string]$name) {
    $b = Find-Named $root $name ([System.Windows.Automation.ControlType]::Button)
    if (-not $b) { return $false }
    try { $b.SetFocus() } catch { }
    Start-Sleep -Milliseconds 200
    [System.Windows.Forms.SendKeys]::SendWait(' ')
    return $true
}

function Get-OutputText($win) {
    $o = Find-Named $win 'Command output' $null
    if (-not $o) { return '' }
    try { return $o.GetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern).DocumentRange.GetText(-1) } catch { return '' }
}

function Get-StatusText($win) {
    $s = Find-Named $win 'Run status' $null
    if (-not $s) { return '' }
    return $s.Current.HelpText
}

function Wait-Status($win, [string]$pattern, [int]$timeoutMs) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $timeoutMs) {
        $t = Get-StatusText $win
        if ($t -match $pattern) { return $t }
        Start-Sleep -Milliseconds 200
    }
    return Get-StatusText $win
}

function Start-Run($env, [string]$shellKey, [string]$command, [string]$dirPath) {
    $arguments = 'run "{0}" --shell {1} --command "{2}" --settings "{3}" --data-dir "{4}"' -f $dirPath, $shellKey, $command, $env.Settings, $env.Dir
    return Start-Process -FilePath $Exe -ArgumentList $arguments -PassThru
}

function Stop-All { Get-Process -Name ([IO.Path]::GetFileNameWithoutExtension($Exe)) -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue }

$env = New-Env 'run-echo'
$p = Start-Run $env 'ps' 'Write-Output hello-pst' $folder
$win = Find-Window 'PS Tools Run console' 15000
if (-not $win) { Add-Result 'run-echo' $false 'Run console did not open' }
else {
    Start-Sleep -Milliseconds 500
    Send-Keys $win '^{ENTER}'
    $status = Wait-Status $win 'Exit' 20000
    $text = Get-OutputText $win
    Snap $win '7-run-echo'
    Add-Result 'run-echo' (($status -match 'Exit 0') -and ($text -match 'hello-pst')) ('status: ' + $status + '; output has hello-pst: ' + ($text -match 'hello-pst'))
}
Stop-All

$env = New-Env 'run-cmd'
$p = Start-Run $env 'cmd' 'echo hello-cmd' $folder
$win = Find-Window 'PS Tools Run console' 15000
if (-not $win) { Add-Result 'run-cmd' $false 'Run console did not open' }
else {
    Start-Sleep -Milliseconds 500
    Send-Keys $win '^{ENTER}'
    $status = Wait-Status $win 'Exit' 20000
    $text = Get-OutputText $win
    Add-Result 'run-cmd' (($status -match 'Exit 0') -and ($text -match 'hello-cmd')) ('status: ' + $status)
}
Stop-All

$env = New-Env 'run-stop'
$p = Start-Run $env 'ps' 'Start-Sleep -Seconds 60' $folder
$win = Find-Window 'PS Tools Run console' 15000
if (-not $win) { Add-Result 'run-stop' $false 'Run console did not open' }
else {
    Start-Sleep -Milliseconds 500
    Send-Keys $win '^{ENTER}'
    $running = Wait-Status $win 'Running' 8000
    Start-Sleep -Milliseconds 1200
    Send-Keys $win '{ESC}'
    $status = Wait-Status $win 'Stopped' 10000
    Add-Result 'run-stop' ($status -match 'Stopped') ('while running: ' + $running + '; after Esc: ' + $status)
}
Stop-All

$env = New-Env 'run-table'
$p = Start-Run $env 'ps' 'Get-ChildItem | Select-Object Name, Length' $folder
$win = Find-Window 'PS Tools Run console' 15000
if (-not $win) { Add-Result 'run-table' $false 'Run console did not open' }
else {
    Start-Sleep -Milliseconds 500
    $clicked = Click-Button $win 'Show as table'
    $table = Find-Window ('Result of: Get-ChildItem | Select-Object Name, Length') 20000
    Add-Result 'run-table' ([bool]($clicked -and $table)) 'Show as table opens a result window with the rows'
    if ($table) { Snap $table '8-run-table' }
}
Stop-All

$env = New-Env 'roundtrip'
$command = 'Get-ChildItem -Recurse -Filter *.py | Select-Object Name, Length'
$p = Start-Run $env 'ps' $command $folder
$win = Find-Window 'PS Tools Run console' 15000
if (-not $win) { Add-Result 'save-action-roundtrip' $false 'Run console did not open' }
else {
    Start-Sleep -Milliseconds 500
    Send-Keys $win '^{ENTER}'
    $status = Wait-Status $win 'Exit' 20000
    Send-Keys $win '^s'
    $opened = $true
    $dlg = Find-Dialog $win 'Save as action' 10000
    $ok = $true
    $problem = ''
    if (-not $dlg) { $ok = $false; $problem = 'Save as action did not open (button found: ' + $opened + ', run status: ' + $status + ')' }
    else {
        Start-Sleep -Milliseconds 500
        $titleBox = Find-Named $dlg 'Action title' ([System.Windows.Automation.ControlType]::Edit)
        if ($titleBox) { $titleBox.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('Python files') } else { $ok = $false; $problem = 'title box not found' }
        $how1 = Set-Combo $dlg 'Menu location' 'Search'
        $how2 = Set-Combo $dlg 'Result' 'Copy output to the clipboard'
        $problem += ' [menu: ' + $how1 + ', result: ' + $how2 + ']'
        Start-Sleep -Milliseconds 300
        $titleBox2 = Find-Named $dlg 'Action title' ([System.Windows.Automation.ControlType]::Edit)
        if ($titleBox2) { $titleBox2.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('Python files') }
        Start-Sleep -Milliseconds 300
        Snap $dlg '9-save-action'
        [void](Click-Button $dlg 'Save')
        Start-Sleep -Milliseconds 1500
    }
    $saved = Get-ChildItem -LiteralPath $env.Dir -Recurse -Filter 'Python files*.ps1' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $saved) { $ok = $false; $problem += ' no file was written; files: ' + ((Get-ChildItem -LiteralPath $env.Dir -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName.Substring($env.Dir.Length) }) -join ', ') }
    elseif (-not ((Get-Content -LiteralPath $saved.FullName -Raw) -match '@menu Search')) { $ok = $false; $problem += ' menu was not Search: ' + (Get-Content -LiteralPath $saved.FullName -Raw).Substring(0, 120) }
    Stop-All
    if ($ok) {
        $other = Join-Path $Fixtures 'repo'
        Set-Clipboard -Value 'untouched'
        $arguments = 'menu "{0}" --settings "{1}" --data-dir "{2}" --tools git,code' -f $other, $env.Settings, $env.Dir
        $proc = Start-Process -FilePath $Exe -ArgumentList $arguments -PassThru
        $popup = Find-Popup 15000
        if ($popup) {
            Send-Keys $popup 'python files'
            Snap $popup '10-roundtrip-popup'
            Send-Keys $popup '{ENTER}'
            $sw = [Diagnostics.Stopwatch]::StartNew()
            while (-not $proc.HasExited -and $sw.ElapsedMilliseconds -lt 20000) { Start-Sleep -Milliseconds 200 }
            $clip = Get-Clipboard -Raw
            $ok = [bool]($clip -match 'main\.py')
            $problem = 'clipboard after running from the popup: ' + $(if ($clip) { ($clip -split "`n" | Select-Object -First 3) -join ' | ' } else { '<empty>' })
        }
        else { $ok = $false; $problem = 'popup did not open' }
    }
    Add-Result 'save-action-roundtrip' $ok ('typed a command, saved it as Python files under Search, ran it from the popup on another folder. ' + $problem)
}
Stop-All

Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
$failed = @($results | Where-Object { -not $_.Ok }).Count
Write-Host ('{0} scenarios, {1} failed' -f $results.Count, $failed)
if ($failed -gt 0) { exit 1 }
exit 0
