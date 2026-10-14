# Generates src/DesktopLauncher/Assets/app.ico (multi-size, PNG-compressed entries).
param(
    [string]$OutFile = (Join-Path $PSScriptRoot "..\src\DesktopLauncher\Assets\app.ico")
)

Add-Type -AssemblyName System.Drawing

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = $size / 16.0
    $bgRect = New-Object System.Drawing.RectangleF 0, 0, $size, $size
    $bg = New-RoundedRect 0 0 $size $size (3.5 * $s)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $bgRect, ([System.Drawing.Color]::FromArgb(255, 74, 77, 84)), ([System.Drawing.Color]::FromArgb(255, 36, 38, 42)), 90
    $g.FillPath($brush, $bg)
    if ($size -ge 24) {
        $penWidth = [Math]::Max(1.0, 0.18 * $s)
        $inset = $penWidth / 2
        $outline = New-RoundedRect $inset $inset ($size - 2 * $inset) ($size - 2 * $inset) (3.5 * $s - $inset)
        $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(130, 160, 165, 173)), $penWidth
        $g.DrawPath($pen, $outline)
        $pen.Dispose(); $outline.Dispose()
    }

    # Three list rows; the middle one is the selected row. Small sizes snap to whole pixels to stay crisp.
    $band = 2.8 * $s
    $gap = 1.3 * $s
    $width = 10.6 * $s
    if ($size -lt 32) { $band = [Math]::Round($band); $gap = [Math]::Max(1, [Math]::Round($gap)); $width = [Math]::Round($width) }
    $left = [Math]::Round(($size - $width) / 2)
    $top = [Math]::Round(($size - ($band * 3 + $gap * 2)) / 2)
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 232, 233, 235))
    $accent = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 196, 61))
    $slot = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(150, 36, 38, 42))
    for ($row = 0; $row -lt 3; $row++) {
        $y = $top + $row * ($band + $gap)
        $radius = [Math]::Max(0.5, [Math]::Min($band / 2, 0.9 * $s))
        $bandPath = New-RoundedRect $left $y $width $band $radius
        $g.FillPath($(if ($row -eq 1) { $accent } else { $white }), $bandPath)
        $bandPath.Dispose()

        if ($size -ge 32) {
            # The item's icon at the start of each row.
            $inset = 0.55 * $s
            $square = $band - 2 * $inset
            $slotPath = New-RoundedRect ($left + $inset) ($y + $inset) $square $square ([Math]::Max(0.5, 0.45 * $s))
            $g.FillPath($slot, $slotPath)
            $slotPath.Dispose()
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
