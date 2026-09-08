# Draws the tray and application icon: a ring gauge, three quarters filled.
# A ring survives 16 px in a way a letter or a wordmark does not, and it says what the app is
# about. Written as a script so the icon is reproducible rather than a binary nobody can regenerate.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$out = 'C:\Users\Trevor\01_dev\bingo-hud\src\BingoHud.App\Assets\bingo.ico'
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null

# The tray asks for 16 or 24, the taskbar and alt-tab for 32 or 48, Explorer for 256.
# 64 and 128 are stored uncompressed and cost tens of kilobytes to say nothing new.
$sizes = @(16, 24, 32, 48, 256)
$bitmaps = @()

foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    # Stroke is a fraction of the size so every rendition has the same weight.
    $stroke = [Math]::Max(2.0, $size * 0.16)
    $pad = $stroke * 0.85 + $size * 0.05
    $box = New-Object System.Drawing.RectangleF $pad, $pad, ($size - 2 * $pad), ($size - 2 * $pad)

    # A dark halo under everything. The Windows 11 taskbar can be light or dark, and a white
    # ring on a light taskbar is an invisible icon. The halo costs nothing on a dark taskbar,
    # where it disappears into the background, and is what makes the icon legible on a light one.
    $halo = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(190, 0, 0, 0)), ($stroke * 1.7)
    $halo.StartCap = 'Round'; $halo.EndCap = 'Round'
    $g.DrawArc($halo, $box, -90, 360)

    # The unused remainder, dim, so the ring reads as a gauge rather than a broken circle.
    $track = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(80, 255, 255, 255)), $stroke
    $track.StartCap = 'Round'; $track.EndCap = 'Round'
    $g.DrawArc($track, $box, -90, 360)

    # Three quarters consumed, starting at twelve o'clock and going clockwise.
    $used = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 255, 255, 255)), $stroke
    $used.StartCap = 'Round'; $used.EndCap = 'Round'
    $g.DrawArc($used, $box, -90, 270)

    $halo.Dispose(); $track.Dispose(); $used.Dispose(); $g.Dispose()
    $bitmaps += $bmp
}

# ICO container written by hand.
#
# Frames up to 128 px are stored as uncompressed DIBs rather than PNGs. PNG-in-ICO is legal and
# WPF reads it, but System.Drawing does not decode it reliably, and System.Drawing.Icon is what
# the tray needs — a PNG-only icon renders in the notification area as noise. 256 px stays PNG,
# which is the convention for that size and is never asked for by the tray.
function Get-Dib {
    param([System.Drawing.Bitmap]$Bitmap)

    $w = $Bitmap.Width
    $h = $Bitmap.Height
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms

    # BITMAPINFOHEADER. The height is doubled because the structure describes the colour bitmap
    # and the mask that follows it as one image.
    $bw.Write([UInt32]40)
    $bw.Write([Int32]$w)
    $bw.Write([Int32]($h * 2))
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]0)        # BI_RGB
    $bw.Write([UInt32]($w * $h * 4))
    0..3 | ForEach-Object { $bw.Write([UInt32]0) }

    # Colour data, bottom-up, BGRA.
    $rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
    $data = $Bitmap.LockBits($rect, 'ReadOnly', ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb))
    $bytes = New-Object byte[] ($data.Stride * $h)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
    $Bitmap.UnlockBits($data)

    for ($y = $h - 1; $y -ge 0; $y--) {
        $bw.Write($bytes, $y * $data.Stride, $w * 4)
    }

    # The AND mask. Left all zero: with 32 bits per pixel the alpha channel is what decides
    # transparency, and a mask that disagreed with it would punch holes in the icon.
    $maskRow = [Math]::Floor((($w + 31) / 32)) * 4
    $blank = New-Object byte[] ($maskRow * $h)
    $bw.Write($blank)

    $bw.Flush()
    $out = $ms.ToArray()
    $bw.Dispose(); $ms.Dispose()
    return ,$out
}

$payloads = @()
$isPng = @()
foreach ($bmp in $bitmaps) {
    if ($bmp.Width -ge 256) {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $payloads += ,$ms.ToArray()
        $ms.Dispose()
        $isPng += $true
    }
    else {
        $payloads += ,(Get-Dib -Bitmap $bmp)
        $isPng += $false
    }
}

$stream = [System.IO.File]::Create($out)
$w = New-Object System.IO.BinaryWriter $stream

$w.Write([UInt16]0)              # reserved
$w.Write([UInt16]1)              # type: icon
$w.Write([UInt16]$bitmaps.Count)

$offset = 6 + 16 * $bitmaps.Count
for ($i = 0; $i -lt $bitmaps.Count; $i++) {
    $side = $sizes[$i]
    $w.Write([Byte]$(if ($side -ge 256) { 0 } else { $side }))   # width, 0 meaning 256
    $w.Write([Byte]$(if ($side -ge 256) { 0 } else { $side }))   # height
    $w.Write([Byte]0)            # palette colours
    $w.Write([Byte]0)            # reserved
    $w.Write([UInt16]1)          # colour planes
    $w.Write([UInt16]32)         # bits per pixel
    $w.Write([UInt32]$payloads[$i].Length)
    $w.Write([UInt32]$offset)
    $offset += $payloads[$i].Length
}

foreach ($payload in $payloads) { $w.Write($payload) }

$w.Flush(); $w.Dispose(); $stream.Dispose()
foreach ($bmp in $bitmaps) { $bmp.Dispose() }

"wrote $out ($((Get-Item $out).Length) bytes)"

# A look at the small renditions on both taskbar colours, scaled up so 16 px can be judged.
$preview = New-Object System.Drawing.Bitmap 360, 120
$pg = [System.Drawing.Graphics]::FromImage($preview)
$pg.InterpolationMode = 'NearestNeighbor'
$pg.FillRectangle([System.Drawing.Brushes]::Black, 0, 0, 360, 60)
$pg.FillRectangle([System.Drawing.Brushes]::White, 0, 60, 360, 60)
$x = 10
foreach ($side in @(16, 24, 32, 48)) {
    $ico = New-Object System.Drawing.Icon $out, $side, $side
    $bmp = $ico.ToBitmap()
    $pg.DrawImage($bmp, $x, 6, $side * 2, $side * 2)
    $pg.DrawImage($bmp, $x, 66, $side * 2, $side * 2)
    $x += $side * 2 + 16
    $bmp.Dispose(); $ico.Dispose()
}
$pg.Dispose()
$previewPath = Join-Path $PSScriptRoot 'icon-preview.png'
$preview.Save($previewPath, [System.Drawing.Imaging.ImageFormat]::Png)
$preview.Dispose()
"preview $previewPath"
