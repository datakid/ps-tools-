param(
    [Parameter(Mandatory = $true)][string]$Fixtures,
    [string]$File = (Join-Path $PSScriptRoot '..\cheatsheet.json'),
    [string]$Exe = (Join-Path $PSScriptRoot '..\out\PSTools.exe'),
    [string]$ExeRunner = '',
    [string]$Report = '',
    [switch]$SkipRun,
    [int]$Parallel = 3,
    [int]$TimeoutSec = 10
)

$ErrorActionPreference = 'Stop'
$modern = $PSVersionTable.PSVersion.Major -ge 6
if (-not $modern) {
    Add-Type -AssemblyName System.Web.Extensions
    $ser = New-Object System.Web.Script.Serialization.JavaScriptSerializer
    $ser.MaxJsonLength = [int]::MaxValue
}
function FromJson([string]$text) {
    if ($modern) { return (ConvertFrom-Json -InputObject $text -AsHashtable) }
    return $ser.DeserializeObject($text)
}
function ToJson($value) {
    if ($modern) { return (ConvertTo-Json -InputObject $value -Depth 8 -Compress) }
    return $ser.Serialize($value)
}
$doc = FromJson ([IO.File]::ReadAllText($File, [Text.Encoding]::UTF8))
$failures = New-Object System.Collections.Generic.List[string]
function Fail([string]$text) { $script:failures.Add($text); Write-Host ('FAIL  ' + $text) }

$requiredCategories = 'nav', 'files', 'search', 'pipeline', 'format', 'clipboard', 'process', 'services', 'network', 'sysinfo', 'disks', 'registry', 'env', 'tasks', 'archive', 'perm', 'events', 'security', 'apps', 'git', 'strings', 'dates', 'math', 'remoting', 'profile'
$riskRank = @{ safe = 0; modifies = 1; destructive = 2 }
$placeholder = "\{(?:paths?|dir|name|stem|ext|parent|clip|date|time|now|gitroot|input:[^{}]+)\}"
$adminPattern = '(?i)netsh\s+wlan\b.*key=clear|\btakeown\b|\b(Start|Stop|Restart|Set)-Service\b|\bClear-DnsClientCache\b|\bEnable-PSRemoting\b|\bNew-NetFirewallRule\b|\bGet-BitLockerVolume\b|\bRepair-Volume\b|\bGet-StorageReliabilityCounter\b|\bNew-LocalUser\b|LogName\s*=\s*''Security''|\bClear-EventLog\b|\bNew-EventLog\b|\bTrustedHosts\b|\bGet-Process\b.*-IncludeUserName|-Verb\s+RunAs|\bRestart-Computer\b|\bNew-Item\b.*SymbolicLink'

$entries = @($doc['entries'])
$categories = @($doc['categories'])
$ids = @{}
$catIds = @{}
foreach ($c in $categories) { $catIds[[string]$c['id']] = 0 }
foreach ($need in $requiredCategories) { if (-not $catIds.ContainsKey($need)) { Fail ("category missing: " + $need) } }

