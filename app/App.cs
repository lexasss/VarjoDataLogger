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

        var nbtClient = new NetClient();
        var cttClient = new NetClient();

        var ht = new HandTracker();
        var gt = new GazeTracker();

        int i = 0;
        bool hasFinished = false;

        nbtClient.Message += (s, e) =>
        {
            lock (_nbackTaskMessage)
            {
                _nbackTaskMessage = e;
            }

            Console.WriteLine($"[NBT] Received: {e}");
            if (e.StartsWith("FIN"))
            {
                hasFinished = true;
            }
        };

        ht.Data += (s, e) =>
        {
            var handLoc = ht.CompensateHeadRotation(gt.HeadRotation, e);

            lock (_handLocation)
            {
                handLoc.CopyTo(_handLocation);
            }
        };

        gt.Data += (s, e) =>
        {
            string eventInfo;
            lock (_nbackTaskMessage)
            {
                eventInfo = _nbackTaskMessage;
                _nbackTaskMessage = "";
            }

            lock (_handLocation)
            {
                _logger.Add(e.Timestamp, e.Eye.Yaw, e.Eye.Pitch, e.Head.Yaw, e.Head.Pitch,
                    e.Pupil.OpennessLeft, e.Pupil.SizeLeft, e.Pupil.OpennessRight, e.Pupil.SizeRight,
                    _handLocation.Palm.X, _handLocation.Palm.Y, _handLocation.Palm.Z,
                    _handLocation.Thumb.X, _handLocation.Thumb.Y, _handLocation.Thumb.Z,
                    _handLocation.Index.X, _handLocation.Index.Y, _handLocation.Index.Z,
                    _handLocation.Middle.X, _handLocation.Middle.Y, _handLocation.Middle.Z,
                    eventInfo);

                if ((i++ % 30) == 0)
                {
                    Console.Write($"{e.Timestamp}\tGaze: {e.Eye.Yaw,-6:F1} {e.Eye.Pitch,-6:F1}");
                    Console.Write($"   Pupil: {e.Pupil.OpennessLeft,-6:F1} {e.Pupil.SizeLeft,-6:F1} {e.Pupil.OpennessRight,-6:F1} {e.Pupil.SizeRight,-6:F1}");
                    Console.Write($"   Head: {e.Head.Yaw,-6:F1} {e.Head.Pitch,-6:F1}");
                    Console.Write($"   Palm: {_handLocation.Palm.X,-6:F1} {_handLocation.Palm.Y,-6:F1} {_handLocation.Palm.Z,-6:F1}");
                    Console.Write($"   Thumb: {_handLocation.Thumb.X,-6:F1} {_handLocation.Thumb.Y,-6:F1} {_handLocation.Thumb.Z,-6:F1}");
                    Console.Write($"   Index: {_handLocation.Index.X,-6:F1} {_handLocation.Index.Y,-6:F1} {_handLocation.Index.Z,-6:F1}");
                    Console.Write($"   Middle: {_handLocation.Middle.X,-6:F1} {_handLocation.Middle.Y,-6:F1} {_handLocation.Middle.Z,-6:F1}");
                    Console.WriteLine();
                }
            }
        };

        Console.WriteLine();

        var nbackConnTask = nbtClient.Connect(settings.NBackTaskIP, NetClient.NBackTaskPort);
        nbackConnTask.Wait();

        Exception? ex = nbackConnTask.Result;
        if (ex != null)
        {
            Console.WriteLine($"Cannot connect to the N-Back task on {nbtClient.IP}:{nbtClient.Port}. Is it running?\n  [{ex.Message}]");
        }
        else if (!nbtClient.IsConnected)
        {
            Console.WriteLine($"Cannot connect to the N-Back task on {nbtClient.IP}:{nbtClient.Port}. Is it running?");
        }
        else
        {
            Console.WriteLine($"Connected to the N-Back task on {nbtClient.IP}:{nbtClient.Port}.");
        }

        var cttConnTask = cttClient.Connect(settings.CttIP, NetClient.CttPort);
        cttConnTask.Wait();

        ex = cttConnTask.Result;
        if (ex != null)
        {
            Console.WriteLine($"Cannot connect to the CTT on {cttClient.IP}:{cttClient.Port}. Is it running?\n  [{ex.Message}]");
        }
        else if (!cttClient.IsConnected)
        {
            Console.WriteLine($"Cannot connect to the CTT on {cttClient.IP}:{cttClient.Port}. Is it running?");
        }
        else
        {
            Console.WriteLine($"Connected to the CTT on {cttClient.IP}:{cttClient.Port}.");
        }

        if ((ht.IsReady && gt.IsReady) || settings.IsDebugMode)
        {
            Console.WriteLine("Press ENTER to start logging");
            Console.ReadLine();

            ht.Run();
            gt.Run();

            Task.Run(async () =>
            {
                await Task.Delay(1000);
                if (nbtClient.IsConnected)
                {
                    nbtClient.Send("start");
                }
                if (cttClient.IsConnected)
                {
                    cttClient.Send("start");
                }
            });

            Console.WriteLine("Press any key to interrupt");
            while (!hasFinished)
            {
                Thread.Sleep(100);
                if (Console.KeyAvailable)
                    break;
            }

            Console.WriteLine("Exiting....");

            if (nbtClient.IsConnected)
            {
                nbtClient.Send("stop");
            }
            if (cttClient.IsConnected)
            {
                cttClient.Send("stop");
            }

            ht.Dispose();
            gt.Dispose();

            _logger.Save();
        }
        else
        {
            Console.WriteLine("Not all devices are ready. Exiting...");
        }

        nbtClient.Dispose();
        cttClient.Dispose();
    }

    readonly static HandLocation _handLocation = new();

    readonly static Logger _logger = Logger.Instance;

    static string _nbackTaskMessage = "";
}
