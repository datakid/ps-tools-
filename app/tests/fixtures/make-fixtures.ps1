param(
    [Parameter(Mandatory = $true)][string]$Out
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$Out = [IO.Path]::GetFullPath($Out)
if (Test-Path -LiteralPath $Out) { Remove-Item -LiteralPath $Out -Recurse -Force }
New-Item -ItemType Directory -Path $Out | Out-Null

$utf8 = New-Object System.Text.UTF8Encoding($false)
$stamp = [datetime]'2026-01-15T10:30:00'

function Write-Text([string]$path, [string]$text) {
    $dir = Split-Path -Parent $path
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [IO.File]::WriteAllText($path, $text, $utf8)
}

function Write-Bytes([string]$path, [int]$count, [int]$seed) {
    $dir = Split-Path -Parent $path
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $rnd = New-Object System.Random($seed)
    $buf = New-Object byte[] $count
    $rnd.NextBytes($buf)
    [IO.File]::WriteAllBytes($path, $buf)
}

$basic = Join-Path $Out 'tree-basic'
Write-Text (Join-Path $basic 'src\app\main.py') "print('hello')`n"
Write-Text (Join-Path $basic 'src\app\util.py') "def add(a, b):`n    return a + b`n"
New-Item -ItemType Directory -Path (Join-Path $basic 'src\lib') -Force | Out-Null
Write-Text (Join-Path $basic 'docs\readme.md') "# Readme`n`nSample project.`n"
New-Item -ItemType Directory -Path (Join-Path $basic 'empty') -Force | Out-Null
$hidden = New-Item -ItemType Directory -Path (Join-Path $basic '.hidden') -Force
Write-Text (Join-Path $basic '.hidden\secret.txt') "secret`n"
$hidden.Attributes = $hidden.Attributes -bor [IO.FileAttributes]::Hidden
Write-Text (Join-Path $basic 'node_modules\pkg\index.js') "module.exports = 1;`n"
Write-Bytes (Join-Path $basic 'bin\out.dll') 2048 7

$names = Join-Path $Out 'names'
New-Item -ItemType Directory -Path $names | Out-Null
foreach ($n in @("O'Brien.txt", '100% done.txt', 'a&b.txt', '[draft] v1.txt', ([string]::Concat('U', [char]0x308, 'n', [char]0x308, 'i', [char]0x308, 'c', [char]0xF8, 'd', [char]0xE9, ' ', [char]0x65E5, [char]0x672C, '.txt')))) {
    Write-Text (Join-Path $names $n) "sample`n"
}
Write-Text (Join-Path $names 'space name\inside.txt') "inside`n"
try {
    $seg = 'segment_' + ('x' * 31)
    $long = $names
    $limit = 235
    $i = 0
    while (($long.Length + $seg.Length + 1) -lt $limit) { $long = Join-Path $long ($seg + $i); $i++ }
    $prefixed = '\\?\' + $long
    [IO.Directory]::CreateDirectory($prefixed) | Out-Null
    [IO.File]::WriteAllText($prefixed + '\deep.txt', "deep`n", $utf8)
}
catch { Write-Host ("long path fixture skipped: " + $_.Exception.Message) }

$images = Join-Path $Out 'images'
New-Item -ItemType Directory -Path $images | Out-Null
function New-Picture([string]$path, [int]$w, [int]$h, [string]$colorName, [string]$format) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::FromName($colorName))
    $pen = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $g.FillRectangle($pen, [int]($w / 4), [int]($h / 4), [int]($w / 2), [int]($h / 2))
    $g.Dispose()
    if ($format -eq 'jpg') {
        $prop = [System.Runtime.Serialization.FormatterServices]::GetUninitializedObject([System.Drawing.Imaging.PropertyItem])
        $prop.Id = 0x9003
        $prop.Type = 2
        $value = [Text.Encoding]::ASCII.GetBytes("2025:06:01 12:00:00`0")
        $prop.Value = $value
        $prop.Len = $value.Length
        $bmp.SetPropertyItem($prop)
        $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Jpeg)
    }
    else { $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png) }
    $bmp.Dispose()
}
New-Picture (Join-Path $images 'wide.png') 1200 800 'SteelBlue' 'png'
New-Picture (Join-Path $images 'small.png') 640 480 'Crimson' 'png'
New-Picture (Join-Path $images 'tiny.png') 64 64 'SeaGreen' 'png'
New-Picture (Join-Path $images 'photo.jpg') 800 600 'Gray' 'jpg'

$dups = Join-Path $Out 'dups'
Write-Bytes (Join-Path $dups 'a.bin') 4096 11
Copy-Item -LiteralPath (Join-Path $dups 'a.bin') -Destination (Join-Path $dups 'a-copy.bin')
Write-Bytes (Join-Path $dups 'b.bin') 4096 12

