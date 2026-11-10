Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public class S {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr c);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public static string Cls(IntPtr h) { var sb = new StringBuilder(256); GetClassName(h, sb, 256); return sb.ToString(); }
    public static IntPtr Pick(int minW, int maxW) {
        IntPtr best = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            var s = new StringBuilder(256); GetClassName(h, s, 256);
            if (!s.ToString().StartsWith("HwndWrapper[DesktopNote")) return true;
            var r = new RECT(); GetWindowRect(h, out r);
            int w = r.Right - r.Left;
            if (w >= minW && w <= maxW && IsWindowVisible(h)) { best = h; return false; }
            return true;
        }, IntPtr.Zero);
        return best;
    }
}
"@
try { [void][S]::SetProcessDpiAwarenessContext([IntPtr](-4)) } catch {}

$w = [S]::Pick(250, 900)
if ($w -eq [IntPtr]::Zero) { "widget NOT FOUND"; exit 1 }
$r = New-Object 'S+RECT'
[void][S]::GetWindowRect($w, [ref]$r)
"rect={0},{1} {2}x{3}" -f $r.Left, $r.Top, ($r.Right-$r.Left), ($r.Bottom-$r.Top)

$pad = 24
$x0 = $r.Left - $pad; $y0 = $r.Top - $pad
$wd = ($r.Right - $r.Left) + $pad * 2; $ht = ($r.Bottom - $r.Top) + $pad * 2

$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$full = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
$g = [System.Drawing.Graphics]::FromImage($full)
$g.CopyFromScreen($b.X, $b.Y, 0, 0, $full.Size)
$crop = New-Object System.Drawing.Bitmap($wd, $ht)
$g2 = [System.Drawing.Graphics]::FromImage($crop)
$g2.DrawImage($full, (New-Object System.Drawing.Rectangle(0, 0, $wd, $ht)), (New-Object System.Drawing.Rectangle($x0, $y0, $wd, $ht)), [System.Drawing.GraphicsUnit]::Pixel)
$out = Join-Path (Split-Path $PSScriptRoot -Parent) "docs\screenshot.png"
New-Item -ItemType Directory -Force -Path (Split-Path $out -Parent) | Out-Null
$crop.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
"shot: $out  {0}x{1}" -f $wd, $ht
$g.Dispose(); $full.Dispose(); $g2.Dispose(); $crop.Dispose()
