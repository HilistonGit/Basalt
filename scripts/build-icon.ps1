Add-Type -AssemblyName System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$frames=@()
foreach($size in @(16,24,32,48,64,128,256)) {
 $bmp=New-Object Drawing.Bitmap($size,$size); $g=[Drawing.Graphics]::FromImage($bmp); $g.SmoothingMode='AntiAlias'; $g.ScaleTransform($size/64.0,$size/64.0); $g.Clear([Drawing.Color]::Transparent)
 $points=[Drawing.PointF[]]@((New-Object Drawing.PointF(32,2)),(New-Object Drawing.PointF(58,17)),(New-Object Drawing.PointF(58,47)),(New-Object Drawing.PointF(32,62)),(New-Object Drawing.PointF(6,47)),(New-Object Drawing.PointF(6,17)))
 $brush=New-Object Drawing.Drawing2D.LinearGradientBrush((New-Object Drawing.Point(6,2)),(New-Object Drawing.Point(58,62)),[Drawing.Color]::FromArgb(173,151,255),[Drawing.Color]::FromArgb(91,62,218))
 $g.FillPolygon($brush,$points); $pen=New-Object Drawing.Pen([Drawing.Color]::White,4.5); $pen.StartCap='Round'; $pen.EndCap='Round'; $pen.LineJoin='Round'
 $g.DrawLine($pen,24,18,24,46); $g.DrawLine($pen,24,18,33,18); $g.DrawBezier($pen,33,18,47,18,47,32,33,32); $g.DrawLine($pen,24,32,33,32); $g.DrawBezier($pen,33,32,49,32,49,46,33,46); $g.DrawLine($pen,24,46,33,46)
 $stream=New-Object IO.MemoryStream; $bmp.Save($stream,[Drawing.Imaging.ImageFormat]::Png); $frames+=,@{Size=$size;Bytes=$stream.ToArray()}; $stream.Dispose(); $pen.Dispose(); $brush.Dispose(); $g.Dispose(); $bmp.Dispose()
}
$out=[IO.File]::Create((Join-Path $root 'assets/Basalt.ico')); $writer=New-Object IO.BinaryWriter($out); $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count); $offset=6+16*$frames.Count
foreach($frame in $frames) { $n=if($frame.Size -eq 256){0}else{$frame.Size}; $writer.Write([byte]$n); $writer.Write([byte]$n); $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset); $offset+=$frame.Bytes.Length }
foreach($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }; $writer.Dispose()
