using System.Diagnostics;
using System.Globalization;

namespace VarjoDataLogger;

class App
{
    public static string Name => "Varjo Data Logger";
    public static Debug Debug { get; } = new();

    public static void Main()
    {
        // Set the US-culture across the application to avoid decimal point parsing/logging issues
        var culture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;

        if (!Settings.TryGetInstance(out Settings settings, out string? error))
        {
            Console.WriteLine(error);
            return;
        }

        using var recorder = new Recorder(settings);
        recorder.Run();

        Debug.Dispose();
    }
}

class Recorder : IDisposable
{
    public Recorder(Settings settings)
    {
        _settings = settings;

        _nbtClient.Message += NbtClient_Message;
        var nbackConnTask = _nbtClient.Connect(_settings.NBackTaskIP, NetClient.NBackTaskPort);
        nbackConnTask.Wait();
        HandleConnectionResult("N-Back task", _nbtClient, nbackConnTask.Result);

        var cttConnTask = _cttClient.Connect(_settings.CttIP, NetClient.CttPort);
        cttConnTask.Wait();
        HandleConnectionResult("CTT", _cttClient, cttConnTask.Result);

        _lmsClient.Message += LmsClient_Message;
        var lmsConnTask = _lmsClient.Connect(_settings.LeapMotionStreamerIP, NetClient.LeapMotionStreamerPort);
        lmsConnTask.Wait();
        HandleConnectionResult("Leap Motion Streamer", _lmsClient, lmsConnTask.Result);

        _handTracker.Data += HandTracker_Data;
    }

    public void Run()
    {
        _hasInterrupted = false;

        var tasks = TaskSetup.Load(_settings.SetupFilename, _settings.TaskIndex).GetAllTasks();
        TaskSetup.SaveTo(_settings.LogFolder, tasks);

        for (int i = 0; i < tasks.Length; i++)
        {
            _gazeTracker = new GazeTracker();

            var task = tasks[i];

            if (task.IsValid)
            {
                Console.WriteLine();

                _nbtClient.Send($"{NET_COMMAND_SET_NBT_TASK}{task.NBackTaskIndex}");
                _cttClient.Send($"{NET_COMMAND_SET_CTT_LAMBDA}{task.CttLambdaIndex}");
                var info = $"Task {i + 1}/{tasks.Length}: CTT = {task.CttLambdaIndex}, NBack = {task.NBackTaskIndex}";
                Log(info);
            }

            lock (_headsetHandLocation)
            {
                HandLocation.Empty.CopyTo(_headsetHandLocation);
            }
            lock (_topviewHandLocation)
            {
                HandLocation.Empty.CopyTo(_topviewHandLocation);
            }
            lock (_nbackTaskMessage)
            {
                _nbackTaskMessage = "";
            }

            _hasFinished = false;

            if ((_handTracker.IsReady && _gazeTracker.IsReady) || _settings.IsDebugMode)
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine($"Press ENTER to start");
                var cmd = Console.ReadLine();

                if (cmd == null || _hasInterrupted)
                    break;

                _startTime = 0;
                _gazeSampleCount = 0;
                _gazeTracker.Data += GazeTracker_Data;

                if (_settings.IsHiddenWhileTracking)
                {
                    WinUtils.HideConsoleWindow();
                }

                _headsetHandTotalSampleCount = 0;
                _headsetHandValidSampleCount = 0;
                _topviewHandTotalSampleCount = 0;
                _topviewHandValidSampleCount = 0;
                _lmStreamerPacketCount = 0;

                _handTracker.Start();
                _gazeTracker.Run();

                Task.Run(async () =>
                {
                    if (_lmsClient.IsConnected)
                    {
                        _lmsClient.Send(NET_COMMAND_START);
                    }

                    await Task.Delay(1000);

                    if (_nbtClient.IsConnected)
                    {
                        _nbtClient.Send(NET_COMMAND_START);
                    }
                    if (_cttClient.IsConnected)
                    {
                        _cttClient.Send(NET_COMMAND_START);
                    }
                });

                Stopwatch stopwatch = Stopwatch.StartNew();
                //List<double> durations = [];

                Console.WriteLine("Press Ctrl+C interrupt");
                Console.TreatControlCAsInput = true;
                while (!_hasFinished && !_hasInterrupted)
                {
                    if (!_gazeTracker.IsReady)  // debug mode
                    {
                        var start = stopwatch.Elapsed;
                        while ((stopwatch.Elapsed - start).TotalMilliseconds < 5)
                        {
                            Thread.Yield();
                        }
                        GazeTracker_Data(null, EyeHead.Empty);
                        //durations.Add((stopwatch.Elapsed - start).TotalMilliseconds);
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }

                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
                        {
                            _hasInterrupted = true;
                        }
                        break;
                    }
                }

                Console.TreatControlCAsInput = false;
                Console.WriteLine();

                if (_nbtClient.IsConnected)
                {
                    _nbtClient.Send(NET_COMMAND_STOP);
                }
                if (_cttClient.IsConnected)
                {
                    _cttClient.Send(NET_COMMAND_STOP);
                }
                if (_lmsClient.IsConnected)
                {
                    _lmsClient.Send(NET_COMMAND_STOP);
                }

                _handTracker.Stop();

                _gazeTracker.Data -= GazeTracker_Data;

                if (_settings.IsHiddenWhileTracking)
                {
                    WinUtils.ShowConsoleWindow();
                }

                if (!_hasInterrupted)
                {
                    Thread.Sleep(500);

                    //Console.WriteLine($"Cycle duration: {durations.Average():F4} ms");

                    PrintSessionStatistics();
                    int rating = GetRating();

                    _logger.Add("Rating", rating);
                    _logger.Save();

                    App.Debug.WriteLine($"RATING {rating}");
                }
            }
            else
            {
                Log("Not all devices are ready.");
                _hasInterrupted = true;
            }

