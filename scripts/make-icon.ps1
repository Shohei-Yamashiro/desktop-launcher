# Generates src/DesktopLauncher/Assets/app.ico (multi-size, PNG-compressed entries).
# The logo is a hash: two pale grey stems under two tag-colored bars, each bar with a deep dot at its left end
# like the tag chips on the rows. Below 24 px the dots are left out and strokes snap to whole pixels.
param(
    [string]$OutFile = (Join-Path $PSScriptRoot "..\src\DesktopLauncher\Assets\app.ico")
)

Add-Type -AssemblyName System.Drawing

function C([int]$r, [int]$g, [int]$b, [int]$a = 255) { [System.Drawing.Color]::FromArgb($a, $r, $g, $b) }

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = [Math]::Max(0.01, [Math]::Min($r, [Math]::Min($w, $h) / 2))
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function Fill($g, $color, $path) {
    $brush = New-Object System.Drawing.SolidBrush $color
    $g.FillPath($brush, $path)
    $brush.Dispose(); $path.Dispose()
}

function New-IconPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = $size / 16.0
    $bgRect = New-Object System.Drawing.RectangleF 0, 0, $size, $size
    $bg = New-RoundedRect 0 0 $size $size (3.5 * $s)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $bgRect, (C 74 77 84), (C 36 38 42), 90
    $g.FillPath($brush, $bg)
    $brush.Dispose(); $bg.Dispose()
    if ($size -ge 24) {
        $penWidth = [Math]::Max(1.0, 0.18 * $s)
        $inset = $penWidth / 2
        $outline = New-RoundedRect $inset $inset ($size - 2 * $inset) ($size - 2 * $inset) (3.5 * $s - $inset)
        $pen = New-Object System.Drawing.Pen (C 160 165 173 130), $penWidth
        $g.DrawPath($pen, $outline)
        $pen.Dispose(); $outline.Dispose()
    }

    # One stroke width for all four strokes; stems and bars sit on the thirds of an 11.2-unit square.
    $snap = $size -lt 32
    $t = 2.3 * $s
    $left = 2.4 * $s; $right = 13.6 * $s; $top = 2.4 * $s; $bottom = 13.6 * $s
    $y1 = 6.0 * $s; $y2 = 10.0 * $s
    if ($snap) {
        $t = [Math]::Max(2, [Math]::Round($t))
        $left = [Math]::Round($left); $right = [Math]::Round($right); $top = [Math]::Round($top); $bottom = [Math]::Round($bottom)
        $y1 = [Math]::Round($y1 - $t / 2) + $t / 2; $y2 = [Math]::Round($y2 - $t / 2) + $t / 2
    }

    $stem = New-Object System.Drawing.Pen (C 226 228 232), $t
    $stem.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $stem.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $slant = 0.6 * $s
    foreach ($x in @((6.0 * $s), (10.0 * $s))) {
        $g.DrawLine($stem, $x + $slant, $top + $t / 2, $x - $slant, $bottom - $t / 2)
    }
    $stem.Dispose()

    $bars = @(
        @{ Y = $y1; Fill = (C 168 204 250); Dot = (C 38 92 178) },
        @{ Y = $y2; Fill = (C 166 222 186); Dot = (C 30 122 72) })
    foreach ($bar in $bars) {
        Fill $g $bar.Fill (New-RoundedRect $left ($bar.Y - $t / 2) ($right - $left) $t ($t / 2))
        if ($size -ge 24) {
            $r = 0.3 * $t
            $dot = New-Object System.Drawing.SolidBrush $bar.Dot
            $g.FillEllipse($dot, $left + $t / 2 - $r, $bar.Y - $r, 2 * $r, 2 * $r)
            $dot.Dispose()
        }
    }

    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$images = foreach ($size in $sizes) { , (New-IconPng $size) }

New-Item -ItemType Directory -Force (Split-Path $OutFile) | Out-Null
$fs = [System.IO.File]::Create($OutFile)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$images[$i].Length); $w.Write([UInt32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Dispose()
Write-Host "Wrote $OutFile"