$git = Get-Command git -ErrorAction SilentlyContinue
$repo = Join-Path $Out 'repo'
if ($git) {
    Write-Text (Join-Path $repo '.gitignore') "node_modules/`n*.log`n"
    Write-Text (Join-Path $repo 'src\app\main.py') "print('repo')`n"
    Write-Text (Join-Path $repo 'README.md') "# repo`n"
    Write-Text (Join-Path $repo 'debug.log') "ignored`n"
    Write-Text (Join-Path $repo 'node_modules\dep\index.js') "ignored`n"
    function Invoke-Git([string[]]$GitArgs) {
        $previous = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try { & git -c core.autocrlf=false @GitArgs 2>&1 | Out-Null }
        finally { $ErrorActionPreference = $previous }
        if ($LASTEXITCODE -ne 0) { throw ('git ' + ($GitArgs -join ' ') + ' failed with ' + $LASTEXITCODE) }
    }
    Push-Location $repo
    try {
        Invoke-Git @('init', '-q', '-b', 'main')
        Invoke-Git @('config', 'user.name', 'Fixture')
        Invoke-Git @('config', 'user.email', 'fixture@example.com')
        Invoke-Git @('config', 'core.autocrlf', 'false')
        Invoke-Git @('add', '-A')
        $env:GIT_AUTHOR_DATE = '2026-01-15T10:30:00'
        $env:GIT_COMMITTER_DATE = '2026-01-15T10:30:00'
        Invoke-Git @('commit', '-q', '-m', 'Initial commit')
        Invoke-Git @('remote', 'add', 'origin', 'https://github.com/example/repo.git')
    }
    finally {
        Pop-Location
        Remove-Item Env:GIT_AUTHOR_DATE, Env:GIT_COMMITTER_DATE -ErrorAction SilentlyContinue
    }
}
else { Write-Host 'git not found, repo fixture skipped' }

$zipDir = Join-Path $Out 'zip'
New-Item -ItemType Directory -Path $zipDir | Out-Null
function New-Zip([string]$path, [hashtable]$entries) {
    $fs = [IO.File]::Open($path, [IO.FileMode]::Create)
    try {
        $za = New-Object System.IO.Compression.ZipArchive($fs, [System.IO.Compression.ZipArchiveMode]::Create)
        foreach ($key in ($entries.Keys | Sort-Object)) {
            $e = $za.CreateEntry($key)
            $w = New-Object IO.StreamWriter($e.Open(), $utf8)
            $w.Write($entries[$key])
            $w.Dispose()
        }
        $za.Dispose()
    }
    finally { $fs.Dispose() }
}
New-Zip (Join-Path $zipDir 'one-top.zip') @{ 'proj/a.txt' = "a`n"; 'proj/sub/b.txt' = "b`n" }
New-Zip (Join-Path $zipDir 'flat.zip') @{ 'x.txt' = "x`n"; 'y.txt' = "y`n" }

$text = Join-Path $Out 'text'
New-Item -ItemType Directory -Path $text | Out-Null
[IO.File]::WriteAllText((Join-Path $text 'lf.txt'), "one`ntwo`nthree`n", $utf8)
[IO.File]::WriteAllText((Join-Path $text 'crlf.txt'), "one`r`ntwo`r`nthree`r`n", $utf8)
[IO.File]::WriteAllText((Join-Path $text 'bom.txt'), "one`ntwo`n", (New-Object System.Text.UTF8Encoding($true)))
[IO.File]::WriteAllText((Join-Path $text 'utf16.txt'), "one`ntwo`n", [Text.Encoding]::Unicode)

$T = [string][char]0x251C + [char]0x2500 + [char]0x2500 + ' '
$L = [string][char]0x2514 + [char]0x2500 + [char]0x2500 + ' '
$V = [string][char]0x2502 + '   '
$tree = @(
    'sample/'
    ($T + 'docs/')
    ($V + $L + 'guide.md')
    ($T + 'src/')
    ($V + $T + 'main.py')
    ($V + $L + 'util/')
    ($V + '    ' + $L + 'helpers.py')
    ($L + 'README.md')
) -join "`n"
[IO.File]::WriteAllText((Join-Path $Out 'clip-tree.txt'), $tree + "`n", $utf8)

Get-ChildItem -LiteralPath $Out -Recurse -Force | Where-Object { $_.FullName -notmatch '\\\.git(\\|$)' } | ForEach-Object {
    try { $_.LastWriteTime = $stamp } catch { }
}

Write-Host ("Fixtures created in " + $Out)
Get-ChildItem -LiteralPath $Out | ForEach-Object { Write-Host ('  ' + $_.Name) }
