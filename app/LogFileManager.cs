using System.Text.Json;

namespace VarjoDataLogger;

internal static class LogFileManager
{
    public static int LastParticipantId
    {
        get
        {
            var dirs = Directory.GetDirectories(_destinationFolder, "P??");
            var ids = dirs
                .Select(dir => Path.GetFileName(dir))
                .Where(name => name != null && name.StartsWith("P") && int.TryParse(name[1..], out _))
                .Select(name => int.Parse(name![1..]))
                .ToList();
            return ids.Count > 0 ? ids.Max() : 0;
        }
    }

    public static string GetParticipantFolder(int participantId) =>
        Path.Combine(_destinationFolder, $"P{participantId:00}");

    public static bool IsParticipantDataFull(int participantId)
    {
        var basepath = Path.Combine(_destinationFolder, $"P{participantId:00}");
        return Enum.GetNames(typeof(Pace)).All(pace =>
            Directory.Exists(Path.Combine(basepath, pace.ToLower()))
        );
    }

    public static void Collect(int participantId, Pace? pace)
    {
        if (participantId <= 0 || pace == null)
        {
            return;
        }

        var folder = Path.Combine(GetParticipantFolder(participantId), pace.ToString()?.ToLower() ?? "");
        Directory.CreateDirectory(folder);

        foreach (var (path, fileMask) in _fileMasks)
        {
            if (!Directory.Exists(path))
            {
                continue;
            }

            var files = Directory.GetFiles(path, fileMask);
            foreach (var file in files)
            {
                var filename = Path.GetFileName(file);
                var destPath = Path.Combine(folder, filename);

                try
                {
                    File.Move(file, destPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error moving file '{file}' to {destPath}: {ex.Message}");
                }
            }
        }
    }

    public static void SaveTemporaryLogFile(string filename, string content)
    {
        var path = Path.Combine(_destinationFolder, filename);
        File.WriteAllText(path, content);
    }

    public static void ClearTemporaryFiles()
    {
        foreach (var (path, fileMask) in _fileMasks)
        {
            if (!Directory.Exists(path))
            {
                continue;
            }
            var files = Directory.GetFiles(path, fileMask);
            foreach (var file in files)
            {
                try
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(file,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deleting file '{file}': {ex.Message}");
                }
            }
        }
    }

    // Internal

    record class FileMask(string Path, string Mask);
    record class Paths(string Destination, FileMask[] FilesMasks);

    static readonly string _destinationFolder = Path.Combine("..", "data");
    static readonly string _pathsFilename = "paths.json";

    static readonly FileMask[] _fileMasks = [
        new(Path.Combine("..", "data"), "*.txt"),
    ];

    static LogFileManager()
    {
        Directory.CreateDirectory(_destinationFolder);

        Paths? paths = null;

        try
        {
            paths = JsonSerializer.Deserialize<Paths?>(
                File.ReadAllText(_pathsFilename)
            );
        }
        catch { }

        if (paths != null)
        {
            _destinationFolder = paths.Destination;
            _fileMasks = paths.FilesMasks;
        }
        else
        {
            paths = new Paths(
                _destinationFolder,
                _fileMasks
            );
            File.WriteAllText(
                _pathsFilename,
                JsonSerializer.Serialize(paths, new JsonSerializerOptions { WriteIndented = true })
            );
        }
    }
}
