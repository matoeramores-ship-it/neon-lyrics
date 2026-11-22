$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$metadata = Join-Path $env:WINDIR 'System32\WinMetadata'
$references = @(
    "/r:$framework\System.Runtime.dll",
    "/r:$framework\WPF\PresentationCore.dll", "/r:$framework\WPF\WindowsBase.dll",
    "/r:$metadata\Windows.Foundation.winmd",
    "/r:$metadata\Windows.Media.winmd", "/r:$metadata\Windows.Storage.winmd",
    '/r:yaml\lib\net47\YamlDotNet.dll', '/resource:yaml\lib\net47\YamlDotNet.dll,YamlDotNet.dll', '/resource:YamlLicense.txt,YamlDotNet.LICENSE', '/r:System.Net.Http.dll',
    '/r:naudio\lib\net35\NAudio.dll', '/resource:naudio\lib\net35\NAudio.dll,NAudio.dll', '/resource:NAudioLicense.txt,NAudio.LICENSE', '/r:System.Web.Extensions.dll',
    '/r:System.Windows.Forms.dll',
    '/r:System.Drawing.dll'
)
$sources = 'Program.cs', 'Playback.cs', 'Lyrics.cs', 'Wallpaper.cs', 'Scene.cs', 'WordTimes.cs', 'Rhythm.cs', 'Art.cs', 'HDCover.cs', 'LyricIcons.cs', 'MusicVideos.cs', 'VideoBackdrop.cs', 'SongReactions.cs', 'WordMotion.cs'

Add-Type -AssemblyName System.Drawing
$image = [Drawing.Bitmap]::new(64, 64)
$g = [Drawing.Graphics]::FromImage($image)
$g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$bg = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(18, 9, 34))
$accent = [Drawing.Pen]::new([Drawing.Color]::FromArgb(224, 255, 116), 6)
$surround = [Drawing.Pen]::new([Drawing.Color]::FromArgb(214, 118, 243), 4)
$accent.StartCap = $accent.EndCap = $surround.StartCap = $surround.EndCap = [Drawing.Drawing2D.LineCap]::Round
$g.FillEllipse($bg, 1, 1, 62, 62)
$g.DrawLine($surround, 23, 20, 41, 20)
$g.DrawLine($accent, 15, 32, 49, 32)
$g.DrawLine($surround, 25, 44, 39, 44)
$png = [IO.MemoryStream]::new()
$image.Save($png, [Drawing.Imaging.ImageFormat]::Png)
$ico = [IO.BinaryWriter]::new([IO.File]::Create((Join-Path $PWD 'NeonLyrics.ico')))
$ico.Write([uint16]0)
$ico.Write([uint16]1)
$ico.Write([uint16]1)
$ico.Write([byte]64)
$ico.Write([byte]64)
$ico.Write([uint16]0)
$ico.Write([uint16]1)
$ico.Write([uint16]32)
$ico.Write([uint32]$png.Length)
$ico.Write([uint32]22)
$ico.Write($png.ToArray())
$ico.Dispose()
$png.Dispose()
$surround.Dispose()
$accent.Dispose()
$bg.Dispose()
$g.Dispose()
$image.Dispose()

& "$framework\csc.exe" /nologo /optimize+ /target:winexe /platform:anycpu /win32manifest:NeonLyrics.manifest /win32icon:NeonLyrics.ico '/out:Neon Lyrics.exe' @references @sources
if ($LASTEXITCODE) { exit $LASTEXITCODE }
& "$framework\csc.exe" /nologo /target:exe /main:Check /win32manifest:NeonLyrics.manifest /out:Check.exe @references @sources Check.cs
exit $LASTEXITCODE