            _gazeTracker.Dispose();
            _gazeTracker = null;

            if (_hasInterrupted)
                break;
        }

        Console.WriteLine("Exiting....");
    }

    public void Dispose()
    {
        _handTracker.Dispose();
        
        _nbtClient.Dispose();
        _cttClient.Dispose();
        _lmsClient.Dispose();

        GC.SuppressFinalize(this);
    }

    // Internal

    readonly string NET_COMMAND_SET_NBT_TASK = "set";
    readonly string NET_COMMAND_SET_CTT_LAMBDA = "lambda";
    readonly string NET_COMMAND_START = "start";
    readonly string NET_COMMAND_STOP = "stop";

    readonly HandLocation _headsetHandLocation = new();
    readonly HandLocation _topviewHandLocation = new();
    
    readonly Logger _logger = Logger.Instance;
    readonly NetClient _nbtClient = new();
    readonly NetClient _cttClient = new();
    readonly NetClient _lmsClient = new();
    readonly HandTracker _handTracker = new();
    readonly Settings _settings;

    string _nbackTaskMessage = "";

    GazeTracker? _gazeTracker = null;

    bool _hasFinished = false;
    bool _hasInterrupted = false;

    long _startTime = 0;
    int _gazeSampleCount = 0;
    int _headsetHandTotalSampleCount = 0;
    int _headsetHandValidSampleCount = 0;
    int _topviewHandTotalSampleCount = 0;
    int _topviewHandValidSampleCount = 0;
    int _lmStreamerPacketCount = 0;

    private static void Log(string info)
    {
        Console.WriteLine(info);
        App.Debug.WriteLine($"INFO {info}");
    }

    private static void HandleConnectionResult(string serviceName, NetClient client, Exception? ex)
    {
        string info;
        if (ex != null)
        {
            info = $"Cannot connect to {serviceName} on {client.IP}:{client.Port}. Is it running?\n  [{ex.Message}]";
        }
        else if (!client.IsConnected)
        {
            info = $"Cannot connect to {serviceName} on {client.IP}:{client.Port}. Is it running?";
        }
        else
        {
            info = $"Connected to {serviceName} on {client.IP}:{client.Port}.";
        }

        Log(info);
    }

    private static int GetRating()
    {
        Console.WriteLine("Overall, how difficult or easy did you find this task?");
        Console.WriteLine();
        Console.WriteLine("Very difficult                                        Very easy");
        Console.WriteLine("--- 1 ------ 2 ------ 3 ------ 4 ------ 5 ------ 6 ------ 7 ---");
        Console.WriteLine();

        int rating;
        for (; ; )
        {
            var input = Console.ReadLine();
            if (!int.TryParse(input, out rating) || rating < 1 || rating > 7)
            {
                Console.WriteLine("Please enter a number between 1 and 7.");
            }
            else
            {
                break;
            }
        }
        return rating;
    }

    private void PrintSessionStatistics()
    {
        var handLocalTrackingPercentage = (double)_headsetHandValidSampleCount / (_headsetHandTotalSampleCount > 0 ? _headsetHandTotalSampleCount : 1) * 100;
        var topViewHandTrackingPercentage = (double)_topviewHandValidSampleCount / (_topviewHandTotalSampleCount > 0 ? _topviewHandTotalSampleCount : 1) * 100;

        Log($"Gaze samples: {_gazeSampleCount}");
        Log($"Headset hand tracking samples: {_headsetHandTotalSampleCount}");
        Log($"Top-view hand tracking samples: {_topviewHandTotalSampleCount}");
        Log($"Valid top-view hand tracking percentage: {100 * _topviewHandTotalSampleCount / _lmStreamerPacketCount:F1}");
        Log($"Hand tracking percentage: {handLocalTrackingPercentage:F1} % (headset) / {topViewHandTrackingPercentage:F1} % (top-view)");
        Console.WriteLine();
    }

    // Event handlers

    private void NbtClient_Message(object? sender, string e)
    {
        lock (_nbackTaskMessage)
        {
            _nbackTaskMessage = e;
        }

        Console.WriteLine($"[NBT] Received: {e}");
        if (e.StartsWith("FIN"))
        {
            _hasFinished = true;
        }
    }

    private void LmsClient_Message(object? sender, string e)
    {
        _lmStreamerPacketCount++;

        var handLocation = HandLocation.FromJson(e);
        if (handLocation != null)
        {
            _topviewHandTotalSampleCount++;

            lock (_topviewHandLocation)
            {
                handLocation.CopyTo(_topviewHandLocation);

                if (!_topviewHandLocation.IsEmpty)
                {
                    _topviewHandValidSampleCount++;
                }
            }
        }
    }

    private void GazeTracker_Data(object? sender, EyeHead e)
    {
        _gazeSampleCount++;

        string eventInfo;
        lock (_nbackTaskMessage)
        {
            eventInfo = _nbackTaskMessage;
            _nbackTaskMessage = "";
        }

        if (_startTime == 0)
        {
            _startTime = e.Timestamp;
        }

        HandLocation headsetHandLocation;
        lock (_headsetHandLocation)
        {
            headsetHandLocation = _headsetHandLocation.Copy();
        }
        
        HandLocation topviewHandLocation;
        lock (_topviewHandLocation)
        {
            topviewHandLocation = _topviewHandLocation.Copy();
        }

        _logger.Add(e.Timestamp,
            e.Eye.Yaw.ToString("F4"), e.Eye.Pitch.ToString("F4"),
            e.Head.Yaw.ToString("F4"), e.Head.Pitch.ToString("F4"),
            e.Pupil.OpennessLeft.ToString("F4"), e.Pupil.SizeLeft.ToString("F4"),
            e.Pupil.OpennessRight.ToString("F4"), e.Pupil.SizeRight.ToString("F4"),
            headsetHandLocation.Palm.X.ToString("F2"), headsetHandLocation.Palm.Y.ToString("F2"), headsetHandLocation.Palm.Z.ToString("F2"),
            headsetHandLocation.Thumb.X.ToString("F2"), headsetHandLocation.Thumb.Y.ToString("F2"), headsetHandLocation.Thumb.Z.ToString("F2"),
            headsetHandLocation.Index.X.ToString("F2"), headsetHandLocation.Index.Y.ToString("F2"), headsetHandLocation.Index.Z.ToString("F2"),
            headsetHandLocation.Middle.X.ToString("F2"), headsetHandLocation.Middle.Y.ToString("F2"), headsetHandLocation.Middle.Z.ToString("F2"),
            topviewHandLocation.Palm.X.ToString("F2"), topviewHandLocation.Palm.Y.ToString("F2"), topviewHandLocation.Palm.Z.ToString("F2"),
            topviewHandLocation.Thumb.X.ToString("F2"), topviewHandLocation.Thumb.Y.ToString("F2"), topviewHandLocation.Thumb.Z.ToString("F2"),
            topviewHandLocation.Index.X.ToString("F2"), topviewHandLocation.Index.Y.ToString("F2"), topviewHandLocation.Index.Z.ToString("F2"),
            topviewHandLocation.Middle.X.ToString("F2"), topviewHandLocation.Middle.Y.ToString("F2"), topviewHandLocation.Middle.Z.ToString("F2"),
            eventInfo);

        if ((_gazeSampleCount % 50) == 0)
        {
            if (_settings.IsVerbose)
            {
                Console.WriteLine($"{e.Timestamp - _startTime}");
                Console.WriteLine($"   Gaze: {e.Eye.Yaw,-6:F1} {e.Eye.Pitch,-6:F1}");
                Console.WriteLine($"   Pupil: {e.Pupil.OpennessLeft,-6:F1} {e.Pupil.SizeLeft,-6:F1} {e.Pupil.OpennessRight,-6:F1} {e.Pupil.SizeRight,-6:F1}");
                Console.WriteLine($"   Head: {e.Head.Yaw,-6:F1} {e.Head.Pitch,-6:F1}");
                Console.WriteLine($"   Hand (Headset)");
                Console.WriteLine($"      Palm: {headsetHandLocation.Palm.X,-6:F1} {headsetHandLocation.Palm.Y,-6:F1} {headsetHandLocation.Palm.Z,-6:F1}");
                Console.WriteLine($"      Thumb: {headsetHandLocation.Thumb.X,-6:F1} {headsetHandLocation.Thumb.Y,-6:F1} {headsetHandLocation.Thumb.Z,-6:F1}");
                Console.WriteLine($"      Index: {headsetHandLocation.Index.X,-6:F1} {headsetHandLocation.Index.Y,-6:F1} {headsetHandLocation.Index.Z,-6:F1}");
                Console.WriteLine($"      Middle: {headsetHandLocation.Middle.X,-6:F1} {headsetHandLocation.Middle.Y,-6:F1} {headsetHandLocation.Middle.Z,-6:F1}");
                Console.WriteLine($"   Hand (TopView)");
                Console.WriteLine($"      Palm: {topviewHandLocation.Palm.X,-6:F1} {topviewHandLocation.Palm.Y,-6:F1} {topviewHandLocation.Palm.Z,-6:F1}");
                Console.WriteLine($"      Thumb: {topviewHandLocation.Thumb.X,-6:F1} {topviewHandLocation.Thumb.Y,-6:F1} {topviewHandLocation.Thumb.Z,-6:F1}");
                Console.WriteLine($"      Index: {topviewHandLocation.Index.X,-6:F1} {topviewHandLocation.Index.Y,-6:F1} {topviewHandLocation.Index.Z,-6:F1}");
                Console.WriteLine($"      Middle: {topviewHandLocation.Middle.X,-6:F1} {topviewHandLocation.Middle.Y,-6:F1} {topviewHandLocation.Middle.Z,-6:F1}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[{e.Timestamp - _startTime}] Gaze = {_gazeSampleCount}, Headset LM = {_headsetHandTotalSampleCount}, Topview LM = {_topviewHandTotalSampleCount}");
            }
        }
    }

    private void HandTracker_Data(object? sender, HandLocation e)
    {
        if (_gazeTracker == null)
            return;

        var handLocation = _handTracker.CompensateHeadRotation(_gazeTracker.HeadRotation, e);

        lock (_headsetHandLocation)
        {
            handLocation.CopyTo(_headsetHandLocation);
        }

        if (!e.Palm.IsZero)
        {
            _headsetHandValidSampleCount++;
        }

        _headsetHandTotalSampleCount++;
    }
}
