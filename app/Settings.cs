using CommandLine;

namespace VarjoDataLogger;

public class Settings
{
    [Option("ip", Required = false, HelpText = "IP address of the PC running N-Back task application. Default is '127.0.0.1'")]
    public string IP { get; set; } = "127.0.0.1";

    [Option('l', "log", Required = false, HelpText = "Log file folder, must be without spaces. Default is 'MyDocuments'")]
    public string LogFolder { get; set; }

    [Option('f', "finger", Required = false, HelpText = "If set, then the hand tracking site is an index finger instead of palm. Default is 'false'")]
    public bool UseFinger { get; set; } = false;

    public static bool TryGetInstance(out Settings settings, out string? error)
    {
        error = null;

        try
        {
            _instance ??= Create();
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        settings = _instance ?? new Settings();
        return _instance != null;
    }

    /// <summary>
    /// IMPORTANT! The constructor must not be used explicitely, rather use <see cref="TryGetInstance"/>
    /// </summary>
    public Settings()
    {
        /*
        var settings = Properties.Settings.Default;

        _logFolder = settings.LogFolder;
        */

        if (string.IsNullOrEmpty(LogFolder))
        {
            LogFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
    }

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

    private static Settings Create()
    {
        var args = Environment.GetCommandLineArgs()[1..];
        var settings = Parser.Default.ParseArguments<Settings>(args);

        bool missesRequired = false;
        settings.WithNotParsed(errors => missesRequired = errors.Any(error => error is MissingRequiredOptionError));

        if (missesRequired)
            throw new Exception("Missing required options");

        return settings.Value ?? new Settings();
    }
}
