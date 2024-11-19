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

        var ht = new HandTracker();
        ht.Data += (s, e) =>
        {
            lock (_handLocation)
            {
                _handLocation.X = e.X;
                _handLocation.Y = e.Y;
                _handLocation.Z = e.Z;
            }
        };

        var gt = new GazeTracker();
        gt.Data += (s, e) =>
        {
            lock (_handLocation)
            {
                _logger.Add(e.Timestamp / 1000_000, e.Eye.Yaw, e.Eye.Pitch, e.Head.Yaw, e.Head.Pitch, _handLocation.X, _handLocation.Y, _handLocation.Z);
                Console.WriteLine($"{e.Timestamp / 1000_000}\tGaze: H={e.Eye.Yaw,-8:F2} V={e.Eye.Pitch,-8:F2}\tHead: H={e.Head.Yaw,-8:F2} V={e.Head.Pitch,-8:F2}\tHand: X={_handLocation.X,-8:F1}, Y={_handLocation.Y,-8:F1}, Z={_handLocation.Z,-8:F1}");
            }
        };

        Console.WriteLine();

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
    }

    readonly static Vector _handLocation = new(0, 0, 0);

    readonly static Logger _logger = Logger.Instance;
}