foreach ($e in $entries) {
    $id = [string]$e['id']
    foreach ($f in 'id', 'category', 'title', 'command', 'explain', 'safeToRun', 'risk', 'minVersion') {
        if (-not $e.ContainsKey($f) -or $null -eq $e[$f] -or ([string]$e[$f]).Length -eq 0 -and $f -ne 'safeToRun') { Fail ("$id : required field missing: $f") }
    }
    if ($ids.ContainsKey($id)) { Fail ("duplicate id: $id") }
    $ids[$id] = $e
    if ($id -notmatch '^[a-z0-9-]+\.[a-z0-9-]+$') { Fail ("$id : id must look like category.slug") }
    $cat = [string]$e['category']
    if (-not $catIds.ContainsKey($cat)) { Fail ("$id : unknown category $cat") } else { $catIds[$cat] = $catIds[$cat] + 1 }
    if ($id -notlike ($cat + '.*')) { Fail ("$id : id must start with its category") }
    if (@('5.1', '7.0') -notcontains [string]$e['minVersion']) { Fail ("$id : minVersion must be 5.1 or 7.0") }
    if (-not $riskRank.ContainsKey([string]$e['risk'])) { Fail ("$id : risk must be safe, modifies or destructive") }
    if ($e['safeToRun'] -isnot [bool]) { Fail ("$id : safeToRun must be true or false") }
    if (([string]$e['title']).Length -gt 64) { Fail ("$id : title is longer than 64 characters") }
    foreach ($f in 'command', 'example') {
        if ($e.ContainsKey($f) -and ([string]$e[$f]) -match $placeholder) { Fail ("$id : placeholder in $f (allowed only in actionTemplate.command)") }
    }
    if ($e.ContainsKey('params')) { foreach ($p in @($e['params'])) { if (-not $p['name'] -or -not $p['text']) { Fail ("$id : params need name and text") } } }
    if ($e.ContainsKey('runIn') -and @('repo') -notcontains [string]$e['runIn']) { Fail ("$id : runIn must be repo") }
    if ($e.ContainsKey('actionTemplate')) {
        $t = $e['actionTemplate']
        foreach ($w in @($t['when'])) { if (@('file', 'folder', 'background', 'drive') -notcontains [string]$w) { Fail ("$id : actionTemplate.when has $w") } }
        if (@('console', 'clipboard', 'grid', 'toast') -notcontains [string]$t['output']) { Fail ("$id : actionTemplate.output is " + $t['output']) }
        if (-not $t['command']) { Fail ("$id : actionTemplate.command is empty") }
    }
}
foreach ($e in $entries) {
    if ($e.ContainsKey('related')) { foreach ($r in @($e['related'])) { if (-not $ids.ContainsKey([string]$r)) { Fail (([string]$e['id']) + " : related id not found: $r") } } }
}

$cmdRows = @($doc['translations']['cmd'])
$bashRows = @($doc['translations']['bash'])
foreach ($t in ($cmdRows + $bashRows)) { if (-not $t['from'] -or -not $t['to']) { Fail 'translation row needs from and to' } }
$commandNames = @($doc['commands'])

if ($entries.Count -lt 300) { Fail ("only {0} entries, need at least 300" -f $entries.Count) }
foreach ($k in @($catIds.Keys)) { if ($catIds[$k] -lt 8) { Fail ("category {0} has {1} entries, need at least 8" -f $k, $catIds[$k]) } }
if ($cmdRows.Count -lt 40) { Fail ("cmd table has {0} rows, need at least 40" -f $cmdRows.Count) }
if ($bashRows.Count -lt 60) { Fail ("bash table has {0} rows, need at least 60" -f $bashRows.Count) }
if ($commandNames.Count -lt 200) { Fail ("commands list has {0} names, need at least 200 (run app/tools/gen-commands-list.ps1)" -f $commandNames.Count) }

$isPs5 = $PSVersionTable.PSVersion.Major -lt 6
$pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
$parseItems = New-Object System.Collections.Generic.List[object]
function Prepare([string]$text) { return [regex]::Replace($text, $placeholder, "'C:\x'") }
foreach ($e in $entries) {
    $v7 = [string]$e['minVersion'] -eq '7.0'
    $id = [string]$e['id']
    $parseItems.Add(@{ label = $id + ' command'; text = (Prepare ([string]$e['command'])); v7 = $v7 })
    if ($e.ContainsKey('example')) { $parseItems.Add(@{ label = $id + ' example'; text = (Prepare ([string]$e['example'])); v7 = $v7 }) }
    if ($e.ContainsKey('actionTemplate')) { $parseItems.Add(@{ label = $id + ' actionTemplate'; text = (Prepare ([string]$e['actionTemplate']['command'])); v7 = $v7 }) }
}
foreach ($t in $cmdRows) { $parseItems.Add(@{ label = 'cmd translation: ' + $t['from']; text = (Prepare ([string]$t['to'])); v7 = $false }) }
foreach ($t in $bashRows) { $parseItems.Add(@{ label = 'bash translation: ' + $t['from']; text = (Prepare ([string]$t['to'])); v7 = $false }) }

