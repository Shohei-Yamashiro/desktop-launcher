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
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $bgRect, ([System.Drawing.Color]::FromArgb(255, 47, 128, 237)), ([System.Drawing.Color]::FromArgb(255, 26, 86, 196)), 90
    $g.FillPath($brush, $bg)

    $tile = 4.6 * $s
    $gap = 1.6 * $s
    $origin = ($size - ($tile * 2 + $gap)) / 2
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $accent = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 196, 61))
    for ($row = 0; $row -lt 2; $row++) {
        for ($col = 0; $col -lt 2; $col++) {
            $x = $origin + $col * ($tile + $gap)
            $y = $origin + $row * ($tile + $gap)
            $tilePath = New-RoundedRect $x $y $tile $tile ([Math]::Max(1.0, 1.2 * $s))
            $g.FillPath($(if ($row -eq 0 -and $col -eq 1) { $accent } else { $white }), $tilePath)
            $tilePath.Dispose()
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
