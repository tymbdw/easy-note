Add-Type -AssemblyName System.Drawing

$size = 256
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

function RoundedPath($rect, $radius) {
    $d = $radius * 2
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $p.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $p.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $p.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

$rect = New-Object System.Drawing.Rectangle(16, 16, 224, 224)
$path = RoundedPath $rect 54

# 玻璃底 + 轻微纵向渐变
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Point(0, 0)),
    (New-Object System.Drawing.Point(0, 256)),
    [System.Drawing.Color]::FromArgb(255, 252, 253, 255),
    [System.Drawing.Color]::FromArgb(255, 214, 236, 250))
$g.FillPath($brush, $path)

# 描边
$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(90, 24, 38, 51), 7)
$g.DrawPath($pen, $path)

# 冰蓝三条笔迹
$accent = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 40, 153, 214))
$g.FillRectangle($accent, 68, 88, 120, 16)
$g.FillRectangle($accent, 68, 124, 84, 16)
$g.FillRectangle($accent, 68, 160, 106, 16)

# 深色勾
$navy = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 24, 38, 51), 11)
$navy.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$navy.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawLine($navy, 132, 168, 148, 184)
$g.DrawLine($navy, 148, 184, 184, 142)

$g.Dispose()

# 存 PNG
$pngPath = "D:\DesktopNote\assets\icon256.png"
New-Item -ItemType Directory -Force -Path "D:\DesktopNote\assets" | Out-Null
$bmp.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)

# 包成 .ico（Vista+ 支持 PNG 内嵌）
$png = [System.IO.File]::ReadAllBytes($pngPath)
$icoPath = "D:\DesktopNote\assets\desktopnote.ico"
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0)          # reserved
$bw.Write([UInt16]1)          # type = icon
$bw.Write([UInt16]1)          # count
$bw.Write([byte]0)            # width 256
$bw.Write([byte]0)            # height 256
$bw.Write([byte]0)            # colors
$bw.Write([byte]0)            # reserved
$bw.Write([UInt16]1)          # planes
$bw.Write([UInt16]32)         # bpp
$bw.Write([UInt32]$png.Length)
$bw.Write([UInt32]22)         # offset
$bw.Write($png)
$bw.Flush(); $bw.Close(); $fs.Close()
$bmp.Dispose()

"ico: {0}  ({1} bytes)" -f $icoPath, (Get-Item $icoPath).Length
