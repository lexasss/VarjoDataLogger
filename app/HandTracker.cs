using Leap;

namespace VarjoDataLogger;

public class HandTracker : IDisposable
{
    /// <summary>
    /// Reports hand location. The coordinate system is as it used to be in the original Leap Motion:
    /// X = left, Y = forward, Z = down
    /// </summary>
    public event EventHandler<Vector>? Data;

    public bool IsReady => _lm != null;

    /// <summary>
    /// Maximum distance for the hand to be tracked, in cm
    /// </summary>
    public double MaxDistance { get; set; } = 80;

    public bool UseFinger { get; set; } = false;

    /// <summary>
    /// Three X, Y and Z letters:
    ///     First: uppercase = left, lowercase = right
    ///     Second: uppercase = forward, lowercase = backward
    ///     Third: uppercase = down, lowecase = up
    /// Default is XYZ, i.e. left, forward, down
    /// </summary>
    public string CoordSystem { get; set; } = "XYZ";

    public HandTracker()
    {
        if (Settings.TryGetInstance(out Settings settings, out string? error))
        {
            _offsetY = settings.LmOffset.y;
            _offsetZ = settings.LmOffset.z;

            Console.WriteLine($"[LM] offsets: {_offsetX},{_offsetY},{_offsetZ}");

            CoordSystem = settings.LmCoords;
        }

        try
        {
            _lm = new LeapMotion();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to connect to LeapMotion: {e.Message}");
        }

        if (_lm != null)
        {
            _lm.Connect += Lm_Connect;
            _lm.Disconnect += Lm_Disconnect;
            _lm.FrameReady += Lm_FrameReady;

            _lm.Device += Lm_Device;
            _lm.DeviceLost += Lm_DeviceLost;
            _lm.DeviceFailure += Lm_DeviceFailure;

            // Ask for frames even in the background - this is important!
            _lm.SetPolicy(LeapMotion.PolicyFlag.POLICY_BACKGROUND_FRAMES);
            _lm.SetPolicy(LeapMotion.PolicyFlag.POLICY_ALLOW_PAUSE_RESUME);
            _lm.SetPolicy(LeapMotion.PolicyFlag.POLICY_OPTIMIZE_HMD);

            _lm.ClearPolicy(LeapMotion.PolicyFlag.POLICY_IMAGES);       // NO images, please
        }
    }

    public void Run()
    {
        if (_lm == null || _isConnected)
            return;

        if (_lm.Devices.Count == 0)
        {
            Console.WriteLine("[LM] Found no devices");
        }

        if (_lm.IsConnected)
        {
            _isConnected = true;
        }
        else
        {
            _lm.StartConnection();
        }

        _isRunning = true;
    }

    public void Dispose()
    {
        _lm?.Dispose();
        _lm = null;

        GC.SuppressFinalize(this);
    }

    public Vector ConvertLeapMotionCoordsToVarjoCoords(Rotation headAngles, Vector location)
    {
        var x = location.Z + _offsetZ;
        var y = location.X + _offsetX;
        var z = location.Y + _offsetY;

        var a = -headAngles.Yaw.ToRad();
        var b = -headAngles.Pitch.ToRad();
        var c = -headAngles.Roll.ToRad();

        var sinA = Math.Sin(a);
        var sinB = Math.Sin(b);
        var sinC = Math.Sin(c);
        var cosA = Math.Cos(a);
        var cosB = Math.Cos(b);
        var cosC = Math.Cos(c);

        var m11 = cosB * cosC;
        var m21 = cosB * sinC;
        var m31 = -sinB;

        var m12 = sinA * sinB * cosC - cosA * sinC;
        var m22 = sinA * sinB * sinC + cosA * cosC;
        var m32 = sinA * cosB;

        var m13 = cosA * sinB * cosC + sinA * sinC;
        var m23 = cosA * sinB * sinC - sinA * cosC;
        var m33 = cosA * cosB;

        var z_ = m11 * x + m12 * y + m13 * z - _offsetZ;
        var x_ = m21 * x + m22 * y + m23 * z - _offsetX;
        var y_ = m31 * x + m32 * y + m33 * z - _offsetY;

        //Console.WriteLine($"{a,-8:F2} {b,-8:F2} {c,-8:F2} | {location.X,-8:F2} {location.Y,-8:F2} {location.Z,-8:F2} > {x_,-8:F2} {y_,-8:F2} {z_,-8:F2}");

        return new Vector(x_, y_, z_);
    }

    // Internal

    double _offsetX = 0;
    double _offsetY = 15.99;
    double _offsetZ = -1.12;

    LeapMotion? _lm = null;
    bool _isConnected = false;
    bool _isRunning = false;

    private float GetCm(ref Leap.Vector vector, int id) => CoordSystem[id] switch
    {
        'z' => -vector.z,
        'Z' => vector.z,
        'y' => -vector.y,
        'Y' => vector.y,
        'x' => -vector.x,
        _ => vector.x
    } / 10;

    private void Lm_Disconnect(object? sender, ConnectionLostEventArgs e)
    {
        _isConnected = false;
    }

    private void Lm_Connect(object? sender, ConnectionEventArgs e)
    {
        _isConnected = true;
    }

    private void Lm_FrameReady(object? sender, FrameEventArgs e)
    {
        if (!_isRunning)
            return;

        if (!_isConnected)
            _isConnected = true;

        bool handDetected = false;

        if (e.frame.Hands.Count > 0)
        {
            var vector = UseFinger
                ? e.frame.Hands[0].Fingers[1].TipPosition
                : e.frame.Hands[0].PalmPosition;

            var x = GetCm(ref vector, 0);
            var y = GetCm(ref vector, 1);
            var z = GetCm(ref vector, 2);

            if (Math.Sqrt(x * x + y * y + z * z) < MaxDistance)
            {
                handDetected = true;
                Data?.Invoke(this, new Vector(x, y, z));
            }
        }

        if (!handDetected)
        {
            Data?.Invoke(this, Vector.Zero);
        }

        // e.frame.Hands[0].Fingers[0..4].TipPosition.x;
        // e.frame.Hands[0].PalmVelocity.Magnitude);
        // e.frame.Hands[0].Fingers[x].TipPosition.DistanceTo(e.frame.Hands[0].Fingers[x + 1].TipPosition);
        // e.frame.Hands[0].PalmNormal.x
        // e.frame.Hands[iTrackHand].GrabAngle
        // e.frame.Hands[iTrackHand].PinchDistance
    }

    private void Lm_DeviceFailure(object? sender, DeviceFailureEventArgs e)
    {
        Console.WriteLine($"[LM] Device {e.DeviceSerialNumber} failure: {e.ErrorMessage} ({e.ErrorCode})");
    }

    private void Lm_DeviceLost(object? sender, DeviceEventArgs e)
    {
        Console.WriteLine($"[LM] Device {e.Device.SerialNumber} was lost");
    }

    private void Lm_Device(object? sender, DeviceEventArgs e)
    {
        Console.WriteLine($"[LM] Found device {e.Device.SerialNumber}");
    }
}
