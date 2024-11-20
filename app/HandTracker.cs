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

    public HandTracker()
    {
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
    }

    public void Dispose()
    {
        _lm?.Dispose();
        _lm = null;

        GC.SuppressFinalize(this);
    }

    // Internal

    LeapMotion? _lm = null;
    bool _isConnected = false;

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
        if (!_isConnected)
        {
            _isConnected = true;
        }

        bool handDetected = false;

        if (e.frame.Hands.Count > 0)
        {
            var x = e.frame.Hands[0].PalmPosition.x / 10;
            var y = e.frame.Hands[0].PalmPosition.y / 10;
            var z = e.frame.Hands[0].PalmPosition.z / 10;

            if (Math.Sqrt(x * x + y * y + z * z) < MaxDistance)
            {
                handDetected = true;
                Data?.Invoke(this, new Vector(x, y, z));
            }
        }

        if (!handDetected)
        {
            Data?.Invoke(this, new Vector());
        }

        // e.frame.Hands[0].Fingers[0..4].TipPosition.x;
        // e.frame.Hands[0].PalmVelocity.Magnitude);
        // e.frame.Hands[0].Fingers[x].TipPosition.DistanceTo(e.frame.Hands[0].Fingers[x + 1].TipPosition);
        // e.frame.Hands[0].PalmNormal.x
        // e.frame.Hands[iTrackHand].GrabAngle
        // e.frame.Hands[iTrackHand].PinchDistance
    }
}
