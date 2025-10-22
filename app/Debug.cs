namespace VarjoDataLogger;

internal class Debug : IDisposable
{
    public Debug()
    {
        if (!Directory.Exists(FOLDER_NAME))
            Directory.CreateDirectory(FOLDER_NAME);

        _stream = new(Path.Combine(FOLDER_NAME, $"debug-{DateTime.Now:u}.txt".ToPath()));
        _startTimestamp = DateTime.Now.Ticks;
    }

    public void WriteLine(string line)
    {
        _stream.WriteLine($"{(DateTime.Now.Ticks - _startTimestamp)/10000} {line}");
    }

    public void Dispose()
    {
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }

    // Internal

    readonly string FOLDER_NAME = "debug";

    readonly StreamWriter _stream;
    readonly long _startTimestamp;
}