function Get-ParseErrors($items) {
    $out = New-Object System.Collections.Generic.List[string]
    foreach ($i in $items) {
        $errors = $null
        [void][System.Management.Automation.Language.Parser]::ParseInput($i.text, [ref]$null, [ref]$errors)
        if ($errors -and $errors.Count -gt 0) { $out.Add(('{0} : line {1}: {2}' -f $i.label, $errors[0].Extent.StartLineNumber, $errors[0].Message)) }
    }
    return $out
}

$parsed = 0
$parseFailed = 0
$here = @($parseItems | Where-Object { -not $_.v7 -or -not $isPs5 })
foreach ($m in (Get-ParseErrors $here)) { Fail ('parse (' + $(if ($isPs5) { '5.1' } else { 'pwsh' }) + ') ' + $m); $parseFailed++ }
$parsed += $here.Count
$there = @($parseItems | Where-Object { $_.v7 -and $isPs5 })
if ($there.Count -gt 0) {
    if ($pwsh) {
        $tmpJson = [IO.Path]::GetTempFileName()
        $items = @($there | ForEach-Object { @{ label = $_.label; text = $_.text } })
        [IO.File]::WriteAllText($tmpJson, (ToJson @($items)), (New-Object Text.UTF8Encoding($false)))
        $child = [IO.Path]::GetTempFileName() + '.ps1'
        $body = @'
param($Json)
Add-Type -AssemblyName System.Web.Extensions -ErrorAction SilentlyContinue
$items = [System.Text.Json.JsonSerializer]::Deserialize([IO.File]::ReadAllText($Json), [object[]])
$items = Get-Content -Raw -LiteralPath $Json | ConvertFrom-Json
foreach ($i in $items) {
    $errors = $null
    [void][System.Management.Automation.Language.Parser]::ParseInput($i.text, [ref]$null, [ref]$errors)
    if ($errors -and $errors.Count -gt 0) { '{0} : line {1}: {2}' -f $i.label, $errors[0].Extent.StartLineNumber, $errors[0].Message }
}
'@
        $body = ($body -split "`n" | Where-Object { $_ -notmatch 'JsonSerializer' }) -join "`n"
        [IO.File]::WriteAllText($child, $body, (New-Object Text.UTF8Encoding($true)))
        $lines = @(& $pwsh.Source -NoProfile -File $child $tmpJson)
        foreach ($l in $lines) { if ($l) { Fail ('parse (pwsh) ' + $l); $parseFailed++ } }
        $parsed += $there.Count
        Remove-Item $tmpJson, $child -ErrorAction SilentlyContinue
    }
    else { Write-Host ('pwsh not found: {0} PowerShell 7 entries were not parsed' -f $there.Count) }
}

function Invoke-Exe([string[]]$arguments) {
    if ($ExeRunner) { return & $ExeRunner $Exe @arguments }
    return & $Exe @arguments
}

