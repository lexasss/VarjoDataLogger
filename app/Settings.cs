using CommandLine;

namespace VarjoDataLogger;

public class Settings
{
    [Option('n', "nbtip", Required = false, HelpText = "IP address of the PC running N-Back task application. Default is '127.0.0.1'")]
    public string NBackTaskIP { get; set; } = "127.0.0.1";

    [Option('c', "cttip", Required = false, HelpText = "IP address of the PC running CTT application. Default is '127.0.0.1'")]
    public string CttIP { get; set; } = "127.0.0.1";

    [Option('l', "log", Required = false, HelpText = "Log file folder, must be without spaces. Default is 'MyDocuments'")]
    public string LogFolder { get; set; }

    [Option('o', "offset", Required = false, HelpText = "Leap Motion ZYX offsets (comma-separated, no spaces). Default is '-6,15,0'")]
    public string LmOffsetStr { get; set; } = "-6,15,0";

    [Option('v', "verbose", Required = false, HelpText = "Debug info is printed in the verbose mode. Default is 'false'")]
    public bool IsVerbose { get; set; } = false;

    [Option('d', "debug", Required = false, HelpText = "Sets to the debug mode. Default is 'false'")]
    public bool IsDebugMode { get; set; } = false;

    // Maybe this is redundant and all Leap Motion devices has the same coordinate system orientation
    //[Option('c', "coords", Required = false, HelpText = "Leap Motion coordinates. Default is 'XYZ' meaning left, forward, down. Use lowercase to inverse an axis the direction")]
    //public string LmCoords { get; set; } = "XYZ";

    public Leap.Vector LmOffset
    {
        get
        {
            var p = LmOffsetStr.Split(",");
            if (float.TryParse(p.Length > 2 ? p[2] : "0", out float x) &&
                float.TryParse(p.Length > 1 ? p[1] : "0", out float y) &&
                float.TryParse(p[0], out float z))
            {
                return new Leap.Vector(x, y, z);
            }
            else
            {
                return new Leap.Vector();
            }
        }
    }

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
        if (string.IsNullOrEmpty(LogFolder))
        {
            LogFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
    }


    // Internal

    static Settings? _instance = null;

    private static Settings Create()
    {
        var args = Environment.GetCommandLineArgs()[1..];
        var settings = Parser.Default.ParseArguments<Settings>(args);

        bool missesRequiredSettings = false;
        settings.WithNotParsed(errors => missesRequiredSettings = missesRequiredSettings || errors.Any(error => error is MissingRequiredOptionError));

        if (missesRequiredSettings)
            throw new Exception("Missing required options");

        return settings.Value ?? new Settings();
    }
}
