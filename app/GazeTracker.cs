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

        void Callback(long timestamp, double eyeRotX, double eyeRotY, double eyeRotZ, double headPitch, double headYaw, double headRoll)
        {
            double oneOverZ = 1.0 / eyeRotZ;
            var yaw = RadiansToDegrees * Math.Atan(eyeRotX * oneOverZ);
            var pitch = RadiansToDegrees * Math.Atan(eyeRotY * oneOverZ);

            HeadRotation = new(headPitch, headYaw, headRoll);

            Data?.Invoke(this, new EyeHead(timestamp, new Rotation(pitch, yaw, 0), new Rotation(headPitch, headYaw, headRoll)));
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
        _thread?.Interrupt();
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
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool Init();

        public delegate void GazeCallback(long timestamp, double eyeRotX, double eyeRotY, double eyeRotZ, double headPitch, double headYaw, double headRoll);

        [LibraryImport(_dllImportPath)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool Run(IntPtr cb);
    }
}
