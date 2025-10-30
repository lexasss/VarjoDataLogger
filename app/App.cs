using System.Diagnostics;
using System.Globalization;
using VarjoDataLogger.Study;

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
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
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

        if (!string.IsNullOrEmpty(_settings.StudySetupFilename))
        {
            var config = Configuration.Load(_settings.StudySetupFilename);
            if (config == null)
            {
                throw new Exception($"The configuration file was not found. A template was created in '{_settings.StudySetupFilename}', please review and edit as neccesary.");
            }

            _config = config;

            Task.Delay(500).Wait();
            _participantId = GetParticipantId(_config);

            var session = _config.CreateSession(_participantId);
            if (session == null)
            {
                Log($"Invalid configuration: check the '{_settings.StudySetupFilename}' file.");
                throw new Exception("Invalid configuration");
            }

            _session = session;
        }

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

        if (_cttClient.IsConnected)
        {
            Session.CttLambdas = [];
            RequestAndGetReply(_cttClient, NET_COMMAND_CTT_GET_LAMBDAS, () => Session.CttLambdas.Length > 0).Wait();
        }

        if (_nbtClient.IsConnected)
        {
            Session.NbtLayouts = [];
            RequestAndGetReply(_nbtClient, NET_COMMAND_NBT_GET_TASKS, () => Session.NbtLayouts.Length > 1).Wait();
            
            Task.Delay(300).Wait();
            Session.NbtProfiles = [];
            RequestAndGetReply(_nbtClient, NET_COMMAND_NBT_GET_PROFILES, () => Session.NbtProfiles.Length > 1).Wait();

            if (_session != null && !Session.NbtProfiles.Contains(_session.NbtProfile))
                throw new Exception($"NBackTask application has no `{_session.NbtProfile}` profile defined.");
        }
    }

    public void Run()
    {
        if (_session == null || _config == null)
        {
            RunSimple();
        }
        else
        {
            RunSession(_session);
        }
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

    readonly string NET_COMMAND_NBT_GET_PROFILES = "profiles";
    readonly string NET_COMMAND_NBT_GET_TASKS = "tasks";
    readonly string NET_COMMAND_NBT_SET_TASK = "task";
    readonly string NET_COMMAND_NBT_GET_LAST_LOG = "getlog";
    readonly string NET_COMMAND_NBT_LOAD_PROFILE = "profile";
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
    readonly Configuration? _config;
    readonly Session? _session;
    readonly int _participantId;

    string _nbtMessage = string.Empty;

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

    private void RunSimple(Action? afterFinished = null)
    {
        _gazeTracker = new GazeTracker();

        lock (_headsetHandLocation)
        {
            HandLocation.Empty.CopyTo(_headsetHandLocation);
        }
        lock (_topviewHandLocation)
        {
            HandLocation.Empty.CopyTo(_topviewHandLocation);
        }
        lock (_nbtMessage)
        {
            _nbtMessage = "";
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
                return;
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
                _lmsClient.Send(NET_COMMAND_START);

                await Task.Delay(1000);

                _nbtClient.Send(NET_COMMAND_START);
                _cttClient.Send(NET_COMMAND_START);

                if (!_nbtClient.IsConnected && _settings.IsDebugMode)
                {
                    await Task.Delay(10000);
                    NbtClient_Message(null, "FIN");
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

            _nbtClient.Send(NET_COMMAND_STOP);
            _cttClient.Send(NET_COMMAND_STOP);
            _lmsClient.Send(NET_COMMAND_STOP);

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

                afterFinished?.Invoke();

                _logger.Save();
            }
        }
        else
        {
            Log("Not all devices are ready.");
            _hasInterrupted = true;
        }

        _gazeTracker.Dispose();
        _gazeTracker = null;
    }

    private void RunSession(Session session)
    {
        session.SaveBlockOrder(_settings.LogFolder);

        if (!string.IsNullOrEmpty(session.NbtProfile))
        {
            Console.WriteLine();
            Log($"NBT profile: {session.NbtProfile}");

            Task.Delay(200).Wait();
            _nbtClient.Send($"{NET_COMMAND_NBT_LOAD_PROFILE}{session.NbtProfile}");
            Task.Delay(200).Wait();
        }

        _hasInterrupted = false;

        for (int i = 0; i < session.Blocks.Length; i++)
        {
            var block = session.Blocks[i];

            if (Session.IsValidBlock(block))
            {
                Console.WriteLine();

                _nbtClient.Send($"{NET_COMMAND_NBT_SET_TASK}{block.NbtLayoutIndex}");
                _cttClient.Send($"{NET_COMMAND_CTT_SET_LAMBDA}{block.CttLambdaIndex}");

                var nbtLayoutDescription = block.NbtLayoutIndex < Session.NbtLayouts.Length
                    ? Session.NbtLayouts[block.NbtLayoutIndex].AsDescription()
                    : "[unknown]";
                var lambda = block.CttLambdaIndex < Session.CttLambdas.Length
                    ? Session.CttLambdas[block.CttLambdaIndex]
                    : block.CttLambdaIndex;
                var info = $"Block {i + 1}/{session.Blocks.Length}: CTT = {lambda}, NBack = {nbtLayoutDescription}";
                Log(info);
            }

            RunSimple(() =>
            {
                foreach (var question in _config?.Questionnaires ?? [])
                {
                    Console.WriteLine(string.Join('\n', question.GetQuestionTextLines()));
                    string answer = question.ReadAnswer();
                    _logger.Add("Question", question.ID, answer);
                    App.Debug.WriteLine("QUESTION", $"{question.ID}\t{answer}");
                }
            });

            if (_hasInterrupted)
                break;
        }

        if (!_hasInterrupted)
        {
            Console.WriteLine();
            Console.WriteLine("Please stop all recordings and press ENTER");
            Console.ReadLine();

            LogFileManager.CollectFiles(session.ParticipantID, Configuration.GetSessionId(_participantId), session.NbtProfile);
        }
        else
        {
            LogFileManager.ClearTemporaryFiles();
        }

        Console.WriteLine("Exiting....");
    }

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

    private static int GetParticipantId(Configuration config)
    {
        int result = 0;

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
                break;
            }
            else if (int.TryParse(input, out int pid) && pid > 0 && pid < 100)
            {
                if (LogFileManager.IsParticipantDataFull(pid, config))
                {
                    Console.Write("This participant has all data collected. Enter another ID: ");
                }
                else
                {
                    result = pid;
                    break;
                }
            }
            else
            {
                Console.Write("Please enter a valid participant ID (1-99): ");
            }
        }

        App.Debug.WriteLine("INFO", $"Participant ID: {result}");

        return result;
    }

    // Event handlers

    private void CttClient_Message(object? sender, string e)
    {
        if (e.StartsWith("LMB") && e.Length > 3)
        {
            var items = new List<double>();
            foreach (var item in e[3..].Split(';'))
            {
                if (double.TryParse(item, out double lambda))
                {
                    items.Add(lambda);
                }
            }
            Session.CttLambdas = items.ToArray();
        }
    }

    private void NbtClient_Message(object? sender, string e)
    {
        if (e.StartsWith("FIN"))
        {
            _hasFinished = true;
        }
        else if (e.StartsWith("PRO") && e.Length > 3)
        {
            var items = new List<string>();
            foreach (var item in e[4..].Split(';'))
            {
                items.Add(item);
            }
            Session.NbtProfiles = items.ToArray();
        }
        else if (e.StartsWith("TSK") && e.Length > 3)
        {
            var items = new List<NbtLayout>();
            foreach (var item in e[4..].Split(';'))
            {
                var p = item.Split(',');
                if (p.Length >= 2 && int.TryParse(p[0], out int count))
                {
                    items.Add(new NbtLayout(count, p[1] != "Ordered"));
                }
            }
            Session.NbtLayouts = items.ToArray();
        }
        else if (e.StartsWith("LOG"))
        {
            if (e.Length > 3)
            {
                LogFileManager.SaveTemporaryLogFile($"nbt-{DateTime.Now:u}.txt".ToPath(), e[3..]);
            }
            e = e[..3];
        }

        lock (_nbtMessage)
        {
            _nbtMessage = e;
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
        lock (_nbtMessage)
        {
            eventInfo = _nbtMessage;
            _nbtMessage = "";
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
