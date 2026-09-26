# Draws assets\metamove.ico (double M: white 'Meta', blue 'Move'), 16-256 px, PNG frames.
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot '..\assets\metamove.ico'
$sizes = 256, 64, 48, 32, 16
$pngs = foreach ($n in $sizes) {
  $bmp = New-Object Drawing.Bitmap $n, $n
  $g = [Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'; $g.Clear([Drawing.Color]::Transparent)
  $k = $n / 32.0
  $r = 9 * $k; $path = New-Object Drawing.Drawing2D.GraphicsPath
  $path.AddArc(0, 0, 2*$r, 2*$r, 180, 90); $path.AddArc($n-2*$r-1, 0, 2*$r, 2*$r, 270, 90)
  $path.AddArc($n-2*$r-1, $n-2*$r-1, 2*$r, 2*$r, 0, 90); $path.AddArc(0, $n-2*$r-1, 2*$r, 2*$r, 90, 90); $path.CloseFigure()
  $g.FillPath((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 33, 36, 44))), $path)
  function Stroke($x0, $rgb) {
    $pen = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(255, $rgb[0], $rgb[1], $rgb[2])), (2.4 * $k)
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
    $xs = $x0, $x0, ($x0 + 5.25), ($x0 + 10.5), ($x0 + 10.5); $ys = 22.5, 10, 17, 10, 22.5
    $pts = [Drawing.PointF[]](0..4 | ForEach-Object { New-Object Drawing.PointF ($xs[$_] * $k), ($ys[$_] * $k) })
    $g.DrawLines($pen, $pts)
  }
  Stroke 5.5 @(246, 244, 239)
  Stroke 16 @(51, 136, 255)
  $ms = New-Object IO.MemoryStream; $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()
  ,$ms.ToArray()
}
$fs = [IO.File]::Create($out); $w = New-Object IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
  $n = $sizes[$i]; $b = if ($n -ge 256) { 0 } else { $n }
  $w.Write([byte]$b); $w.Write([byte]$b); $w.Write([byte]0); $w.Write([byte]0)
  $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
  $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Close()
'ico ok'
