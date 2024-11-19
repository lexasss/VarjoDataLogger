using System.ComponentModel;

namespace VarjoDataLogger;

class Settings : INotifyPropertyChanged
{
    public static Settings Instance => _instance ??= new();

    public string LogFolder
    {
        get => _logFolder;
        set
        {
            _logFolder = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LogFolder)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Save()
    {
        /*
        var settings = Properties.Settings.Default;

        settings.LogFolder = LogFolder;

        settings.Save();
        */
    }

    // Internal

    static Settings? _instance = null;

    string _logFolder = "";

#pragma warning disable CS8618
    private Settings()
    {
        Load();

        if (string.IsNullOrEmpty(_logFolder))
        {
            _logFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
    }
#pragma warning restore CS8618

    private void Load()
    {
        /*
        var settings = Properties.Settings.Default;

        _logFolder = settings.LogFolder;
        */
    }
}
