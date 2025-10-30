using System.Text.Json;

namespace VarjoDataLogger.Study;

// Serializable classes

public record class Block(int CttLambdaIndex, int NbtLayoutIndex);

public record class SessionSetupAndNbtProfile(int SessionSetupIndex, int NbtProfileIndex);

public class SessionSetup
{
    public bool Randomized { get; set; } = false;
    public int Repetitions { get; set; } = 1;
    public int[] CttLambdaIndexes { get; set; } = [ 0 ];
    public int[] NbtLayoutIndexes { get; set; } = [ 0 ];

    public Block[] CreateBlocks()
    {
        var result = new List<Block>();
        for (int i = 0; i < Repetitions; i++)
        {
            foreach (var lambdaIndex in CttLambdaIndexes)
            {
                foreach (var nbtLayoutIndex in NbtLayoutIndexes)
                {
                    result.Add(new Block(lambdaIndex, nbtLayoutIndex));
                }
            }
        }

        if (Randomized)
        {
            var rnd = new Random((int)DateTime.Now.Ticks);
            rnd.Shuffle(result);
        }

        return result.ToArray();
    }
}

public class Configuration
{
    public Questionnaire[] Questionnaires { get; set; } = [];
    public SessionSetup[] SessionSetups { get; set; } = [];
    public string[] NbtProfiles { get; set; } = [];
    public SessionSetupAndNbtProfile[][] Sets { get; set; } = [];

    public static int GetSessionId(int participantId)
    {
        int index = 0;

        var participantFolder = LogFileManager.GetParticipantFolder(participantId);
        if (Directory.Exists(participantFolder))
        {
            var folders = Directory.EnumerateDirectories(participantFolder);
            index = folders.Count();
        }

        return index;
    }

    public Session? CreateSession(int participantId)
    {
        if (participantId < 1)
            return null;

        var sessionSetsAndNbtProfiles = Sets[(participantId - 1) % Sets.Length];
        int index = GetSessionId(participantId);

        var sessionSetAndNbtProfile = sessionSetsAndNbtProfiles[index];
        if (sessionSetAndNbtProfile.SessionSetupIndex < 0 || sessionSetAndNbtProfile.SessionSetupIndex >= SessionSetups.Length)
            return null;

        var sessionSetup = SessionSetups[sessionSetAndNbtProfile.SessionSetupIndex];
        var nbtProfile = NbtProfiles[sessionSetAndNbtProfile.NbtProfileIndex];
        return new Session(participantId, sessionSetup.CreateBlocks(), nbtProfile);
    }

    public static Configuration? Load(string? filename)
    {
        if (!string.IsNullOrEmpty(filename))
        {
            if (File.Exists(filename))
            {
                try
                {
                    var json = File.ReadAllText(filename);
                    return JsonSerializer.Deserialize<Configuration>(json) ?? new Configuration();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load the study configuration from '{filename}': {ex.Message}");
                }
            }
            else
            {
                var config = new Configuration();

                try
                {
                    var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(filename, json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to save the study configuration to '{filename}': {ex.Message}");
                }

                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Do not use the constructor explicitely,
    /// always load it from a file usinf <see cref="Load(string?)"/>
    /// </summary>
    public Configuration()
    {
        if (Sets.Length == 0)
            Sets = [        // default sets blocks IDs
                [ new(0, 0), new(4, 1) ],
                [ new(5, 1), new(1, 0) ],
                [ new(2, 0), new(4, 1) ],
                [ new(5, 1), new(3, 0) ],
                [ new(0, 0), new(5, 1) ],
                [ new(4, 1), new(1, 0) ],
                [ new(2, 0), new(5, 1) ],
                [ new(4, 1), new(3, 0) ],
                [ new(4, 1), new(0, 0) ],
                [ new(1, 0), new(5, 1) ],
                [ new(4, 1), new(2, 0) ],
                [ new(3, 0), new(5, 1) ],
                [ new(5, 1), new(0, 0) ],
                [ new(1, 0), new(4, 1) ],
                [ new(5, 1), new(2, 0) ],
                [ new(3, 0), new(4, 1)]
            ];

        if (NbtProfiles.Length == 0)
            NbtProfiles = ["system", "self"];

        if (Questionnaires.Length == 0)
            Questionnaires = [
                new Questionnaire() {
                    Type = QuestionnaireType.Scale,
                    Text = "Overall, how difficult or easy did you find this task?",
                    ID = "RATING",
                    ScaleMin = 1,
                    ScaleMinText = "Very difficult",
                    ScaleMax = 7,
                    ScaleMaxText = "Very easy"
                }
            ];

        if (SessionSetups.Length == 0)
            SessionSetups = [
                new() { CttLambdaIndexes = [1, 3], NbtLayoutIndexes = [1, 2, 3, 4] },
                new() { CttLambdaIndexes = [1, 3], NbtLayoutIndexes = [2, 1, 4, 3] },
                new() { CttLambdaIndexes = [1, 3], NbtLayoutIndexes = [3, 4, 2, 1] },
                new() { CttLambdaIndexes = [1, 3], NbtLayoutIndexes = [4, 3, 1, 2] },
                new() { CttLambdaIndexes = [1, 3], NbtLayoutIndexes = [3, 4] },
                new() { CttLambdaIndexes = [1, 3], NbtLayoutIndexes = [4, 3] },
            ];
    }
}
