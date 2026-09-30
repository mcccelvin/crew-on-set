param([Parameter(Mandatory=$true)][string]$Source,[Parameter(Mandatory=$true)][string]$Destination)
# Windows PowerShell -STA: ten seconds, 12 fps, 10 columns by 12 rows.
Add-Type -AssemblyName PresentationCore,PresentationFramework,WindowsBase
function Pump([int]$milliseconds) {
    $timer = New-Object System.Windows.Threading.DispatcherTimer
    $frame = New-Object System.Windows.Threading.DispatcherFrame
    $timer.Interval = [TimeSpan]::FromMilliseconds($milliseconds)
    $timer.Add_Tick({ $frame.Continue = $false; $timer.Stop() }.GetNewClosure())
    $timer.Start()
    [System.Windows.Threading.Dispatcher]::PushFrame($frame)
}
$player = New-Object System.Windows.Media.MediaPlayer
$player.Volume = 0
$player.Open([Uri][System.IO.Path]::GetFullPath($Source))
$player.Play()
Pump 1500
if ($player.NaturalVideoWidth -eq 0) { throw 'Could not decode the loading reference.' }
# Keep playback running while seeking so MediaPlayer refreshes its decoded frame.
$visual = New-Object System.Windows.Media.DrawingVisual
$drawing = $visual.RenderOpen()
for ($index = 0; $index -lt 120; $index++) {
    $player.Position = [TimeSpan]::FromSeconds($index / 12.0)
    Pump 100
    $frameVisual = New-Object System.Windows.Media.DrawingVisual
    $context = $frameVisual.RenderOpen()
    $context.DrawVideo($player, [System.Windows.Rect]::new(-272,-78,1024,576))
    $context.Close()
    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(480,420,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($frameVisual)
    $drawing.DrawImage($bitmap,[System.Windows.Rect]::new(($index % 10)*480,[Math]::Floor($index/10)*420,480,420))
}
$drawing.Close()
$sheet = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(4800,5040,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
$sheet.Render($visual)
$encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
$encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($sheet))
$output = [System.IO.File]::Create($Destination)
try { $encoder.Save($output) } finally { $output.Dispose(); $player.Close() }
Write-Output 'Exported 120 loading frames (12 fps).'
