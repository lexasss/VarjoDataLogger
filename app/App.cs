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

        var nc = new NetClient();
        var ht = new HandTracker();
        var gt = new GazeTracker();

        int i = 0;
        string nbackTaskMessage = "";

        nc.Message += (s, e) => nbackTaskMessage = e;

        ht.Data += (s, e) =>
        {
            var palm = ht.ConvertLeapMotionCoordsToVarjoCoords(gt.HeadRotation, e.Palm);
            var thumb = ht.ConvertLeapMotionCoordsToVarjoCoords(gt.HeadRotation, e.Thumb);
            var index = ht.ConvertLeapMotionCoordsToVarjoCoords(gt.HeadRotation, e.Index);
            var middle = ht.ConvertLeapMotionCoordsToVarjoCoords(gt.HeadRotation, e.Middle);

            lock (_handLocation)
            {
                _handLocation.Palm = palm;
                _handLocation.Thumb = thumb;
                _handLocation.Index = index;
                _handLocation.Middle = middle;
            }
        };

        gt.Data += (s, e) =>
        {
            lock (_handLocation)
            {
                _logger.Add(e.Timestamp,
                    e.Eye.Yaw, e.Eye.Pitch,
                    e.Head.Yaw, e.Head.Pitch,
                    _handLocation.Palm.X, _handLocation.Palm.Y, _handLocation.Palm.Z,
                    _handLocation.Thumb.X, _handLocation.Thumb.Y, _handLocation.Thumb.Z,
                    _handLocation.Index.X, _handLocation.Index.Y, _handLocation.Index.Z,
                    _handLocation.Middle.X, _handLocation.Middle.Y, _handLocation.Middle.Z,
                    nbackTaskMessage);

                nbackTaskMessage = "";

                if ((i++ % 30) == 0)
                {
                    Console.Write($"{e.Timestamp}\tGaze: {e.Eye.Yaw,-6:F1} {e.Eye.Pitch,-6:F1}     Head: {e.Head.Yaw,-6:F1} {e.Head.Pitch,-6:F1}");
                    Console.Write($"   Palm: {_handLocation.Palm.X,-6:F1} {_handLocation.Palm.Y,-6:F1} {_handLocation.Palm.Z,-6:F1}");
                    Console.Write($"   Thumb: {_handLocation.Thumb.X,-6:F1} {_handLocation.Thumb.Y,-6:F1} {_handLocation.Thumb.Z,-6:F1}");
                    Console.Write($"   Index: {_handLocation.Index.X,-6:F1} {_handLocation.Index.Y,-6:F1} {_handLocation.Index.Z,-6:F1}");
                    Console.Write($"   Middle: {_handLocation.Middle.X,-6:F1} {_handLocation.Middle.Y,-6:F1} {_handLocation.Middle.Z,-6:F1}");
                    Console.WriteLine();
                }
            }
        };

        Console.WriteLine();

        var connectionTask = nc.Connect(settings.IP);
        connectionTask.Wait();

        Exception? ex = connectionTask.Result;
        if (ex != null)
        {
            Console.WriteLine($"Cannot connect to the N-Back task application on {nc.IP}:{nc.Port}. Is it running?\n  [{ex.Message}]");
        }
        else if (!nc.IsConnected)
        {
            Console.WriteLine($"Cannot connect to the N-Back task application on {nc.IP}:{nc.Port}. Is it running?");
        }
        else
        {
            Console.WriteLine($"Connected to the N-Back task on {nc.IP}:{nc.Port}.");
        }

        if (ht.IsReady && gt.IsReady)
        {
            Console.WriteLine("Press ENTER to start logging");
            Console.ReadLine();

            ht.Run();
            gt.Run();

            Console.ReadLine();

            ht.Dispose();
            gt.Dispose();

            _logger.Save();
        }
        else
        {
            Console.WriteLine("Not all devices are ready. Exiting...");
        }

        nc.Dispose();
    }

    readonly static HandLocation _handLocation = new();

    readonly static Logger _logger = Logger.Instance;
}
