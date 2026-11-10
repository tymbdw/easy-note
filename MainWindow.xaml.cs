using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Application = System.Windows.Application;

namespace DesktopNote;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private readonly DispatcherTimer _deskTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };

    private Store _store = new();
    private int _index;
    private IntPtr _hwnd;
    private bool _collapsed;
    private bool _userHidden;

    public MainWindow()
    {
        InitializeComponent();

        string path = Path.Combine(Store.Dir, "notes.json");
        bool firstRun = !File.Exists(path);

        _store = Store.Load();
        foreach (var b in _store.Boards) HookBoard(b);

        var now = DateTime.Now;
        DayText.Text = now.Day.ToString();
        MonthText.Text = now.Month + "月";
        WeekText.Text = "周" + "日一二三四五六"[(int)now.DayOfWeek];
        AddBox.TextChanged += (_, __) =>
            AddHint.Visibility = string.IsNullOrEmpty(AddBox.Text) ? Visibility.Visible : Visibility.Collapsed;

        _saveTimer.Tick += (_, __) => PersistNow();
        _deskTimer.Tick += (_, __) => KeepOnDesktop();

        Loaded += (_, __) => OnLoaded(firstRun);
        Closed += (_, __) => { _saveTimer.Stop(); PersistNow(); };
    }

    private void OnLoaded(bool firstRun)
    {
        _hwnd = new WindowInteropHelper(this).Handle;

        if (PresentationSource.FromVisual(this) is HwndSource src) src.AddHook(WndProc);

        var dpi0 = VisualTreeHelper.GetDpi(this);
        if (firstRun || _store.X < -5000 || _store.Y < -5000)
        {
            var wa = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
            _store.X = wa.Right - (int)(Width * dpi0.DpiScaleX) - 140;
            _store.Y = 80;
        }
        Left = _store.X / dpi0.DpiScaleX;
        Top = _store.Y / dpi0.DpiScaleY;
        Topmost = _store.Topmost;
        if (!_store.Topmost) ShellPin.PlaceOnDesktop(_hwnd);
        _deskTimer.Start();

        if (_store.Collapsed)
        {
            _collapsed = false;
            ToggleFold();
        }

        Tab0.IsChecked = true;
        AddBox.Focus();
    }

    /// <summary>钉在桌面右上角；分辨率变化后也会自动回到位。</summary>
    private void AnchorToCorner()
    {
        var screen = System.Windows.Forms.Screen.PrimaryScreen;
        if (screen is null) return;
        var wa = screen.WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(this);
        double left = wa.Right / dpi.DpiScaleX - Width - 16;
        double top = wa.Top / dpi.DpiScaleY + 16;
        if (Math.Abs(Left - left) > 0.5) Left = left;
        if (Math.Abs(Top - top) > 0.5) Top = top;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_SIZE = 0x0005;
        const int WM_ACTIVATE = 0x0006;
        const int WM_SYSCOMMAND = 0x0112;

        switch (msg)
        {
            case WM_SYSCOMMAND:
                // 拦下 Win+D / 显示桌面 发来的最小化
                if ((long)wParam == 0xF020)
                {
                    handled = true;
                    Dispatcher.BeginInvoke(new Action(KeepOnDesktop), DispatcherPriority.Background);
                }
                break;

            case WM_SIZE:
                if ((int)wParam == 1 /*SIZE_MINIMIZED*/)
                {
                    handled = true;
                    Dispatcher.BeginInvoke(new Action(KeepOnDesktop), DispatcherPriority.Background);
                }
                break;

            case WM_ACTIVATE:
                // 被点击/被激活也立刻压回桌面层，任何软件窗口都能盖住它
                Dispatcher.BeginInvoke(new Action(KeepOnDesktop), DispatcherPriority.Background);
                break;
        }
        return IntPtr.Zero;
    }

    // ---------------- 数据 ----------------

    private ObservableCollection<NoteItem> Current => _store.Boards[_index].Items;

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (_store is null || sender is not RadioButton rb) return;
        if (!int.TryParse(rb.Tag?.ToString(), out int i)) return;
        if (i < 0 || i >= _store.Boards.Count) return;
        SelectBoard(i);
    }

    private void SelectBoard(int i)
    {
        _index = i;
        ListHost.ItemsSource = Current;
        UpdateCount();
        ListScroll.ScrollToTop();
    }

    private void UpdateCount()
    {
        if (_store.Boards.Count == 0) return;
        var items = Current;
        int done = items.Count(x => x.Done);
        CountText.Text = items.Count(x => !string.IsNullOrWhiteSpace(x.Text)) == 0 ? "" : $"{done}/{items.Count} 完成";
    }

    private void HookBoard(Board b)
    {
        b.Items.CollectionChanged += Items_Changed;
        foreach (var it in b.Items) it.PropertyChanged += Item_PropertyChanged;
    }

    private void Items_Changed(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (NoteItem it in e.NewItems) it.PropertyChanged += Item_PropertyChanged;
        if (e.OldItems is not null)
            foreach (NoteItem it in e.OldItems) it.PropertyChanged -= Item_PropertyChanged;
        QueueSave();
        UpdateCount();
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        QueueSave();
        if (e.PropertyName == nameof(NoteItem.Done)) UpdateCount();
    }

    private void QueueSave()
    {
        StatusText.Text = "编辑中…";
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void PersistNow()
    {
        _saveTimer.Stop();
        if (_hwnd != IntPtr.Zero && ShellPin.IsWindow(_hwnd))
        {
            var r = new ShellPin.RECT();
            if (ShellPin.GetWindowRect(_hwnd, out r))
            {
                _store.X = r.Left;
                _store.Y = r.Top;
                _store.W = r.Right - r.Left;
                }
        }
        _store.Collapsed = _collapsed;
        _store.Save();
        StatusText.Text = "已保存 " + DateTime.Now.ToString("HH:mm");
    }

    // ---------------- 条目 ----------------

    private void Item_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb || tb.DataContext is not NoteItem item) return;
        var items = Current;
        int i = items.IndexOf(item);
        if (i < 0) return;

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            var ni = new NoteItem();
            items.Insert(i + 1, ni);
            FocusItem(ni);
        }
        else if (e.Key == Key.Back && string.IsNullOrEmpty(tb.Text) && items.Count > 1)
        {
            e.Handled = true;
            items.RemoveAt(i);
            FocusItem(items[Math.Max(0, i - 1)]);
        }
        else if (e.Key == Key.Down && i < items.Count - 1)
        {
            e.Handled = true;
            FocusItem(items[i + 1]);
        }
        else if (e.Key == Key.Up && i > 0)
        {
            e.Handled = true;
            FocusItem(items[i - 1]);
        }
    }

    private void FocusItem(NoteItem item)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            ListHost.UpdateLayout();
            if (ListHost.ItemContainerGenerator.ContainerFromItem(item) is not ContentPresenter cp) return;
            var tb = FindChild<TextBox>(cp);
            if (tb is null) return;
            tb.Focus();
            tb.CaretIndex = tb.Text.Length;
            tb.BringIntoView();
        }), DispatcherPriority.Background);
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        int n = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < n; i++)
        {
            var c = VisualTreeHelper.GetChild(parent, i);
            if (c is T t) return t;
            var r = FindChild<T>(c);
            if (r is not null) return r;
        }
        return null;
    }

    private void RowDel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.DataContext is NoteItem item)
        {
            Current.Remove(item);
            if (Current.Count == 0) Current.Add(new NoteItem());
        }
    }

    private void DeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is NoteItem item)
        {
            Current.Remove(item);
            if (Current.Count == 0) Current.Add(new NoteItem());
        }
    }

    private void AddBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            var text = AddBox.Text.Trim();
            if (text.Length == 0) return;
            var ni = new NoteItem { Text = text };
            var items = Current;
            int empty = items.ToList().FindIndex(x => string.IsNullOrWhiteSpace(x.Text));
            if (empty >= 0) items.Insert(empty, ni);
            else items.Add(ni);
            AddBox.Clear();
            AddHint.Visibility = Visibility.Visible;
            FocusItem(ni);
        }
        else if (e.Key == Key.Escape)
        {
            AddBox.Clear();
        }
    }

    private void AddBox_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        AddHint.Visibility = string.IsNullOrEmpty(AddBox.Text) ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------------- 外壳 ----------------

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.ClickCount == 2) ToggleFold();   // 位置固定，不提供拖动
    }

    private void Fold_Click(object sender, RoutedEventArgs e) => ToggleFold();

    public void ToggleFold()
    {
        _collapsed = !_collapsed;
        for (int i = 1; i <= 4; i++)
            RootGrid.RowDefinitions[i].Height = _collapsed ? new GridLength(0) : GridLength.Auto;
        if (!_collapsed) RootGrid.RowDefinitions[2].Height = new GridLength(1, GridUnitType.Star);

        if (_collapsed)
        {
            SizeToContent = SizeToContent.Manual;
            Height = 114;
        }
        else
        {
            Height = double.NaN;
            SizeToContent = SizeToContent.Height;
        }
        FoldBtn.Content = _collapsed ? "▸" : "▾";
        AnchorToCorner();
        QueueSave();
    }

    private void Hide_Click(object sender, RoutedEventArgs e) => HideToTray();

    public void HideToTray()
    {
        _userHidden = true;
        Hide();
    }

    /// <summary>位置钉在右上角，层级钉在壁纸之上、所有程序之下。</summary>
    private void KeepOnDesktop()
    {
        if (_hwnd == IntPtr.Zero || _userHidden) return;
        if (!ShellPin.IsWindow(_hwnd) || !IsVisible) return;
        AnchorToCorner();
        ShellPin.RestoreQuiet(_hwnd);
        ShellPin.PlaceOnDesktop(_hwnd);
    }

    public void ShowAgain()
    {
        _userHidden = false;
        Show();
        if (_hwnd != IntPtr.Zero)
        {
            AnchorToCorner();
            ShellPin.PlaceOnDesktop(_hwnd);
        }
    }
}
