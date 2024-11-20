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
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        Settings settings;

        try
        {
            settings = Settings.Instance;
        }
        catch (Exception ex_)
        {
            Console.WriteLine(ex_);
            return;
        }

        var nc = new NetClient();
        var ht = new HandTracker();
        var gt = new GazeTracker();

        nc.Message += (s, e) => { Console.WriteLine(e); };

        ht.Data += (s, e) =>
        {
            double x = e.X, y = e.Y, z = e.Z;
            gt.ConvertLeapMotionCoordsToVarjoCoords(ref x, ref y, ref z);

            lock (_handLocation)
            {
                _handLocation.X = x;
                _handLocation.Y = y;
                _handLocation.Z = z;
            }
        };

        gt.Data += (s, e) =>
        {
            lock (_handLocation)
            {
                _logger.Add(e.Timestamp, e.Eye.Yaw, e.Eye.Pitch, e.Head.Yaw, e.Head.Pitch, _handLocation.X, _handLocation.Y, _handLocation.Z);
                Console.WriteLine($"{e.Timestamp}\tGaze: {e.Eye.Yaw,-6:F1} {e.Eye.Pitch,-6:F1}     Head: {e.Head.Yaw,-6:F1} {e.Head.Pitch,-6:F1}     Hand: {_handLocation.X,-6:F1} {_handLocation.Y,-6:F1} {_handLocation.Z,-6:F1}");
            }
        };

        Console.WriteLine();

        var connectionTask = nc.Connect(settings.IP);
        connectionTask.Wait();

        Exception? ex = connectionTask.Result;
        if (ex != null)
        {
            Console.WriteLine($"Cannot connect to the N-Back task application {ex.Message}.\nIs N-Back task application running?");
        }
        else if (!nc.IsConnected)
        {
            Console.WriteLine("Cannot connect to the N-Back task application. Is it running?");
        }
        else
        {
            Console.WriteLine("Connect to the N-Back task application.");
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

    readonly static Vector _handLocation = new(0, 0, 0);

    readonly static Logger _logger = Logger.Instance;
}
