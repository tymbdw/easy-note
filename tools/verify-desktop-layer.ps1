Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public class V8 {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr c);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern IntPtr FindWindow(string c, string w);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] public static extern IntPtr GetTopWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint c);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    public static IntPtr Progman() { return FindWindow("Progman", null); }
    public static string Cls(IntPtr h) { var sb = new StringBuilder(256); GetClassName(h, sb, 256); return sb.ToString(); }
    public static IntPtr Pick(int minW, int maxW) {
        IntPtr best = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            var s = new StringBuilder(256); GetClassName(h, s, 256);
            if (!s.ToString().StartsWith("HwndWrapper[DesktopNote")) return true;
            var r = new RECT(); GetWindowRect(h, out r);
            int w = r.Right - r.Left;
            if (w >= minW && w <= maxW) { best = h; return false; }
            return true;
        }, IntPtr.Zero);
        return best;
    }
    public static int ZIndex(IntPtr target) {
        IntPtr c = GetTopWindow(IntPtr.Zero); int i = 0;
        while (c != IntPtr.Zero && i < 900) { if (c == target) return i; c = GetWindow(c, 2); i++; }
        return -1;
    }
    // 所有可见的非桌面层顶层窗口里，最靠下的那个索引（= 我们的上限）
    public static int LowestAppIndex(IntPtr self) {
        int best = -1;
        IntPtr c = GetTopWindow(IntPtr.Zero); int i = 0;
        while (c != IntPtr.Zero && i < 900) {
            var cls = Cls(c);
            bool desktop = cls == "Progman" || cls == "WorkerW" || cls == "SHELLDLL_DefView" || cls == "SysListView32";
            if (c != self && !desktop && IsWindowVisible(c)) {
                var r = new RECT(); GetWindowRect(c, out r);
                if (r.Right - r.Left > 60 && r.Bottom - r.Top > 60 && best < i) best = i;
            }
            c = GetWindow(c, 2); i++;
        }
        return best;
    }
    public static void Click(int x, int y) {
        SetCursorPos(x, y); System.Threading.Thread.Sleep(150);
        mouse_event(0x0002, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(80);
        mouse_event(0x0004, 0, 0, 0, IntPtr.Zero);
    }
}
"@
try { [void][V8]::SetProcessDpiAwarenessContext([IntPtr](-4)) } catch {}

$w = [V8]::Pick(250, 900)
if ($w -eq [IntPtr]::Zero) { "widget NOT FOUND"; exit 1 }
$r = New-Object 'V8+RECT'
[void][V8]::GetWindowRect($w, [ref]$r)
$screen = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
"widget rect = {0},{1} {2}x{3}" -f $r.Left, $r.Top, ($r.Right-$r.Left), ($r.Bottom-$r.Top)
"桌面工作区 = {0},{1} {2}x{3}  → 右边距 {4}px, 上边距 {5}px" -f `
  $screen.Left, $screen.Top, $screen.Width, $screen.Height, ($screen.Right - $r.Right), ($r.Top - $screen.Top)

function State([string]$tag) {
    $p = New-Object 'V8+POINT'
    $p.X = [int](($r.Left + $r.Right) / 2); $p.Y = $r.Top + 20
    $hit = [V8]::WindowFromPoint($p)
    $zw = [V8]::ZIndex($w); $za = [V8]::LowestAppIndex($w)
    "{0,-14} 打点={1,-26} z(便签)={2,-4} 最靠下程序窗口 z={3,-4} 在程序之下={4}" -f `
      $tag, [V8]::Cls($hit), $zw, $za, ($zw -gt $za)
}

$sh = New-Object -ComObject Shell.Application
$sh.MinimizeAll()
Start-Sleep -Milliseconds 1500
State "桌面"

"--- 点一下便签（会被激活并顶到最前，看是否立即压回去）---"
[V8]::Click([int](($r.Left + $r.Right) / 2), $r.Top + 20)
Start-Sleep -Milliseconds 250
State "点击+0.25s"
Start-Sleep -Milliseconds 700
State "点击+1s"

"--- 用一个真实窗口盖在便签位置上 ---"
$cover = New-Object System.Windows.Forms.Form
$cover.FormBorderStyle = 'None'
$cover.BackColor = [System.Drawing.Color]::FromArgb(245, 250, 252)
$cover.StartPosition = 'Manual'
$cover.Location = New-Object System.Drawing.Point($r.Left, $r.Top)
$cover.Size = New-Object System.Drawing.Size(($r.Right - $r.Left), 280)
$cover.TopMost = $false
$cover.Show()
[System.Windows.Forms.Application]::DoEvents()
Start-Sleep -Milliseconds 900
State "被窗口覆盖"
$cover.Close()

Start-Sleep -Milliseconds 500
State "关掉覆盖窗口"
$sh.UndoMinimizeALL()
