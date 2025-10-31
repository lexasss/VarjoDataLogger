namespace VarjoDataLogger.Study;

internal static class LogFileManager
{
    public static int LastParticipantId
    {
        get
        {
            var dirs = Directory.GetDirectories(_destinationFolder, "P??");
            var ids = dirs
                .Select(dir => Path.GetFileName(dir))
                .Where(name => name != null && name.StartsWith('P') && int.TryParse(name[1..], out _))
                .Select(name => int.Parse(name![1..]))
                .ToList();
            return ids.Count > 0 ? ids.Max() : 0;
        }
    }

    public static void Init(Paths paths)
    {
        _destinationFolder = paths.Destination;
        _fileMasks = paths.FilesMasks;

        Directory.CreateDirectory(_destinationFolder);
    }

    public static string GetParticipantFolder(int participantId) =>
        Path.Combine(_destinationFolder, $"P{participantId:00}");

    public static bool IsParticipantDataFull(int participantId, Configuration config)
    {
        var participantFolder = Path.Combine(_destinationFolder, $"P{participantId:00}");
        if (!Directory.Exists(participantFolder))
            return false;

        var folders = Directory.EnumerateDirectories(participantFolder);
        var sessionSetsAndNbtProfiles = config.Sets[participantId % config.Sets.Length];
        return folders.Count() == sessionSetsAndNbtProfiles.Length;
    }

    public static void CollectFiles(int participantId, int sessionId, string nbtProfile)
    {
        if (participantId <= 0 || string.IsNullOrEmpty(nbtProfile))
        {
            return;
        }

        var folder = Path.Combine(GetParticipantFolder(participantId), $"{sessionId} - {nbtProfile.ToLower()}");
        Directory.CreateDirectory(folder);

        foreach (var (path, fileMask) in _fileMasks)
        {
            if (!Directory.Exists(path))
            {
                continue;
            }

            int moved = 0;

            var files = Directory.GetFiles(path, fileMask);
            foreach (var file in files)
            {
                var filename = Path.GetFileName(file);
                var destPath = Path.Combine(folder, filename);

                try
                {
                    File.Move(file, destPath);
                    moved++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error moving file '{file}' to {destPath}: {ex.Message}");
                }
            }

            Console.WriteLine($"Moved {moved}/{files.Length} files from the folder {path}");
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

    static string _destinationFolder = "data";
    static FileMask[] _fileMasks = [];
}
