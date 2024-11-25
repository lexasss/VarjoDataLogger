using Leap;

namespace VarjoDataLogger;

public class HandTracker : IDisposable
{
    public event EventHandler<Vector>? Data;

    public bool IsReady => _lm != null;

    /// <summary>
    /// Maximum distance for the hand to be tracked, in cm
    /// </summary>
    public double MaxDistance { get; set; } = 80;

    public bool UseFinger { get; set; } = false;

    public HandTracker()
    {
        if (Settings.TryGetInstance(out Settings settings, out string? error))
        {
            _offsetY = settings.LeapMotionOffset.y;
            _offsetZ = settings.LeapMotionOffset.z;
        }

        try
        {
            _lm = new LeapMotion();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error connecting to LeapMotion: {e.Message}");
        }

        if (_lm != null)
        {
            // Ask for frames even in the background - this is important!
            _lm.SetPolicy(LeapMotion.PolicyFlag.POLICY_BACKGROUND_FRAMES);
            _lm.SetPolicy(LeapMotion.PolicyFlag.POLICY_ALLOW_PAUSE_RESUME);
            _lm.SetPolicy(LeapMotion.PolicyFlag.POLICY_OPTIMIZE_HMD);

            _lm.ClearPolicy(LeapMotion.PolicyFlag.POLICY_IMAGES);       // NO images, please

            // Subscribe to connected/not messages
            _lm.Connect += Lmc_Connect;
            _lm.Disconnect += Lmc_Disconnect;
            _lm.FrameReady += Lmc_FrameReady;
        }
    }

    public void Run()
    {
        if (_lm == null || _isConnected)
            return;

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

    private void Lmc_Disconnect(object? sender, ConnectionLostEventArgs e)
    {
        _isConnected = false;
    }

    private void Lmc_Connect(object? sender, ConnectionEventArgs e)
    {
        _isConnected = true;
    }

    private void Lmc_FrameReady(object? sender, FrameEventArgs e)
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

            var x = vector.x / 10;
            var y = vector.y / 10;
            var z = vector.z / 10;

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
}
