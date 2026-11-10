using Microsoft.Win32;
using System.Drawing;
using System.Windows;
using System.Windows.Threading;
using Application = System.Windows.Application;

namespace DesktopNote;

public partial class App : Application
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunName = "DesktopNote";

    private Mutex? _mutex;
    private MainWindow? _win;
    private System.Windows.Forms.NotifyIcon? _tray;
    private System.Windows.Forms.ToolStripMenuItem? _autoStartItem;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "DesktopNote.SingleInstance.v1", out bool isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);

        DispatcherUnhandledException += (_, ev) =>
        {
            Log(ev.Exception);
            ev.Handled = true;
        };

        _win = new MainWindow();
        _win.Show();
        BuildTray();
    }

    private static void Log(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Store.Dir);
            File.AppendAllText(Path.Combine(Store.Dir, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n");
        }
        catch { }
    }

    private void BuildTray()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("显示 / 隐藏", null, (_, __) => ToggleVisible());

        menu.Items.Add("折叠 / 展开", null, (_, __) => _win?.ToggleFold());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        _autoStartItem = new System.Windows.Forms.ToolStripMenuItem("开机自启")
        {
            CheckOnClick = true,
            Checked = IsAutoStart(),
        };
        _autoStartItem.Click += (_, __) => SetAutoStart(_autoStartItem!.Checked);
        menu.Items.Add(_autoStartItem);

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, __) => ExitApp());

        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = MakeIcon(),
            Visible = true,
            Text = "易便签（双击显示/隐藏）",
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, __) => ToggleVisible();
    }

    private void ToggleVisible()
    {
        if (_win is null) return;
        if (_win.IsVisible) _win.HideToTray();
        else _win.ShowAgain();
    }

    private void ExitApp()
    {
        _win?.PersistNow();
        if (_tray is not null) { _tray.Visible = false; _tray.Dispose(); }
        Shutdown();
    }

    private static System.Drawing.Icon MakeIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.Transparent);
            using var card = new SolidBrush(System.Drawing.Color.FromArgb(255, 231, 245, 253));
            using var accent = new SolidBrush(System.Drawing.Color.FromArgb(255, 40, 153, 214));
            using var edge = new Pen(System.Drawing.Color.FromArgb(140, 24, 38, 51), 1.2f);
            using var path = RoundedRect(new Rectangle(2, 2, 28, 28), 7);
            g.FillPath(card, path);
            g.DrawPath(edge, path);
            g.FillRectangle(accent, 8, 9, 16, 2.6f);
            g.FillRectangle(accent, 8, 15, 11, 2.6f);
            g.FillRectangle(accent, 8, 21, 14, 2.6f);
        }
        return System.Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var p = new System.Drawing.Drawing2D.GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    private static bool IsAutoStart()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey);
            return k?.GetValue(RunName) is string s && s.Length > 0;
        }
        catch { return false; }
    }

    private static void SetAutoStart(bool on)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (k is null) return;
            if (on) k.SetValue(RunName, "\"" + Environment.ProcessPath + "\"");
            else k.DeleteValue(RunName, false);
        }
        catch { }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        base.OnExit(e);
    }
}
