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

        try
        {
            using var recorder = new Recorder(settings);
            recorder.Run();
        }
        finally
        {
            Debug.Dispose();
        }
    }
}

class Recorder : IDisposable
{
    public Recorder(Settings settings)
    {
        _settings = settings;

        Task.Delay(500).Wait();
        AskParticipantId();

        _nbtClient.Message += NbtClient_Message;
        var nbackConnTask = _nbtClient.Connect(_settings.NBackTaskIP, NetClient.NBackTaskPort);
        nbackConnTask.Wait();
        HandleConnectionResult("N-Back task", _nbtClient, nbackConnTask.Result);

        _cttClient.Message += CttClient_Message; ;
        var cttConnTask = _cttClient.Connect(_settings.CttIP, NetClient.CttPort);
        cttConnTask.Wait();
        HandleConnectionResult("CTT", _cttClient, cttConnTask.Result);

        _lmsClient.Message += LmsClient_Message;
        var lmsConnTask = _lmsClient.Connect(_settings.LeapMotionStreamerIP, NetClient.LeapMotionStreamerPort);
        lmsConnTask.Wait();
        HandleConnectionResult("Leap Motion Streamer", _lmsClient, lmsConnTask.Result);

        _handTracker.Data += HandTracker_Data;
        _lmsUdpClient.DataReceived += LmsUdpClient_DataReceived;

        Task[] tasks = [
            RequestAndGetReply(_cttClient, NET_COMMAND_CTT_GET_LAMBDAS, () => _lambdas.Length > 0),
            RequestAndGetReply(_nbtClient, NET_COMMAND_NBT_GET_TASKS, () => _nbackTaskDescriptions.Length > 1)
        ];
        Task.WaitAll(tasks);

        if (_settings.Pace != null)
        {
            Task.Delay(200).Wait();
            _nbtClient.Send($"{NET_COMMAND_NTB_LOAD_PROFILE}{_settings.Pace}");
            Task.Delay(200).Wait();
        }
    }