$riskChecked = 0
if (Test-Path -LiteralPath $Exe) {
    $riskInput = New-Object System.Collections.Generic.List[object]
    foreach ($e in $entries) {
        $id = [string]$e['id']
        $riskInput.Add(@{ id = $id + '|command'; text = [string]$e['command'] })
        if ($e.ContainsKey('example')) { $riskInput.Add(@{ id = $id + '|example'; text = [string]$e['example'] }) }
        if ($e.ContainsKey('actionTemplate')) { $riskInput.Add(@{ id = $id + '|actionTemplate'; text = (Prepare ([string]$e['actionTemplate']['command'])) }) }
    }
    $riskFile = [IO.Path]::GetTempFileName()
    $riskOut = [IO.Path]::GetTempFileName()
    [IO.File]::WriteAllText($riskFile, (ToJson $riskInput.ToArray()), (New-Object Text.UTF8Encoding($false)))
    Invoke-Exe @('--risk-classify-json', $riskFile, '--out', $riskOut) | Out-Null
    $scan = (FromJson ([IO.File]::ReadAllText($riskOut, [Text.Encoding]::UTF8)))
    foreach ($s in @($scan)) {
        $riskChecked++
        $id = ([string]$s['id']).Split('|')[0]
        $e = $ids[$id]
        $declared = [string]$e['risk']
        if ($riskRank[[string]$s['risk']] -gt $riskRank[$declared]) { Fail ("{0} : RiskScan says {1} ({2}) but risk is {3}" -f $s['id'], $s['risk'], (@($s['reasons']) -join '; '), $declared) }
    }
    Remove-Item $riskFile, $riskOut -ErrorAction SilentlyContinue
}
else { Fail ("PSTools.exe not found at $Exe, cannot run the safety lint") }

foreach ($e in $entries) {
    $id = [string]$e['id']
    $text = [string]$e['command'] + "`n" + $(if ($e.ContainsKey('example')) { [string]$e['example'] } else { '' })
    if ($e['safeToRun'] -eq $true -and [string]$e['risk'] -ne 'safe') { Fail ("$id : safeToRun entries must have risk safe") }
    if ([string]$e['risk'] -eq 'destructive' -and $e['safeToRun'] -eq $true) { Fail ("$id : destructive entries must not be safeToRun") }
    if (($text -match $adminPattern -or $e['admin'] -eq $true) -and ([string]$e['explain']) -notmatch '(?i)administrator') { Fail ("$id : needs administrator rights, so explain must say so") }
    if ($text -match '(?i)\b(Invoke-WebRequest|Invoke-RestMethod|Test-Connection|Resolve-DnsName|Test-NetConnection|Install-Module|Find-Module|winget|curl|wget)\b' -and $e['safeToRun'] -eq $true) { Fail ("$id : network entries must set safeToRun false") }
}

