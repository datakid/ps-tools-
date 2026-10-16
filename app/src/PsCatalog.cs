using System;
using System.Collections.Generic;

namespace PSTools
{
    static class PsCatalog
    {
        static readonly Dictionary<string, string> Commands = new Dictionary<string, string>
        {
            { "attr-hide", @"$i = Get-Item -LiteralPath '{path}' -Force
$i.Attributes = $i.Attributes -bor [IO.FileAttributes]::Hidden" },
            { "attr-ro-off", @"$i = Get-Item -LiteralPath '{path}' -Force
if ($i.Attributes -band [IO.FileAttributes]::ReadOnly) { $i.Attributes = $i.Attributes -bxor [IO.FileAttributes]::ReadOnly }" },
            { "attr-ro-on", @"$i = Get-Item -LiteralPath '{path}' -Force
$i.Attributes = $i.Attributes -bor [IO.FileAttributes]::ReadOnly" },
            { "attr-unhide", @"$i = Get-Item -LiteralPath '{path}' -Force
if ($i.Attributes -band [IO.FileAttributes]::Hidden) { $i.Attributes = $i.Attributes -bxor [IO.FileAttributes]::Hidden }" },
            { "bom-add", @"# Approximate: PS Tools only touches valid UTF-8 files and keeps a backup for Undo last
$f = (Resolve-Path -LiteralPath '{path}').ProviderPath
[IO.File]::WriteAllText($f, [IO.File]::ReadAllText($f), (New-Object Text.UTF8Encoding $true))" },
            { "bom-remove", @"# Approximate: PS Tools keeps a backup for Undo last
$f = (Resolve-Path -LiteralPath '{path}').ProviderPath
[IO.File]::WriteAllText($f, [IO.File]::ReadAllText($f), (New-Object Text.UTF8Encoding $false))" },
            { "by-type", @"Get-ChildItem -LiteralPath '{path}' -Recurse -File -ErrorAction SilentlyContinue | Group-Object Extension | ForEach-Object {
    [pscustomobject]@{ Extension = $_.Name; Files = $_.Count; MB = [math]::Round(($_.Group | Measure-Object Length -Sum).Sum / 1MB, 1) }
} | Sort-Object MB -Descending" },
            { "clean-build", @"# Approximate: this only lists folders; PS Tools checks for a project file beside each one and lets you delete
Get-ChildItem -LiteralPath '{path}' -Recurse -Directory -Force -ErrorAction SilentlyContinue | Where-Object { $_.Name -in 'node_modules', '__pycache__', 'bin', 'obj', '.vs', 'target' } | Select-Object FullName" },
            { "clear-icon-cache", @"Stop-Process -Name explorer -Force
$e = ""$env:LOCALAPPDATA\Microsoft\Windows\Explorer""
Remove-Item ""$env:LOCALAPPDATA\IconCache.db"", ""$e\iconcache_*.db"", ""$e\thumbcache_*.db"" -Force -ErrorAction SilentlyContinue" },
            { "compact-off", @"compact.exe /u ('/s:' + '{path}') /a /i /exe" },
            { "compact-on", @"compact.exe /c ('/s:' + '{path}') /a /i /exe:lzx" },
            { "compare-marked", @"# Approximate: this compares file names only; PS Tools also compares sizes and content
$marked = 'C:\Path\To\MarkedFolder'
$here = (Resolve-Path -LiteralPath '{path}').ProviderPath
$a = Get-ChildItem -LiteralPath $marked -Recurse -File | ForEach-Object { $_.FullName.Substring($marked.TrimEnd('\').Length) }
$b = Get-ChildItem -LiteralPath $here -Recurse -File | ForEach-Object { $_.FullName.Substring($here.TrimEnd('\').Length) }
Compare-Object $a $b" },
            { "copy-contents", @"# Approximate: PS Tools refuses binary files and files over 1 MB
Get-Content -LiteralPath '{path}' -Raw | Set-Clipboard" },
            { "copy-csv", @"((Get-Item -LiteralPath '{path}').Name -join ', ') | Set-Clipboard" },
            { "copy-datauri", @"# Approximate: PS Tools picks the MIME type from the file extension
$b = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath '{path}').ProviderPath)
'data:application/octet-stream;base64,' + [Convert]::ToBase64String($b) | Set-Clipboard" },
            { "copy-dirs-paths", @"(Get-ChildItem -LiteralPath '{path}' -Recurse -Directory -ErrorAction SilentlyContinue).FullName -join ""`r`n"" | Set-Clipboard" },
            { "copy-dirs-rel", @"$root = (Resolve-Path -LiteralPath '{path}').ProviderPath.TrimEnd('\')
(Get-ChildItem -LiteralPath $root -Recurse -Directory -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName.Substring($root.Length + 1) }) -join ""`r`n"" | Set-Clipboard" },
            { "copy-dirs-tree", @"# Approximate: the layout of tree.com differs a little from PS Tools' tree
tree '{path}' | Set-Clipboard" },
            { "copy-json", @"ConvertTo-Json -InputObject @((Resolve-Path -LiteralPath '{path}').ProviderPath) | Set-Clipboard" },
            { "copy-md-link", @"$i = Get-Item -LiteralPath '{path}'
'[' + $i.Name + '](' + ([Uri]$i.FullName).AbsoluteUri + ')' | Set-Clipboard" },
            { "copy-md5", @"(Get-FileHash -LiteralPath '{path}' -Algorithm MD5).Hash.ToLower() | Set-Clipboard" },
            { "copy-name", @"Split-Path -Leaf -LiteralPath '{path}' | Set-Clipboard" },
            { "copy-name-noext", @"$i = Get-Item -LiteralPath '{path}'
$n = if ($i.PSIsContainer) { $i.Name } else { $i.BaseName }
$n | Set-Clipboard" },
            { "copy-names", @"(Get-ChildItem -LiteralPath '{path}' -Force | Sort-Object Name).Name -join ""`r`n"" | Set-Clipboard" },
            { "copy-parent", @"Split-Path -Parent (Resolve-Path -LiteralPath '{path}').ProviderPath | Set-Clipboard" },
            { "copy-parent-name", @"Split-Path -Leaf (Split-Path -Parent (Resolve-Path -LiteralPath '{path}').ProviderPath) | Set-Clipboard" },
            { "copy-path-fwd", @"(Resolve-Path -LiteralPath '{path}').ProviderPath.Replace('\', '/') | Set-Clipboard" },
            { "copy-path-quoted", @"'""' + (Resolve-Path -LiteralPath '{path}').ProviderPath + '""' | Set-Clipboard" },
            { "copy-pslocation", @"$p = (Resolve-Path -LiteralPath '{path}').ProviderPath
if (-not (Test-Path -LiteralPath $p -PathType Container)) { $p = Split-Path -Parent $p }
""Set-Location -LiteralPath '"" + $p.Replace(""'"", ""''"") + ""'"" | Set-Clipboard" },
            { "copy-sha1", @"(Get-FileHash -LiteralPath '{path}' -Algorithm SHA1).Hash.ToLower() | Set-Clipboard" },
            { "copy-sha256", @"(Get-FileHash -LiteralPath '{path}' -Algorithm SHA256).Hash.ToLower() | Set-Clipboard" },
            { "copy-size", @"# Approximate: PS Tools does not follow junctions or symbolic links
$i = Get-Item -LiteralPath '{path}'
if ($i.PSIsContainer) {
    $m = Get-ChildItem -LiteralPath $i.FullName -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum
    $t = '{0}: {1:N0} bytes, {2:N0} files' -f $i.Name, $m.Sum, $m.Count
} else {
    $t = '{0}: {1:N0} bytes' -f $i.Name, $i.Length
}
$t | Set-Clipboard" },
            { "copy-tree", @"# Approximate: the layout of tree.com differs a little from PS Tools' tree
tree '{path}' /F | Set-Clipboard" },
            { "copy-unc", @"$p = (Resolve-Path -LiteralPath '{path}').ProviderPath
$d = Get-PSDrive -Name $p.Substring(0, 1) -ErrorAction SilentlyContinue
$r = if ($d -and $d.DisplayRoot) { $d.DisplayRoot + $p.Substring(2) } else { $p }
$r | Set-Clipboard" },
            { "copy-url", @"([Uri](Resolve-Path -LiteralPath '{path}').ProviderPath).AbsoluteUri | Set-Clipboard" },
            { "copy-wsl", @"# Approximate: drive-letter paths only
$p = (Resolve-Path -LiteralPath '{path}').ProviderPath
'/mnt/' + $p.Substring(0, 1).ToLower() + $p.Substring(2).Replace('\', '/') | Set-Clipboard" },
            { "delete-empty", @"Get-ChildItem -LiteralPath '{path}' -Recurse -Directory | Sort-Object { $_.FullName.Length } -Descending | Where-Object { -not (Get-ChildItem -LiteralPath $_.FullName -Force) } | Remove-Item" },
            { "download-clip", @"# Approximate: PS Tools names the file from the server's response and shows progress
$u = (Get-Clipboard -Raw).Trim()
$name = [IO.Path]::GetFileName(([Uri]$u).AbsolutePath)
if (-not $name) { $name = 'download-' + (Get-Date -Format 'yyyyMMdd-HHmmss') }
Invoke-WebRequest -Uri $u -OutFile (Join-Path '{path}' $name) -UseBasicParsing" },
            { "duplicates", @"# Approximate: this only lists duplicates; PS Tools lets you tick copies and sends them to the Recycle Bin
Get-ChildItem -LiteralPath '{path}' -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Length -gt 0 } | Group-Object Length | Where-Object Count -gt 1 |
    ForEach-Object { $_.Group | Get-FileHash -Algorithm SHA256 } | Group-Object Hash | Where-Object Count -gt 1 |
    ForEach-Object { $_.Group.Path; '' }" },
            { "eol-crlf", @"# Approximate: PS Tools works on the raw bytes, skips binary files and keeps a backup for Undo last
$f = (Resolve-Path -LiteralPath '{path}').ProviderPath
$t = [IO.File]::ReadAllText($f)
[IO.File]::WriteAllText($f, $t.Replace(""`r`n"", ""`n"").Replace(""`n"", ""`r`n""), (New-Object Text.UTF8Encoding $false))" },
            { "eol-lf", @"# Approximate: PS Tools works on the raw bytes, skips binary files and keeps a backup for Undo last
$f = (Resolve-Path -LiteralPath '{path}').ProviderPath
$t = [IO.File]::ReadAllText($f)
[IO.File]::WriteAllText($f, $t.Replace(""`r`n"", ""`n""), (New-Object Text.UTF8Encoding $false))" },
            { "flatten-copy", @"# Approximate: PS Tools asks first and does not follow junctions
$src = (Resolve-Path -LiteralPath '{path}').ProviderPath.TrimEnd('\')
$dest = $src + '-flat'
New-Item -ItemType Directory -Path $dest -Force | Out-Null
Get-ChildItem -LiteralPath $src -Recurse -File | ForEach-Object {
    $name = $_.Name; $i = 1
    while (Test-Path -LiteralPath (Join-Path $dest $name)) { $name = '{0}_{1}{2}' -f $_.BaseName, $i++, $_.Extension }
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $dest $name)
}" },
            { "flatten-move", @"# Approximate: PS Tools writes an undo journal so Undo last can reverse this
$root = (Resolve-Path -LiteralPath '{path}').ProviderPath.TrimEnd('\')
@(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object { $_.DirectoryName -ne $root }) | ForEach-Object {
    $name = $_.Name; $i = 1
    while (Test-Path -LiteralPath (Join-Path $root $name)) { $name = '{0}_{1}{2}' -f $_.BaseName, $i++, $_.Extension }
    Move-Item -LiteralPath $_.FullName -Destination (Join-Path $root $name)
}
Get-ChildItem -LiteralPath $root -Recurse -Directory | Sort-Object { $_.FullName.Length } -Descending | Where-Object { -not (Get-ChildItem -LiteralPath $_.FullName -Force) } | Remove-Item" },
            { "folder-sizes", @"Get-ChildItem -LiteralPath '{path}' -Directory | ForEach-Object {
    $s = (Get-ChildItem -LiteralPath $_.FullName -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
    [pscustomobject]@{ Folder = $_.Name; MB = [math]::Round($s / 1MB, 1) }
} | Sort-Object MB -Descending" },
            { "force-delete", @"# Deletes permanently and skips the Recycle Bin. PS Tools also refuses protected locations
$p = '\\?\' + [IO.Path]::GetFullPath('{path}')
if ([IO.Directory]::Exists($p)) { cmd /c rd /s /q ""$p"" } else { cmd /c del /f /q ""$p"" }" },
            { "git-pull", @"Get-ChildItem -LiteralPath '{path}' -Recurse -Directory -Force -Filter .git -ErrorAction SilentlyContinue | ForEach-Object {
    $r = $_.Parent.FullName
    ""== $r""
    git -C $r pull --ff-only
}" },
            { "git-status", @"Get-ChildItem -LiteralPath '{path}' -Recurse -Directory -Force -Filter .git -ErrorAction SilentlyContinue | ForEach-Object {
    $r = $_.Parent.FullName
    ""== $r""
    git -C $r status -sb
}" },
            { "group-selected", @"# Approximate: PS Tools asks for the folder name, handles several items and can undo
$parent = Split-Path -Parent (Resolve-Path -LiteralPath '{path}').ProviderPath
$dest = Join-Path $parent 'New folder'
New-Item -ItemType Directory -Path $dest | Out-Null
Move-Item -LiteralPath '{path}' -Destination $dest" },
            { "img-jpg", @"# Approximate: PS Tools uses quality 90 and turns transparency white
Add-Type -AssemblyName System.Drawing
$f = (Resolve-Path -LiteralPath '{path}').ProviderPath
$img = [Drawing.Image]::FromFile($f)
try { $img.Save([IO.Path]::ChangeExtension($f, '.jpg'), [Drawing.Imaging.ImageFormat]::Jpeg) } finally { $img.Dispose() }" },
            { "img-png", @"# Approximate: PS Tools also upright-rotates the picture and never overwrites an existing file
Add-Type -AssemblyName System.Drawing
$f = (Resolve-Path -LiteralPath '{path}').ProviderPath
$img = [Drawing.Image]::FromFile($f)
try { $img.Save([IO.Path]::ChangeExtension($f, '.png'), [Drawing.Imaging.ImageFormat]::Png) } finally { $img.Dispose() }" },
            { "img-resize-1600", @"# Approximate: PS Tools keeps the format, skips pictures that are already small and never overwrites
Add-Type -AssemblyName System.Drawing
$max = 1600
$f = (Resolve-Path -LiteralPath '{path}').ProviderPath
$src = [Drawing.Image]::FromFile($f)
try {
    $k = [Math]::Min(1, $max / [Math]::Max($src.Width, $src.Height))
    $w = [int]($src.Width * $k); $h = [int]($src.Height * $k)
    $bmp = New-Object Drawing.Bitmap $w, $h
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.DrawImage($src, 0, 0, $w, $h)
    $g.Dispose()
    $out = Join-Path (Split-Path -Parent $f) ([IO.Path]::GetFileNameWithoutExtension($f) + '-' + $max + [IO.Path]::GetExtension($f))
    $bmp.Save($out, $src.RawFormat)
    $bmp.Dispose()
} finally { $src.Dispose() }" },
            { "img-resize-800", @"# Approximate: PS Tools keeps the format, skips pictures that are already small and never overwrites
Add-Type -AssemblyName System.Drawing
$max = 800
$f = (Resolve-Path -LiteralPath '{path}').ProviderPath
$src = [Drawing.Image]::FromFile($f)
try {
    $k = [Math]::Min(1, $max / [Math]::Max($src.Width, $src.Height))
    $w = [int]($src.Width * $k); $h = [int]($src.Height * $k)
    $bmp = New-Object Drawing.Bitmap $w, $h
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.DrawImage($src, 0, 0, $w, $h)
    $g.Dispose()
    $out = Join-Path (Split-Path -Parent $f) ([IO.Path]::GetFileNameWithoutExtension($f) + '-' + $max + [IO.Path]::GetExtension($f))
    $bmp.Save($out, $src.RawFormat)
    $bmp.Dispose()
} finally { $src.Dispose() }" },
            { "img-strip", @"# Approximate: PS Tools also rotates the picture upright first
Add-Type -AssemblyName System.Drawing
$f = (Resolve-Path -LiteralPath '{path}').ProviderPath
$src = [Drawing.Image]::FromFile($f)
try {
    $bmp = New-Object Drawing.Bitmap $src.Width, $src.Height
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.DrawImage($src, 0, 0, $src.Width, $src.Height)
    $g.Dispose()
    $out = Join-Path (Split-Path -Parent $f) ([IO.Path]::GetFileNameWithoutExtension($f) + '-clean' + [IO.Path]::GetExtension($f))
    $bmp.Save($out, $src.RawFormat)
    $bmp.Dispose()
} finally { $src.Dispose() }" },
            { "inventory", @"Get-ChildItem -LiteralPath '{path}' -Recurse -File | Select-Object FullName, Length, CreationTime, LastWriteTime | Export-Csv -LiteralPath (Join-Path '{path}' 'inventory.csv') -NoTypeInformation -Encoding UTF8" },
            { "largest", @"Get-ChildItem -LiteralPath '{path}' -Recurse -File -ErrorAction SilentlyContinue | Sort-Object Length -Descending | Select-Object -First 200 FullName, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } }" },
            { "link-hard", @"# Approximate: PS Tools reads the marked items for you and can undo. Set $marked to the item to link to
$marked = 'C:\Path\To\MarkedFile'
New-Item -ItemType HardLink -Path (Join-Path '{path}' (Split-Path -Leaf $marked)) -Target $marked" },
            { "link-junction", @"# Approximate: PS Tools reads the marked items for you and can undo. Set $marked to the item to link to
$marked = 'C:\Path\To\MarkedFolder'
New-Item -ItemType Junction -Path (Join-Path '{path}' (Split-Path -Leaf $marked)) -Target $marked" },
            { "link-symlink", @"# Approximate: PS Tools reads the marked items for you and can undo. Set $marked to the item to link to
$marked = 'C:\Path\To\MarkedItem'
New-Item -ItemType SymbolicLink -Path (Join-Path '{path}' (Split-Path -Leaf $marked)) -Target $marked" },
            { "long-paths", @"Get-ChildItem -LiteralPath '{path}' -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.FullName.Length -gt 240 } | Select-Object @{ n = 'Length'; e = { $_.FullName.Length } }, FullName" },
            { "mark", @"# Approximate: PS Tools remembers the marked items between runs; here $marked lasts for this session
$marked = (Resolve-Path -LiteralPath '{path}').ProviderPath" },
            { "new-dated-folder", @"# Approximate: PS Tools adds _1, _2 when the folder already exists
New-Item -ItemType Directory -Path (Join-Path '{path}' (Get-Date -Format 'yyyy-MM-dd')) | Out-Null" },
            { "new-folder-clip", @"$n = (Get-Clipboard | Select-Object -First 1) -replace '[\\/:*?""<>|]', '_'
New-Item -ItemType Directory -Path (Join-Path '{path}' $n.Trim()) | Out-Null" },
            { "open-cmd", @"$d = '{path}'
if (-not (Test-Path -LiteralPath $d -PathType Container)) { $d = Split-Path -Parent $d }
Start-Process cmd.exe -WorkingDirectory $d" },
            { "open-notepad", @"notepad.exe '{path}'" },
            { "open-ps", @"$d = '{path}'
if (-not (Test-Path -LiteralPath $d -PathType Container)) { $d = Split-Path -Parent $d }
Start-Process powershell.exe -WorkingDirectory $d" },
            { "open-ps-admin", @"$d = '{path}'
if (-not (Test-Path -LiteralPath $d -PathType Container)) { $d = Split-Path -Parent $d }
Start-Process powershell.exe -Verb RunAs -WorkingDirectory $d" },
            { "open-vscode", @"code '{path}'" },
            { "open-wsl", @"wsl.exe --cd '{path}'" },
            { "organize-ext", @"# Approximate: PS Tools renames clashing names to name_1 and can undo
$root = '{path}'
Get-ChildItem -LiteralPath $root -File | ForEach-Object {
    $e = $_.Extension.TrimStart('.').ToUpper()
    if (-not $e) { $e = 'NOEXT' }
    $d = Join-Path $root $e
    New-Item -ItemType Directory -Path $d -Force | Out-Null
    Move-Item -LiteralPath $_.FullName -Destination $d
}" },
            { "organize-month", @"# Approximate: PS Tools renames clashing names to name_1 and can undo
$root = '{path}'
Get-ChildItem -LiteralPath $root -File | ForEach-Object {
    $d = Join-Path $root $_.LastWriteTime.ToString('yyyy-MM')
    New-Item -ItemType Directory -Path $d -Force | Out-Null
    Move-Item -LiteralPath $_.FullName -Destination $d
}" },
            { "organize-type", @"# Approximate: PS Tools knows a longer list of extensions and can undo
$root = '{path}'
$kinds = @{
    Images = '.jpg .jpeg .png .gif .webp .bmp .tif .tiff .heic .svg .ico'
    Videos = '.mp4 .mkv .mov .avi .wmv .webm'
    Audio = '.mp3 .wav .flac .m4a .ogg'
    Documents = '.pdf .doc .docx .xls .xlsx .ppt .pptx .txt .rtf .csv .md'
    Archives = '.zip .rar .7z .tar .gz'
    Code = '.js .ts .html .css .json .py .cs .ps1 .xml .yml'
}
Get-ChildItem -LiteralPath $root -File | ForEach-Object {
    $ext = $_.Extension.ToLower(); $kind = 'Other'
    foreach ($k in $kinds.Keys) { if ($kinds[$k].Split(' ') -contains $ext) { $kind = $k } }
    $dest = Join-Path $root $kind
    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    Move-Item -LiteralPath $_.FullName -Destination $dest
}" },
            { "paste-image", @"# Needs an STA session: start it with powershell.exe -STA
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
$img = [Windows.Forms.Clipboard]::GetImage()
if ($img) {
    $img.Save((Join-Path '{path}' ('clip-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.png')), [Drawing.Imaging.ImageFormat]::Png)
} else {
    'The clipboard does not contain an image.'
}" },
            { "paste-text", @"# Approximate: PS Tools picks .json or .html when the text looks like one
$t = Get-Clipboard -Raw
if ($t -and $t.Trim()) {
    [IO.File]::WriteAllText((Join-Path '{path}' ('clip-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.txt')), $t, (New-Object Text.UTF8Encoding $false))
} else {
    'The clipboard does not contain text.'
}" },
            { "recent", @"Get-ChildItem -LiteralPath '{path}' -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -gt (Get-Date).AddDays(-7) } | Sort-Object LastWriteTime -Descending | Select-Object LastWriteTime, FullName" },
            { "rename", @"# Approximate: PS Tools offers find and replace, prefix, numbering and case with a live preview. This example replaces spaces with underscores
Get-ChildItem -LiteralPath '{path}' -File | Rename-Item -NewName { $_.Name -replace ' ', '_' }" },
            { "restart-explorer", @"# Windows starts Explorer again by itself after it is stopped
Stop-Process -Name explorer -Force" },
            { "search-here", @"# Approximate: PS Tools also searches inside text files and skips .git, node_modules, bin and obj
$q = 'report'
Get-ChildItem -LiteralPath '{path}' -Recurse -Force -ErrorAction SilentlyContinue | Where-Object { $_.Name -like ""*$q*"" } | Select-Object FullName" },
            { "serve-here", @"# Approximate: no MIME types are sent, so some module and WASM pages need PS Tools' server
$root = (Resolve-Path -LiteralPath '{path}').ProviderPath.TrimEnd('\')
$l = New-Object Net.HttpListener
$l.Prefixes.Add('http://localhost:8080/')
$l.Start()
Write-Host 'Serving on http://localhost:8080/  (Ctrl+C to stop)'
try {
    while ($l.IsListening) {
        $c = $l.GetContext()
        $rel = [Uri]::UnescapeDataString($c.Request.Url.AbsolutePath.TrimStart('/'))
        $f = [IO.Path]::GetFullPath((Join-Path $root $rel))
        if ([IO.Directory]::Exists($f)) { $f = Join-Path $f 'index.html' }
        if ($f.StartsWith($root) -and [IO.File]::Exists($f)) {
            $b = [IO.File]::ReadAllBytes($f)
            $c.Response.OutputStream.Write($b, 0, $b.Length)
        } else { $c.Response.StatusCode = 404 }
        $c.Response.Close()
    }
} finally { $l.Stop() }" },
            { "snapshot", @"$s = (Resolve-Path -LiteralPath '{path}').ProviderPath.TrimEnd('\')
Copy-Item -LiteralPath $s -Destination ($s + '_' + (Get-Date -Format 'yyyy-MM-dd')) -Recurse" },
            { "take-ownership", @"# Run in an elevated PowerShell. For a single file, drop /r /d y and /t /c and use :F
takeown /f '{path}' /r /d y
icacls '{path}' /grant ""${env:USERNAME}:(OI)(CI)F"" /t /c" },
            { "toggle-classic", @"# Approximate: PS Tools also remembers the change so Full uninstall can restore it
$k = 'HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}'
if (Test-Path -LiteralPath ""$k\InprocServer32"") { Remove-Item -LiteralPath $k -Recurse } else { New-Item -Path ""$k\InprocServer32"" -Force -Value '' | Out-Null }" },
            { "toggle-ext", @"# Approximate: PS Tools also remembers the old value so Full uninstall can restore it
$k = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'
$v = (Get-ItemProperty $k).HideFileExt
Set-ItemProperty $k HideFileExt $(if ($v -eq 1) { 0 } else { 1 })" },
            { "toggle-hidden", @"# Approximate: PS Tools also remembers the old value so Full uninstall can restore it
$k = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'
$v = (Get-ItemProperty $k).Hidden
Set-ItemProperty $k Hidden $(if ($v -eq 1) { 2 } else { 1 })" },
            { "touch", @"$now = Get-Date
$i = Get-Item -LiteralPath '{path}' -Force
$i.CreationTime = $now; $i.LastWriteTime = $now; $i.LastAccessTime = $now" },
            { "unblock", @"Get-ChildItem -LiteralPath '{path}' -Recurse -File | Unblock-File" },
            { "unblock-files", @"Unblock-File -LiteralPath '{path}'" },
            { "unwrap", @"# Approximate: PS Tools renames clashing names to name_1 and can undo
$d = (Resolve-Path -LiteralPath '{path}').ProviderPath.TrimEnd('\')
$parent = Split-Path -Parent $d
$tmpName = '~pst' + [guid]::NewGuid().ToString('N').Substring(0, 8)
Rename-Item -LiteralPath $d -NewName $tmpName
$tmp = Join-Path $parent $tmpName
Get-ChildItem -LiteralPath $tmp -Force | Move-Item -Destination $parent
Remove-Item -LiteralPath $tmp" },
            { "unzip-here", @"# Approximate: PS Tools skips unsafe entries and avoids a folder inside a folder
$z = (Resolve-Path -LiteralPath '{path}').ProviderPath
Expand-Archive -LiteralPath $z -DestinationPath (Join-Path (Split-Path -Parent $z) ([IO.Path]::GetFileNameWithoutExtension($z)))" },
            { "verify-hash", @"$want = ((Get-Clipboard -Raw) -replace '[^0-9a-fA-F]', '').ToLower()
$alg = switch ($want.Length) { 32 { 'MD5' } 40 { 'SHA1' } 64 { 'SHA256' } 128 { 'SHA512' } default { throw 'The clipboard does not contain a checksum.' } }
$got = (Get-FileHash -LiteralPath '{path}' -Algorithm $alg).Hash.ToLower()
if ($got -eq $want) { ""MATCH ($alg)"" } else { ""DIFFERENT ($alg)`nExpected: $want`nActual:   $got"" }" },
            { "zip-smart", @"# Approximate: PS Tools skips .git, node_modules, bin, obj and similar folders
$p = (Resolve-Path -LiteralPath '{path}').ProviderPath.TrimEnd('\')
Compress-Archive -LiteralPath $p -DestinationPath ($p + '.zip')" },
        };

        static readonly Dictionary<string, string> Reasons = new Dictionary<string, string>
        {
            { "library", @"It opens the command library window, whose snippets are already PowerShell." },
            { "space-map", @"It draws an interactive map of tiles. Subfolder sizes gives the same numbers as a table." },
            { "structure", @"Opens a window with format, depth and preview options. The Copy tree actions (and tree.com in PowerShell) cover single formats." },
            { "undo-history", @"It reads and reverses the PS Tools undo journal, which PowerShell does not have." },
            { "undo-last", @"It uses the PS Tools undo journal, which PowerShell does not have." },
        };

        static readonly HashSet<string> Undo = new HashSet<string>
        {
            "paste-image",
            "paste-text",
            "new-folder-clip",
            "new-dated-folder",
            "group-selected",
            "unwrap",
            "download-clip",
            "zip-smart",
            "unzip-here",
            "img-png",
            "img-jpg",
            "img-resize-1600",
            "img-resize-800",
            "img-strip",
            "flatten-move",
            "organize-ext",
            "organize-month",
            "organize-type",
            "rename",
            "delete-empty",
            "touch",
            "attr-hide",
            "attr-unhide",
            "attr-ro-on",
            "attr-ro-off",
            "eol-lf",
            "eol-crlf",
            "bom-add",
            "bom-remove",
            "link-junction",
            "link-symlink",
            "link-hard",
        };

        public static IEnumerable<string> CommandIds { get { return Commands.Keys; } }
        public static IEnumerable<string> ReasonIds { get { return Reasons.Keys; } }
        public static IEnumerable<string> UndoIds { get { return Undo; } }

        public static void Apply(IEnumerable<Action> actions)
        {
            foreach (var a in actions)
            {
                string text;
                if (string.IsNullOrEmpty(a.Ps) && Commands.TryGetValue(a.Id, out text)) a.Ps = text.Replace("\r\n", "\n");
                if (string.IsNullOrEmpty(a.PsNone) && Reasons.TryGetValue(a.Id, out text)) a.PsNone = text;
                if (Undo.Contains(a.Id)) a.Undoable = true;
            }
        }
    }
}
