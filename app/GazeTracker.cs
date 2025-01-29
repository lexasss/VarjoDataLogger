using System.Runtime.InteropServices;

namespace VarjoDataLogger;

public partial class GazeTracker : IDisposable
{
    public event EventHandler<EyeHead>? Data;

    public bool IsReady => _isInitilized;

    public Rotation HeadRotation { get; private set; } = new(0, 0, 0);


    public GazeTracker()
    {
        _isInitilized = Interop.Init();
    }

    public void Run()
    {
        if (!_isInitilized || _isRunning)
            return;

        bool Callback(long timestamp,
            double eyeRotX, double eyeRotY, double eyeRotZ,
            double headPitch, double headYaw, double headRoll,
            float pupilOpennessLeft, float pupilSizeLeft,
            float pupilOpennessRight, float pupilSizeRight)
        {
            double oneOverZ = 1.0 / eyeRotZ;
            var yaw = RadiansToDegrees * Math.Atan(eyeRotX * oneOverZ);
            var pitch = RadiansToDegrees * Math.Atan(eyeRotY * oneOverZ);

            HeadRotation = new(headPitch, headYaw, headRoll);

            Data?.Invoke(this, new EyeHead(timestamp,
                new Rotation(pitch, yaw, 0),
                new Rotation(headPitch, headYaw, headRoll),
                new Pupil(pupilOpennessLeft, pupilSizeLeft, pupilOpennessRight, pupilSizeRight)));

            return _isRunning;
        }

        Interop.GazeCallback action = new(Callback);
        _thread = new Thread(() =>
        {
            _isRunning = true;

            Interop.Run(Marshal.GetFunctionPointerForDelegate(action));

            _isRunning = false;
        });
        _thread.Start();
    }

    public void Dispose()
    {
        _isRunning = false;

        Interop.Terminate();

        _thread?.Join();
        _thread = null;

        GC.SuppressFinalize(this);
    }

    // Internal

    const double RadiansToDegrees = 180.0 / Math.PI;

    readonly bool _isInitilized;

    bool _isRunning = false;
    Thread? _thread;


    // Interop

    static partial class Interop
    {
        private const string _dllImportPath = @"VarjoTrackerLib.dll";

        [LibraryImport(_dllImportPath)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool Init();

        public delegate bool GazeCallback(long timestamp,
            double eyeRotX, double eyeRotY, double eyeRotZ,
            double headPitch, double headYaw, double headRoll,
            float pupilOpennessLeft, float pupilSizeLeft,
            float pupilOpennessRight, float pupilSizeRight);

        [LibraryImport(_dllImportPath)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool Run(IntPtr cb);

        [LibraryImport(_dllImportPath)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        public static partial void Terminate();
    }
}
