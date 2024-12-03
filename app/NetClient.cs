using System.Net.Sockets;
using System.Text;

namespace VarjoDataLogger;

public class NetClient : IDisposable
{
    public static int NBackTaskPort => 8963;
    public static int CttPort => 8964;

    public string IP { get; private set; } = "127.0.0.1";
    public int Port { get; private set; }
    public bool IsConnected { get; private set; } = false;

    public event EventHandler<string>? Message;

    public event EventHandler? Connected;
    public event EventHandler? Disconnected;

    public NetClient()
    {
        _client = new TcpClient();
    }

    public async Task<Exception?> Connect(string ip, int port, int timeout = 3000)
    {
        if (_readingThread is not null)
            return new Exception($"The client is connected already");

        IP = ip;
        Port = port;

        try
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(timeout);

            await _client.ConnectAsync(IP, Port, cts.Token);
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

    public void Send(string message)
    {
        var bytes = Encoding.ASCII.GetBytes(message + "\n");
        _stream?.WriteAsync(bytes, 0, bytes.Length);
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

    NetworkStream? _stream;
    Thread? _readingThread;

    private void ReadInLoop()
    {
        Connected?.Invoke(this, new EventArgs());

        _stream = _client.GetStream();
        var decoder = new System.Text.ASCIIEncoding();

        try
        {
            do
            {
                byte[] buffer = new byte[16];
                var byteCount = _stream.Read(buffer);

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

                Message?.Invoke(this, decoder.GetString(buffer.TakeWhile(b => b != '\0').ToArray()));

            } while (IsConnected);
        }
        catch { }

        IsConnected = false;    // Set it if the thread was closed internally by error
        _readingThread = null;

        _stream.Dispose();

        Disconnected?.Invoke(this, new EventArgs());
    }
}
