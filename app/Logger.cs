using System.Windows.Forms;

namespace VarjoDataLogger;

internal class Logger
{
    public static Logger Instance => _instance ??= new();

    public void Reset()
    {
        lock (_records)
        {
            _records.Clear();
        }
    }

    public void Add(params object[] items)
    {
        var record = string.Join('\t', [DateTime.Now.Ticks, ..items]);

        lock (_records)
        {
            _records.Add(record);
        }
    }

    public string? Save()
    {
        if (_records.Count == 0)
            return null;

        var folderName = "";
        if (Settings.TryGetInstance(out Settings settings, out string? error))
        {
            folderName = settings.LogFolder;
        }

        if (string.IsNullOrEmpty(folderName))
        {
            folderName = SelectLogFolder();
            if (folderName != null)
                settings.LogFolder = folderName;
            else
                return null;
        }

        var filename = Path.Join(folderName, $"vdl-{DateTime.Now:u}.txt".ToPath());

        try
        {
            using var writer = new StreamWriter(filename);

            lock (_records)
            {
                foreach (var record in _records)
                {
                    writer.WriteLine(record);
                }

                _records.Clear();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
            filename = null;
            MessageBox.Show($"Cannot save data into '{filename}':\n{ex.Message}", App.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        return filename;
    }

    public static string? SelectLogFolder(string? folderName = null)
    {
        var ofd = new OpenFolder.FolderPicker
        {
            InputPath = !string.IsNullOrEmpty(folderName) ?
                folderName :
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            ForceFileSystem = false,
            Title = $"Select a folder to store {App.Name} log files",
        };

        if (ofd.ShowDialog() == false || string.IsNullOrEmpty(ofd.ResultPath))
        {
            return ofd.ResultPath;
        }

        return null;
    }

    // Internal

    protected Logger() { }

    static Logger? _instance = null;

    readonly List<string> _records = [];
}