$ran = 0
$skipped = 0
$runFailed = 0
$timings = New-Object System.Collections.Generic.List[object]
if (-not $SkipRun) {
    $basic = Join-Path $Fixtures 'tree-basic'
    $repo = Join-Path $Fixtures 'repo'
    $work = Join-Path ([IO.Path]::GetTempPath()) ('cheatsheet-run-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $work | Out-Null
    $queue = New-Object System.Collections.Generic.Queue[object]
    foreach ($e in $entries) {
        if ($e['safeToRun'] -ne $true) { $skipped++; continue }
        $v7 = [string]$e['minVersion'] -eq '7.0'
        if ($v7 -and -not $pwsh) { $skipped++; Write-Host ("skip  {0} : needs pwsh" -f $e['id']); continue }
        $cwd = $basic
        if ($e.ContainsKey('runIn') -and [string]$e['runIn'] -eq 'repo') {
            if (-not (Test-Path -LiteralPath $repo)) { $skipped++; Write-Host ("skip  {0} : repo fixture missing" -f $e['id']); continue }
            $cwd = $repo
        }
        $text = if ($e.ContainsKey('example')) { [string]$e['example'] } else { [string]$e['command'] }
        $script = Join-Path $work (([string]$e['id']) + '.ps1')
        [IO.File]::WriteAllText($script, ('$ErrorActionPreference = ''Stop''' + "`r`n" + $text + "`r`n"), (New-Object Text.UTF8Encoding($true)))
        $queue.Enqueue(@{ entry = $e; script = $script; cwd = $cwd; v7 = $v7 })
    }
    $active = New-Object System.Collections.Generic.List[object]
    function Finish($job) {
        $e = $job.entry
        $p = $job.proc
        $ms = [int]$job.watch.ElapsedMilliseconds
        $script:ran++
        $script:timings.Add(@{ id = [string]$e['id']; ms = $ms; exit = $(if ($job.timeout) { 'timeout' } else { $p.ExitCode }) })
        if ($job.timeout) { $script:runFailed++; Fail ("{0} : timed out after {1} s" -f $e['id'], $TimeoutSec); return }
        if ($p.ExitCode -ne 0 -and $e['expectError'] -ne $true) {
            $script:runFailed++
            $msg = ''
            try { $msg = (Get-Content -LiteralPath $job.err -Raw -ErrorAction SilentlyContinue) } catch { }
            if ($msg) { $msg = ($msg.Trim() -split "`n" | Select-Object -First 3) -join ' | ' }
            Fail ("{0} : exit code {1} {2}" -f $e['id'], $p.ExitCode, $msg)
        }
    }
    while ($queue.Count -gt 0 -or $active.Count -gt 0) {
        while ($queue.Count -gt 0 -and $active.Count -lt $Parallel) {
            $item = $queue.Dequeue()
            $exeName = if ($item.v7) { $pwsh.Source } else { Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe' }
            if (-not (Test-Path -LiteralPath $exeName)) { $exeName = if ($pwsh) { $pwsh.Source } else { 'powershell' } }
            $psi = New-Object System.Diagnostics.ProcessStartInfo
            $psi.FileName = $exeName
            $psi.Arguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + $item.script + '"'
            $psi.WorkingDirectory = $item.cwd
            $psi.UseShellExecute = $false
            $psi.CreateNoWindow = $true
            $psi.RedirectStandardOutput = $true
            $psi.RedirectStandardError = $true
            $psi.RedirectStandardInput = $true
            $p = [System.Diagnostics.Process]::Start($psi)
            $p.StandardInput.Close()
            $errFile = $item.script + '.err'
            $outTask = $p.StandardOutput.ReadToEndAsync()
            $errTask = $p.StandardError.ReadToEndAsync()
            $active.Add(@{ entry = $item.entry; proc = $p; watch = [System.Diagnostics.Stopwatch]::StartNew(); err = $errFile; errTask = $errTask; outTask = $outTask; timeout = $false })
        }
        Start-Sleep -Milliseconds 60
        foreach ($job in @($active)) {
            $p = $job.proc
            if ($p.HasExited) {
                try { [IO.File]::WriteAllText($job.err, $job.errTask.Result) } catch { }
                Finish $job
                [void]$active.Remove($job)
            }
            elseif ($job.watch.Elapsed.TotalSeconds -gt $TimeoutSec) {
                try { & taskkill.exe /T /F /PID $p.Id 2>$null | Out-Null } catch { try { $p.Kill() } catch { } }
                $job.timeout = $true
                Finish $job
                [void]$active.Remove($job)
            }
        }
    }
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
else { $skipped = @($entries | Where-Object { $_['safeToRun'] -eq $true }).Count; Write-Host 'run step skipped (-SkipRun)' }

Write-Host ''
Write-Host ('entries {0}, categories {1}, cmd rows {2}, bash rows {3}, commands {4}' -f $entries.Count, $categories.Count, $cmdRows.Count, $bashRows.Count, $commandNames.Count)
Write-Host ('parsed {0}/{0} texts ({1} errors), risk scans {2}, ran {3}, skipped {4}, failed {5}' -f $parsed, $parseFailed, $riskChecked, $ran, $skipped, $runFailed)
if ($Report) {
    $summary = @{ entries = $entries.Count; parsed = $parsed; parseErrors = $parseFailed; riskScans = $riskChecked; ran = $ran; skipped = $skipped; failed = $failures.Count; timings = $timings.ToArray(); failures = $failures.ToArray() }
    [IO.File]::WriteAllText($Report, (ToJson $summary), (New-Object Text.UTF8Encoding($false)))
}
if ($failures.Count -gt 0) { Write-Host ('{0} problems' -f $failures.Count); exit 1 }
Write-Host 'cheat sheet check passed'
exit 0
