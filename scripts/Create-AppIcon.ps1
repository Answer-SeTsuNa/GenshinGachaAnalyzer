param([string]$InputFile, [string]$OutputFile)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore,WindowsBase
$bitmap = [Windows.Media.Imaging.BitmapImage]::new()
$bitmap.BeginInit(); $bitmap.CacheOption = [Windows.Media.Imaging.BitmapCacheOption]::OnLoad
$bitmap.UriSource = [Uri]([IO.Path]::GetFullPath($InputFile)); $bitmap.EndInit(); $bitmap.Freeze()
$frames = @()
foreach ($size in @(16,24,32,48,64,128,256)) {
    $visual = [Windows.Media.DrawingVisual]::new()
    $context = $visual.RenderOpen()
    $context.DrawImage($bitmap, [Windows.Rect]::new(0,0,$size,$size)); $context.Close()
    $target = [Windows.Media.Imaging.RenderTargetBitmap]::new($size,$size,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $target.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($target))
    $memory = [IO.MemoryStream]::new(); $encoder.Save($memory)
    $frames += @{ Size=$size; Bytes=$memory.ToArray() }; $memory.Dispose()
}
$writer = [IO.BinaryWriter]::new([IO.File]::Create([IO.Path]::GetFullPath($OutputFile)))
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + $frames.Count * 16
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
} finally { $writer.Dispose() }
Write-Output 'Generated multi-size app icon (16-256px).'