    public void Run()
    {
        _hasInterrupted = false;

        var tasks = TaskSetup.Load(_settings.SetupFilename, _settings.TaskIndex).GetAllTasks();
        TaskSetup.SaveTo(_settings.LogFolder, tasks);

        if (_settings.Pace != null)
        {
            Log($"\nPace: {_settings.Pace}");
        }

        for (int i = 0; i < tasks.Length; i++)
        {
            _gazeTracker = new GazeTracker();

            var task = tasks[i];

            if (task.IsValid)
            {
                Console.WriteLine();

                _nbtClient.Send($"{NET_COMMAND_NBT_SET_TASK}{task.NBackTaskIndex}");
                _cttClient.Send($"{NET_COMMAND_CTT_SET_LAMBDA}{task.CttLambdaIndex}");

                var nbacktaskDescription = _nbackTaskDescriptions[Math.Min(_nbackTaskDescriptions.Length - 1, task.NBackTaskIndex)];
                var lambda = task.CttLambdaIndex < _lambdas.Length ? _lambdas[task.CttLambdaIndex] : task.CttLambdaIndex;
                var info = $"Task {i + 1}/{tasks.Length}: CTT = {lambda}, NBack = {nbacktaskDescription} [{task.NBackTaskIndex}]";
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
                {
                    _hasInterrupted = true;
                    break;
                }

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

                    _nbtClient.Send(NET_COMMAND_NBT_GET_LAST_LOG);
                    //Console.WriteLine($"Cycle duration: {durations.Average():F4} ms");

                    PrintSessionStatistics();
                    int rating = GetRating();

                    _logger.Add("Rating", rating);
                    _logger.Save();

                    App.Debug.WriteLine("RATING", $"{rating}");
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

        if (!_hasInterrupted)
        {
            LogFileManager.Collect(_settings.ParticipantID, _settings.Pace);
        }
        else
        {
            LogFileManager.ClearTemporaryFiles();
        }

        Console.WriteLine("Exiting....");
    }

    public void Dispose()
    {
        _handTracker.Dispose();
        
        _nbtClient.Dispose();
        _cttClient.Dispose();
        _lmsClient.Dispose();

        try
        {
            _lmsUdpClient.DataReceived -= LmsUdpClient_DataReceived;
        }
        catch { }
        _lmsUdpClient.Dispose();

        GC.SuppressFinalize(this);
    }

    // Internal

    readonly string NET_COMMAND_NBT_GET_TASKS = "tasks";
    readonly string NET_COMMAND_NBT_SET_TASK = "task";
    readonly string NET_COMMAND_NBT_GET_LAST_LOG = "getlog";
    readonly string NET_COMMAND_NTB_LOAD_PROFILE = "profile";
    readonly string NET_COMMAND_CTT_GET_LAMBDAS = "lambdas";
    readonly string NET_COMMAND_CTT_SET_LAMBDA = "lambda";
    readonly string NET_COMMAND_START = "start";
    readonly string NET_COMMAND_STOP = "stop";

    readonly HandLocation _headsetHandLocation = new();
    readonly HandLocation _topviewHandLocation = new();
    
    readonly Logger _logger = Logger.Instance;
    readonly NetClient _nbtClient = new();
    readonly NetClient _cttClient = new();
    readonly NetClient _lmsClient = new();
    readonly UdpReceiver _lmsUdpClient = new();
    readonly HandTracker _handTracker = new();
    readonly Settings _settings;

    string _nbackTaskMessage = "";
    string[] _nbackTaskDescriptions = ["Unknown"];
    double[] _lambdas = [];

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
        App.Debug.WriteLine("INFO", $"{info}");
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
        if (_lmStreamerPacketCount > 0)
            Log($"Valid top-view hand tracking percentage: {100 * _topviewHandTotalSampleCount / _lmStreamerPacketCount:F1}");
        Log($"Hand tracking percentage: {handLocalTrackingPercentage:F1} % (headset) / {topViewHandTrackingPercentage:F1} % (top-view)");
        Console.WriteLine();
    }

    private static async Task RequestAndGetReply(NetClient client, string request, Func<bool> hasReply)
    {
        if (!client.IsConnected)
            return;

        await Task.Delay(100);
        client.Send(request);

        using var cts = new CancellationTokenSource(3000);
        try
        {
            await Task.Run(() => {
                while (cts.Token.IsCancellationRequested == false)
                {
                    if (hasReply())
                    {
                        break;
                    }
                    Thread.Sleep(100);
                }
            }, cts.Token);
        }
        catch (TaskCanceledException)
        {
            Log($"Timeout for request '{request}'.");
        }
    }

    private void AskParticipantId()
    {
        var lastID = LogFileManager.LastParticipantId;
        if (lastID > 0)
        {
            Console.WriteLine($"The last participant ID is {lastID}");
        }
        Console.Write("Participant ID: ");

        for (; ; )
        {
            var input = Console.ReadLine();
            if (input == null)
            {
                throw new Exception("Participant ID is required.");
            }
            else if (string.IsNullOrWhiteSpace(input))
            {
                _settings.ParticipantID = 0;
                break;
            }
            else if (int.TryParse(input, out int pid) && pid > 0 && pid < 100)
            {
                if (LogFileManager.IsParticipantDataFull(pid))
                {
                    Console.Write("This participant has all data collected. Enter another ID: ");
                }
                else
                {
                    _settings.ParticipantID = pid;
                    break;
                }
            }
            else
            {
                Console.Write("Please enter a valid participant ID (1-99): ");
            }
        }

        App.Debug.WriteLine("INFO", $"Participant ID: {_settings.ParticipantID}");
    }

    // Event handlers

    private void CttClient_Message(object? sender, string e)
    {
        if (e.StartsWith("LMB") && e.Length > 3)
        {
            var items = new List<double>();
            foreach (var item in e.Substring(3).Split(';'))
            {
                if (double.TryParse(item, out double lambda))
                {
                    items.Add(lambda);
                }
            }
            _lambdas = items.ToArray();
        }
    }

    private void NbtClient_Message(object? sender, string e)
    {
        if (e.StartsWith("FIN"))
        {
            _hasFinished = true;
        }
        else if (e.StartsWith("TSK") && e.Length > 3)
        {
            var items = new List<string>();
            foreach (var item in e.Substring(3).Split(';'))
            {
                var p = item.Split(',');
                if (p.Length >= 2)
                {
                    var order = p[1] == "Ordered" ? "fixed" : "randomized";
                    items.Add($"{p[0]} {order} numbers");
                }
            }
            _nbackTaskDescriptions = items.ToArray();
        }
        else if (e.StartsWith("LOG"))
        {
            if (e.Length > 3)
            {
                var payload = e.Substring(3);
                LogFileManager.SaveTemporaryLogFile($"nbt-{DateTime.Now:u}.txt".ToPath(), payload);
            }
            e = e[..3];
        }

        lock (_nbackTaskMessage)
        {
            _nbackTaskMessage = e;
        }
    }

    private void LmsClient_Message(object? sender, string e)
    {
        _lmStreamerPacketCount++;
    }

    private void LmsUdpClient_DataReceived(object? sender, HandLocation handLocation)
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
