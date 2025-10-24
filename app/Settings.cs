using CommandLine;

namespace VarjoDataLogger;

public enum TaskOrder
{
    SystemFirst,
    SelfFirst,
}
public enum Pace
{
    System,
    Self,
}

public record class Condition(int SystemTask, int SelfTask, TaskOrder Order);

public class Settings
{
    [Option('n', "nbtip", Required = false, HelpText = "IP address of the PC running N-Back task application. Default is '127.0.0.1'.")]
    public string NBackTaskIP { get; set; } = "127.0.0.1";

    [Option('c', "cttip", Required = false, HelpText = "IP address of the PC running CTT application. Default is '127.0.0.1'.")]
    public string CttIP { get; set; } = "127.0.0.1";

    [Option('m', "lmsip", Required = false, HelpText = "IP address of the PC running Leap Motion Streamer application. Default is '127.0.0.1'.")]
    public string LeapMotionStreamerIP { get; set; } = "127.0.0.1";
    
    [Option('l', "log", Required = false, HelpText = "Log file folder, must be without spaces. Default is 'C:/Users/<USERNAME>/Documents'.")]
    public string LogFolder { get; set; }

    [Option('o', "offset", Required = false, HelpText = "Leap Motion ZYX offsets (comma-separated, no spaces). Default is '-6,15,0'.")]
    public string LmOffsetStr { get; set; } = "-6,15,0";

    [Option('s', "setup", Required = false, HelpText = "Path to a file with the experiment setup. Default is 'no value'.")]
    public string? SetupFilename { get; set; }

    [Option('t', "task", Required = false, HelpText = "Index of the task set loaded from the setup file. Ignored if no setup is loaded. Default is '-1' meaning the last task.")]
    public int TaskIndex { get; set; } = -1;

    [Option('h', "hide", Required = false, HelpText = "Forces the console window to be hidden (minimized) while the tracking is on.")]
    public bool IsHiddenWhileTracking { get; set; } = false;

    [Option('v', "verbose", Required = false, HelpText = "Debug info is printed in the verbose mode.")]
    public bool IsVerbose { get; set; } = false;

    [Option('d', "debug", Required = false, HelpText = "Sets to the debug mode.")]
    public bool IsDebugMode { get; set; } = false;

    // Maybe this is redundant and all Leap Motion devices has the same coordinate system orientation
    //[Option('c', "coords", Required = false, HelpText = "Leap Motion coordinates. Default is 'XYZ' meaning left, forward, down. Use lowercase to inverse an axis the direction")]
    //public string LmCoords { get; set; } = "XYZ";

    public int ParticipantID
    {
        get => field;
        set
        {
            field = value;
            ConfigureTask(value);
        }
    } = 0;

    public Pace? Pace { get; private set; } = null;

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

    readonly Dictionary<int, Condition> _taskConditions = new()
    {
        { 0, new Condition(0, 4, TaskOrder.SystemFirst) },
        { 1, new Condition(1, 5, TaskOrder.SelfFirst) },
        { 2, new Condition(2, 4, TaskOrder.SystemFirst) },
        { 3, new Condition(3, 5, TaskOrder.SelfFirst) },
        { 4, new Condition(0, 5, TaskOrder.SystemFirst) },
        { 5, new Condition(1, 4, TaskOrder.SelfFirst) },
        { 6, new Condition(2, 5, TaskOrder.SystemFirst) },
        { 7, new Condition(3, 4, TaskOrder.SelfFirst) },
        { 8, new Condition(0, 4, TaskOrder.SelfFirst) },
        { 9, new Condition(1, 5, TaskOrder.SystemFirst) },
        { 10, new Condition(2, 4, TaskOrder.SelfFirst) },
        { 11, new Condition(3, 5, TaskOrder.SystemFirst) },
        { 12, new Condition(0, 5, TaskOrder.SelfFirst) },
        { 13, new Condition(1, 4, TaskOrder.SystemFirst) },
        { 14, new Condition(2, 5, TaskOrder.SelfFirst) },
        { 15, new Condition(3, 4, TaskOrder.SystemFirst) },
        { 16, new Condition(6, 6, TaskOrder.SystemFirst) },
    };

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

    private void ConfigureTask(int participantId)
    {
        if (participantId <= 0)
        {
            Pace = null;
        }
        else
        {
            Condition taskCondition = _taskConditions[(participantId - 1) % _taskConditions.Count];

            if (Directory.Exists(LogFileManager.GetParticipantFolder(participantId)))
            {
                Pace = taskCondition.Order == TaskOrder.SystemFirst
                    ? VarjoDataLogger.Pace.Self
                    : VarjoDataLogger.Pace.System;
            }
            else
            {
                Pace = taskCondition.Order == TaskOrder.SystemFirst
                    ? VarjoDataLogger.Pace.System
                    : VarjoDataLogger.Pace.Self;
            }

            TaskIndex = Pace switch
            {
                VarjoDataLogger.Pace.System => taskCondition.SystemTask,
                VarjoDataLogger.Pace.Self => taskCondition.SelfTask,
                _ => -1,
            };
        }
    }
}
