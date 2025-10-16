using System.Globalization;

namespace VarjoDataLogger;

class App
{
    public static string Name => "Varjo Data Logger";

    public static void Main(string[] args)
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
                _nbtClient.Send($"{NET_COMMAND_SET_NBT_TASK}{task.NBackTaskIndex}");
                _cttClient.Send($"{NET_COMMAND_SET_CTT_LAMBDA}{task.CttLambdaIndex}");
                Console.WriteLine($"Task {i + 1}/{tasks.Length}: CTT = {task.CttLambdaIndex}, NBack = {task.NBackTaskIndex}");
            }

            _gazeSampleIndex = 0;
            _handLocalTotalSampleCount = 0;
            _handLocalValidSampleCount = 0;
            _handRemoteTotalSampleCount = 0;
            _handRemoteValidSampleCount = 0;

            _hasFinished = false;

            if ((_handTracker.IsReady && _gazeTracker.IsReady) || _settings.IsDebugMode)
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine($"Press ENTER to start");
                var cmd = Console.ReadLine();

                if (cmd == null || _hasInterrupted)
                    break;

                _gazeTracker.Data += GazeTracker_Data;

                if (_settings.IsHiddenWhileTracking)
                {
                    WinUtils.HideConsoleWindow();
                }

                _handLocalTotalSampleCount = 0;
                _handLocalValidSampleCount = 0;
                _handRemoteTotalSampleCount = 0;
                _handRemoteValidSampleCount = 0;

                _handTracker.Start();
                _gazeTracker.Run();

                Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    if (_nbtClient.IsConnected)
                    {
                        _nbtClient.Send(NET_COMMAND_START);
                    }
                    if (_cttClient.IsConnected)
                    {
                        _cttClient.Send(NET_COMMAND_START);
                    }
                    if (_lmsClient.IsConnected)
                    {
                        _lmsClient.Send(NET_COMMAND_START);
                    }
                });

                Console.WriteLine("Press any key to interrupt");
                Console.TreatControlCAsInput = true;
                while (!_hasFinished && !_hasInterrupted)
                {
                    Thread.Sleep(100);
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

                    var handLocalTrackingPercentage = (double)_handLocalValidSampleCount / (_handLocalTotalSampleCount > 0 ? _handLocalTotalSampleCount : 1) * 100;
                    var handRemoteTrackingPercentage = (double)_handRemoteValidSampleCount / (_handRemoteTotalSampleCount > 0 ? _handRemoteTotalSampleCount : 1) * 100;
                    Console.WriteLine($"Hand tracking percentage: {handLocalTrackingPercentage:F1} % (local) / {handRemoteTrackingPercentage:F1} % (remote)");

                    _logger.Save();

                    Thread.Sleep(1000); // Give some time for the trackers to finalize
                }
            }
            else
            {
                Console.WriteLine("Not all devices are ready.");
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

    readonly HandLocation _handLocation = new();
    readonly Logger _logger = Logger.Instance;
    readonly NetClient _nbtClient = new();
    readonly NetClient _cttClient = new();
    readonly NetClient _lmsClient = new();
    readonly HandTracker _handTracker = new();
    readonly Settings _settings;

    string _nbackTaskMessage = "";
    string _lmsData = "";

    GazeTracker? _gazeTracker = null;

    bool _hasFinished = false;
    bool _hasInterrupted = false;

    int _gazeSampleIndex = 0;
    int _handLocalTotalSampleCount = 0;
    int _handLocalValidSampleCount = 0;
    int _handRemoteTotalSampleCount = 0;
    int _handRemoteValidSampleCount = 0;


    private void HandleConnectionResult(string serviceName, NetClient client, Exception? ex)
    {
        if (ex != null)
        {
            Console.WriteLine($"Cannot connect to {serviceName} on {client.IP}:{client.Port}. Is it running?\n  [{ex.Message}]");
        }
        else if (!client.IsConnected)
        {
            Console.WriteLine($"Cannot connect to {serviceName} on {client.IP}:{client.Port}. Is it running?");
        }
        else
        {
            Console.WriteLine($"Connected to {serviceName} on {client.IP}:{client.Port}.");
        }
    }

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
        lock (_lmsData)
        {
            _lmsData = e;
        }
    }

    private void GazeTracker_Data(object? sender, EyeHead e)
    {
        string eventInfo;
        lock (_nbackTaskMessage)
        {
            eventInfo = _nbackTaskMessage;
            _nbackTaskMessage = "";
        }


        HandLocation handLocationRemote;
        lock (_lmsData)
        {
            _handRemoteTotalSampleCount++;

            handLocationRemote = HandLocation.FromJson(_lmsData);
            if (!handLocationRemote.IsEmpty)
            {
                _handRemoteValidSampleCount++;
            }
        }

        lock (_handLocation)
        {
            _logger.Add(e.Timestamp, e.Eye.Yaw, e.Eye.Pitch, e.Head.Yaw, e.Head.Pitch,
                e.Pupil.OpennessLeft, e.Pupil.SizeLeft, e.Pupil.OpennessRight, e.Pupil.SizeRight,
                _handLocation.Palm.X, _handLocation.Palm.Y, _handLocation.Palm.Z,
                _handLocation.Thumb.X, _handLocation.Thumb.Y, _handLocation.Thumb.Z,
                _handLocation.Index.X, _handLocation.Index.Y, _handLocation.Index.Z,
                _handLocation.Middle.X, _handLocation.Middle.Y, _handLocation.Middle.Z,
                handLocationRemote.Palm.X, handLocationRemote.Palm.Y, handLocationRemote.Palm.Z,
                handLocationRemote.Thumb.X, handLocationRemote.Thumb.Y, handLocationRemote.Thumb.Z,
                handLocationRemote.Index.X, handLocationRemote.Index.Y, handLocationRemote.Index.Z,
                handLocationRemote.Middle.X, handLocationRemote.Middle.Y, handLocationRemote.Middle.Z,
                eventInfo);

            if ((_gazeSampleIndex++ % 60) == 0)
            {
                Console.WriteLine($"{e.Timestamp}");
                Console.WriteLine($"   Gaze: {e.Eye.Yaw,-6:F1} {e.Eye.Pitch,-6:F1}");
                Console.WriteLine($"   Pupil: {e.Pupil.OpennessLeft,-6:F1} {e.Pupil.SizeLeft,-6:F1} {e.Pupil.OpennessRight,-6:F1} {e.Pupil.SizeRight,-6:F1}");
                Console.WriteLine($"   Head: {e.Head.Yaw,-6:F1} {e.Head.Pitch,-6:F1}");
                Console.WriteLine($"   Hand (Headset)");
                Console.WriteLine($"      Palm: {_handLocation.Palm.X,-6:F1} {_handLocation.Palm.Y,-6:F1} {_handLocation.Palm.Z,-6:F1}");
                Console.WriteLine($"      Thumb: {_handLocation.Thumb.X,-6:F1} {_handLocation.Thumb.Y,-6:F1} {_handLocation.Thumb.Z,-6:F1}");
                Console.WriteLine($"      Index: {_handLocation.Index.X,-6:F1} {_handLocation.Index.Y,-6:F1} {_handLocation.Index.Z,-6:F1}");
                Console.WriteLine($"      Middle: {_handLocation.Middle.X,-6:F1} {_handLocation.Middle.Y,-6:F1} {_handLocation.Middle.Z,-6:F1}");
                Console.WriteLine($"   Hand (TopView)");
                Console.WriteLine($"      Palm: {handLocationRemote.Palm.X,-6:F1} {handLocationRemote.Palm.Y,-6:F1} {handLocationRemote.Palm.Z,-6:F1}");
                Console.WriteLine($"      Thumb: {handLocationRemote.Thumb.X,-6:F1} {handLocationRemote.Thumb.Y,-6:F1} {handLocationRemote.Thumb.Z,-6:F1}");
                Console.WriteLine($"      Index: {handLocationRemote.Index.X,-6:F1} {handLocationRemote.Index.Y,-6:F1} {handLocationRemote.Index.Z,-6:F1}");
                Console.WriteLine($"      Middle: {handLocationRemote.Middle.X,-6:F1} {handLocationRemote.Middle.Y,-6:F1} {handLocationRemote.Middle.Z,-6:F1}");
            }
        }
    }

    private void HandTracker_Data(object? sender, HandLocation e)
    {
        if (_gazeTracker == null)
            return;

        var handLoc = _handTracker.CompensateHeadRotation(_gazeTracker.HeadRotation, e);

        lock (_handLocation)
        {
            handLoc.CopyTo(_handLocation);
        }

        if (!e.Palm.IsZero)
        {
            _handLocalValidSampleCount++;
        }

        _handLocalTotalSampleCount++;
    }
}
