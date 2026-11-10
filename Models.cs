using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DesktopNote;

public class NoteItem : INotifyPropertyChanged
{
    private string _text = "";
    private bool _done;

    public string Text
    {
        get => _text;
        set { if (_text != value) { _text = value; OnPropertyChanged(); } }
    }

    public bool Done
    {
        get => _done;
        set { if (_done != value) { _done = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class Board : INotifyPropertyChanged
{
    private string _name = "";
    private string _accent = "#7AA2F7";

    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; OnPropertyChanged(); } }
    }

    public string Accent
    {
        get => _accent;
        set { if (_accent != value) { _accent = value; OnPropertyChanged(); } }
    }

    public ObservableCollection<NoteItem> Items { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class Store
{
    public List<Board> Boards { get; set; } = new();

    /// <summary>窗口在桌面宿主窗口坐标系中的位置（物理像素）。</summary>
    public int X { get; set; } = 80;
    public int Y { get; set; } = 80;
    public int W { get; set; } = 336;
    public int H { get; set; } = 486;
    public bool Collapsed { get; set; }
    public bool Topmost { get; set; }
    public double Opacity { get; set; } = 1.0;

    private static readonly (string Name, string Accent)[] Defaults =
    {
        ("今日", "#7AA2F7"),
        ("本周", "#9ECE6A"),
        ("未掌握", "#F7768E"),
    };

    public static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopNote");

    private static string FilePath => Path.Combine(Dir, "notes.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static Store Load()
    {
        Store? s = null;
        try
        {
            if (File.Exists(FilePath))
                s = JsonSerializer.Deserialize<Store>(File.ReadAllText(FilePath), JsonOpts);
        }
        catch { s = null; }

        s ??= new Store();
        s.Boards ??= new List<Board>();

        // 保证三块固定面板存在且顺序稳定，同时保留用户写的内容
        var normalized = new List<Board>();
        foreach (var (name, accent) in Defaults)
        {
            var b = s.Boards.FirstOrDefault(x => x.Name == name);
            if (b is null)
            {
                b = new Board { Name = name };
                b.Items.Add(new NoteItem());
            }
            b.Accent = accent;
            b.Items ??= new ObservableCollection<NoteItem>();
            if (b.Items.Count == 0) b.Items.Add(new NoteItem());
            normalized.Add(b);
        }
        s.Boards = normalized;
        return s;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, JsonOpts));
            File.Copy(tmp, FilePath, true);
            File.Delete(tmp);
        }
        catch { /* 落盘失败不打断使用 */ }
    }
}
