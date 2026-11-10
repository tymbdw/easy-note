using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DesktopNote;

/// <summary>
/// 窗口外壳的低层操作：把便签压在窗口栈最底层（桌面平面），并拒绝被 Win+D 最小化。
/// 说明：本机有视频壁纸（Wallpaper Engine）在跑，它用 DirectComposition 覆盖整个桌面层，
/// 所以 SetParent 到 Progman/WorkerW 的经典做法在当前环境不会显示（实测三种 UI 栈都不可见），
/// 这里改用“普通窗口 + 常驻最底层”的方案，效果等价于贴在桌面上。
/// </summary>
internal static class ShellPin
{
    public static readonly IntPtr HWND_BOTTOM = (IntPtr)1;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int cmd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT p);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr h, StringBuilder s, int n);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? cls, string? win);

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;

    public static void ToBottom(IntPtr h)
        => SetWindowPos(h, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

    /// <summary>桌面窗口（Progman）。</summary>
    public static IntPtr DesktopWindow() => FindWindow("Progman", null);

    private static readonly string[] DesktopClasses =
        { "Progman", "WorkerW", "SHELLDLL_DefView", "SysListView32", "SysHeader32" };

    private static bool IsDesktopLayer(IntPtr h)
    {
        if (h == IntPtr.Zero) return false;
        var cls = ClassOfInternal(h);
        foreach (var c in DesktopClasses) if (cls == c) return true;
        var root = GetAncestor(h, 2); // GA_ROOT
        return root != IntPtr.Zero && ClassOfInternal(root) == "Progman";
    }

    private static string ClassOfInternal(IntPtr h)
    {
        var sb = new StringBuilder(256);
        GetClassName(h, sb, sb.Capacity);
        return sb.ToString();
    }

    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] private static extern IntPtr GetTopWindow(IntPtr h);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr h, uint cmd);

    /// <summary>
    /// 层级规则：紧贴桌面层之上（壁纸之上），但在所有普通程序窗口之下。
    /// 按 z 序列出可见顶层窗口，找到最上面那个"桌面层"窗口 D，把自己插到 D 正上方
    /// （= 插在"当前位于 D 上面那个窗口"的下面）。被点激活后靠这个函数压回去。
    /// </summary>
    public static void PlaceOnDesktop(IntPtr h)
    {
        if (h == IntPtr.Zero) return;
        var list = new List<IntPtr>(600);
        IntPtr c = GetTopWindow(IntPtr.Zero);
        int guard = 0;
        while (c != IntPtr.Zero && guard++ < 800)
        {
            if (c != h && IsWindowVisible(c)) list.Add(c);
            c = GetWindow(c, 2); // GW_HWNDNEXT
        }

        int k = list.FindIndex(IsDesktopLayer);
        if (k < 0) return;

        // 已经紧贴在桌面层上方就不动，避免抖动
        if (GetWindow(h, 2) == list[k]) return;

        if (k == 0)
        {
            // 桌面上没有任何程序窗口，直接放到最顶（此时最顶就是"桌面之上"）
            SetWindowPos(h, IntPtr.Zero /*HWND_TOP*/, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            return;
        }
        SetWindowPos(h, list[k - 1], 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    public static void MoveTo(IntPtr h, int x, int y)
        => SetWindowPos(h, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

    public static void SizeTo(IntPtr h, int x, int y, int w, int hgt)
        => SetWindowPos(h, IntPtr.Zero, x, y, w, hgt, SWP_NOZORDER | SWP_NOACTIVATE);

    /// <summary>被 Win+D / 显示桌面最小化后，无激活地恢复显示。</summary>
    public static void RestoreQuiet(IntPtr h)
    {
        if (h == IntPtr.Zero || !IsWindow(h)) return;
        if (IsIconic(h)) ShowWindow(h, 4 /*SW_SHOWNOACTIVATE*/);
    }

    public static void Log(string s)
    {
        try
        {
            Directory.CreateDirectory(Store.Dir);
            File.AppendAllText(Path.Combine(Store.Dir, "pin.log"),
                $"[{DateTime.Now:HH:mm:ss.fff}] {s}\r\n");
        }
        catch { }
    }
}
