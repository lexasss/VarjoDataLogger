using Leap;

namespace VarjoDataLogger;

public class HandTracker : IDisposable
{
    /// <summary>
    /// Reports hand location as 3 vectors: palm, infdex finger tip and middle finger tip.
    /// The coordinate system is as it used to be in the original Leap Motion:
    /// X = left, Y = forward, Z = down
    /// </summary>
    public event EventHandler<HandLocation>? Data;

    public bool IsReady => _lm != null;

    /// <summary>
    /// Maximum distance for the hand to be tracked, in cm
    /// </summary>
    public double MaxDistance { get; set; } = 80;

    /// <summary>
    /// Three X, Y and Z letters:
    ///     First: uppercase = left, lowercase = right
    ///     Second: uppercase = forward, lowercase = backward
    ///     Third: uppercase = down, lowecase = up
    /// Default is XYZ, i.e. left, forward, down
    /// </summary>
    //public string CoordSystem { get; set; } = "XYZ";

    public HandTracker()
    {
        if (Settings.TryGetInstance(out _settings, out string? error))
        {
            _offsetY = _settings.LmOffset.y;
            _offsetZ = _settings.LmOffset.z;
           
            Console.WriteLine($"[LM] offsets: {_offsetX},{_offsetY},{_offsetZ}");

            //CoordSystem = _settings.LmCoords;
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

    /// <summary>
    /// Applies translation and ZYX-rotation of hand location to compensate for the head rotations
    /// </summary>
    /// <param name="headAngles">Head Euler angles</param>
    /// <param name="location">Hand location</param>
    /// <returns></returns>
    public HandLocation CompensateHeadRotation(Rotation headAngles, HandLocation location)
    {
        Vector Transform(Vector vec)
        {
            // translation
            var x = vec.Z + _offsetZ;
            var y = vec.X + _offsetX;
            var z = vec.Y + _offsetY;

            // preparing rotation
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

            // rotation
            var z_ = m11 * x + m12 * y + m13 * z;
            var x_ = m21 * x + m22 * y + m23 * z;
            var y_ = m31 * x + m32 * y + m33 * z;

            if (_settings.IsVerbose)
            {
                Console.WriteLine($"{a,-8:F2} {b,-8:F2} {c,-8:F2} | {vec.X,-8:F2} {vec.Y,-8:F2} {vec.Z,-8:F2} > {x_,-8:F2} {y_,-8:F2} {z_,-8:F2}");
            }

            return new Vector(x_, y_, z_);
        }

        return new HandLocation(
            Transform(location.Palm),
            Transform(location.Thumb),
            Transform(location.Index),
            Transform(location.Middle)
        );
    }

    // Internal

    readonly Settings _settings;

    readonly double _offsetX = 0;
    readonly double _offsetY = 15.0;   // cm
    readonly double _offsetZ = -6.0;   // cm

    LeapMotion? _lm = null;
    bool _isConnected = false;
    bool _isRunning = false;

    /*
    private float GetMm(ref Leap.Vector vector, int id) => CoordSystem[id] switch
    {
        'z' => -vector.z,
        'Z' => vector.z,
        'y' => -vector.y,
        'Y' => vector.y,
        'x' => -vector.x,
        'X' => vector.x,
        char c => throw new Exception($"Invalid axis '{c}'")
    };*/

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

        int handIndex = 0;
        while (handIndex < e.frame.Hands.Count && e.frame.Hands[handIndex].IsLeft)
        {
            handIndex++;
        }

        if (handIndex < e.frame.Hands.Count)
        {
            var palm = e.frame.Hands[handIndex].PalmPosition / 10;
            var fingers = e.frame.Hands[handIndex].Fingers;
            var thumb = fingers[0].TipPosition / 10;
            var index = fingers[1].TipPosition / 10;
            var middle = fingers[2].TipPosition / 10;

            if (Math.Sqrt(palm.x * palm.x + palm.y * palm.y + palm.z * palm.z) < MaxDistance)
            {
                handDetected = true;
                Data?.Invoke(this, new HandLocation(Vector.From(in palm), Vector.From(in thumb), Vector.From(in index), Vector.From(in middle)));
            }
        }

        if (!handDetected)
        {
            Data?.Invoke(this, new HandLocation());
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
