# Draws a simple 32x32 + 16x16 icon (blue tile with ">_") and writes app.ico. Run once by build.cmd.
Add-Type -AssemblyName System.Drawing
function Draw($s) {
    $b = New-Object Drawing.Bitmap $s, $s
    $g = [Drawing.Graphics]::FromImage($b)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear([Drawing.Color]::Transparent)
    $g.FillRectangle((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(0, 103, 192))), 1, 1, $s - 2, $s - 2)
    $f = New-Object Drawing.Font 'Consolas', ($s * 0.42), ([Drawing.FontStyle]::Bold), ([Drawing.GraphicsUnit]::Pixel)
    $g.DrawString('>_', $f, [Drawing.Brushes]::White, ($s * 0.06), ($s * 0.22))
    $g.Dispose(); $b
}
$imgs = 16, 32, 48 | ForEach-Object {
    $ms = New-Object IO.MemoryStream; (Draw $_).Save($ms, [Drawing.Imaging.ImageFormat]::Png); , @($_, $ms.ToArray())
}
$out = New-Object IO.MemoryStream; $w = New-Object IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$imgs.Count)
$offset = 6 + 16 * $imgs.Count
foreach ($i in $imgs) {
    $w.Write([byte]$i[0]); $w.Write([byte]$i[0]); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$i[1].Length); $w.Write([uint32]$offset)
    $offset += $i[1].Length
}
foreach ($i in $imgs) { $w.Write([byte[]]$i[1]) }
[IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'app.ico'), $out.ToArray())
