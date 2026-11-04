Add-Type -AssemblyName System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$frames=@()
$source = [Drawing.Image]::FromFile((Join-Path $root 'assets/basalt-crystal.png'))
try {
    foreach ($size in @(16,24,32,48,64,128,256)) {
        $bitmap = New-Object Drawing.Bitmap($source, $size, $size)
        $stream = New-Object IO.MemoryStream
        try {
            $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
            $frames += @{ Size = $size; Bytes = $stream.ToArray() }
        } finally {
            $stream.Dispose()
            $bitmap.Dispose()
        }
    }
} finally {
    $source.Dispose()
}
$out=[IO.File]::Create((Join-Path $root 'assets/Basalt.ico')); $writer=New-Object IO.BinaryWriter($out); $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count); $offset=6+16*$frames.Count
foreach($frame in $frames) { $n=if($frame.Size -eq 256){0}else{$frame.Size}; $writer.Write([byte]$n); $writer.Write([byte]$n); $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset); $offset+=$frame.Bytes.Length }
foreach($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }; $writer.Dispose()
