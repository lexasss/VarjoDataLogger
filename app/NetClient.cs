using System.Net.Sockets;

namespace VarjoDataLogger;

public class NetClient : IDisposable
{
    public bool IsConnected { get; private set; } = false;

    public event EventHandler<string>? Message;

    public event EventHandler? Connected;
    public event EventHandler? Disconnected;

    public NetClient()
    {
        _client = new TcpClient();
    }

    public async Task<Exception?> Connect(string ip, int port = 8963, int timeout = 3000)
    {
        if (_readingThread is not null)
            return new Exception($"The client is connected already");

        try
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(timeout);

            await _client.ConnectAsync(ip, port, cts.Token);
            IsConnected = true;

            _readingThread = new Thread(ReadInLoop);
            _readingThread.Start();
        }
        catch (SocketException ex)
        {
            return ex;
        }
        catch (OperationCanceledException)
        {
            return new TimeoutException($"Timeout in {timeout} ms.");
        }

        return null;
    }

    public async Task Stop()
    {
        IsConnected = false;

        await Task.Run(() =>
        {
            _client.Close(); 
            _readingThread?.Join();
        }); ;
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    // Internal

    readonly TcpClient _client;

    Thread? _readingThread;

    private void ReadInLoop()
    {
        Connected?.Invoke(this, new EventArgs());

        NetworkStream stream = _client.GetStream();
        var decoder = new System.Text.ASCIIEncoding();

        try
        {
            do
            {
                byte[] buffer = new byte[16];
                var byteCount = stream.Read(buffer);

                if (byteCount == 0)
                {
                    break;
                }

                for (int i = byteCount - 1; i > 0; i--)
                {
                    if (buffer[i] == 10 || buffer[i] == 13)
                    {
                        buffer[i] = 0;
                    }
                    else
                    {
                        break;
                    }
                }


                Message?.Invoke(this, decoder.GetString(buffer));

            } while (IsConnected);
        }
        catch { }

        IsConnected = false;    // Set it if the thread was closed internally by error
        _readingThread = null;

        stream.Dispose();

        Disconnected?.Invoke(this, new EventArgs());
    }
}
