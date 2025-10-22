using System.Net;
using System.Net.Sockets;
using System.Text;

namespace VarjoDataLogger;

public class UdpReceiver : IDisposable
{
    public int Port { get; } = 8982;

    public event EventHandler<HandLocation>? DataReceived;

    public UdpReceiver()
    {
        _client = new UdpClient(new IPEndPoint(IPAddress.Any, Port));
        _listeningTask = Task.Run(ListenLoopAsync, _cts.Token);
    }

    public void Dispose()
    {
        _cts.Cancel();

        try
        {
            _listeningTask?.Wait(1000);
        }
        catch { }

        _client.Dispose();
        _cts.Dispose();

        GC.SuppressFinalize(this);
    }

    // Internal

    readonly UdpClient _client;
    readonly CancellationTokenSource _cts = new();
    readonly Task? _listeningTask;

    private async Task ListenLoopAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                UdpReceiveResult result;
                try
                {
                    result = await _client.ReceiveAsync(_cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    // Ignore receive errors and continue listening
                    continue;
                }

                string payload;
                try
                {
                    payload = Encoding.UTF8.GetString(result.Buffer);
                }
                catch (Exception)
                {
                    continue;
                }

                HandLocation? location;
                try
                {
                    location = HandLocation.FromJson(payload);
                }
                catch (Exception)
                {
                    // ignore parse exceptions
                    location = null;
                }

                if (location != null)
                {
                    DataReceived?.Invoke(this, location);
                }
            }
        }
        finally
        {
            // ensure client closed if loop exits
            try { _client.Close(); }
            catch { }
        }
    }

}
