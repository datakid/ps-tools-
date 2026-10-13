param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\out\PSTools.exe')
)

Add-Type -AssemblyName System.Windows.Forms, System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$path = (Resolve-Path $Exe).Path
$asm = [Reflection.Assembly]::LoadFrom($path)
$inst = [Reflection.BindingFlags]'NonPublic,Public,Instance'
$issues = New-Object System.Collections.Generic.List[string]

function Scan($parent, $label) {
    foreach ($k in $parent.Controls) {
        $scrolls = ($parent -is [System.Windows.Forms.ScrollableControl]) -and $parent.AutoScroll
        if ($k.Visible -and -not $scrolls) {
            $limitH = $parent.ClientSize.Height + 1
            $limitW = $parent.ClientSize.Width + 1
            if ($k.Dock -eq 'Top' -and $k.Bottom -gt $limitH) {
                $issues.Add("$label  clipped: $($k.GetType().Name) '$($k.Text)' bottom $($k.Bottom) of $($parent.ClientSize.Height)")
            }
            elseif ($k.Dock -eq 'None' -and ($k.Bottom -gt $limitH -or $k.Right -gt $limitW)) {
                $issues.Add("$label  overflow: $($k.GetType().Name) '$($k.Text)' ($($k.Right) x $($k.Bottom)) in ($($parent.ClientSize.Width) x $($parent.ClientSize.Height))")
            }
        }
        if ($k.HasChildren) { Scan $k $label }
    }
}

function Check($form, $label, $w, $h) {
    if ($w) { $form.ClientSize = New-Object System.Drawing.Size($w, $h) }
    $form.Show()
    [System.Windows.Forms.Application]::DoEvents()
    $before = $issues.Count
    Scan $form $label
    $state = if ($issues.Count -eq $before) { 'ok' } else { 'ISSUES' }
    Write-Host ("{0,-34} {1,4} x {2,-4} {3}" -f $label, $form.ClientSize.Width, $form.ClientSize.Height, $state)
}

$asm.GetType('PSTools.UI').GetMethod('Init').Invoke($null, $null)

$mainType = $asm.GetType('PSTools.MainForm')
$main = [Activator]::CreateInstance($mainType, @($env:USERPROFILE))
$main.Left = 20; $main.Top = 20
$showPage = $mainType.GetMethod('ShowPage', $inst)
foreach ($size in @(@(900, 580), @(760, 480), @(1100, 700))) {
    $showPage.Invoke($main, @($true))
    Check $main ("Actions page") $size[0] $size[1]
    $showPage.Invoke($main, @($false))
    Check $main ("Menu page") $size[0] $size[1]
}

$set = 'System.Collections.Generic.HashSet[string]'
$presets = $asm.GetType('PSTools.Presets')
$ids = $presets.GetMethod('Ids').Invoke($null, @('Everyday'))
$preview = [Activator]::CreateInstance($asm.GetType('PSTools.PreviewForm'), @($ids, (New-Object $set)))
Check $preview 'Menu preview' 0 0

$first = [Activator]::CreateInstance($asm.GetType('PSTools.FirstRunForm'))
Check $first 'First run' 0 0

$recipes = [Activator]::CreateInstance($asm.GetType('PSTools.RecipesForm'))
Check $recipes 'Recipe editor' 0 0
Check $recipes 'Recipe editor (minimum)' 820 520

$picker = [Activator]::CreateInstance($asm.GetType('PSTools.StepPickerForm'))
Check $picker 'Recipe step picker' 0 0

foreach ($f in @($picker, $recipes, $first, $preview, $main)) { $f.Dispose() }

if ($issues.Count -gt 0) {
    Write-Host ''
    $issues | ForEach-Object { Write-Host $_ }
    Write-Host ''
    Write-Host "$($issues.Count) layout problems found."
    exit 1
}
Write-Host ''
Write-Host 'No clipped or overflowing controls found.'
exit 0
