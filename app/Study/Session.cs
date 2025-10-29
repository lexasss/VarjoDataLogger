using System.Text;

namespace VarjoDataLogger.Study;

public record class NBackTask(int DigitCount, bool IsRandomized)
{
    public string AsDescription()
    {
        var order = IsRandomized ? "randomized" : "fixed";
        return $"{DigitCount} {order} numbers";
    }
}

public class Session  // N-Back Task should contain a setup with this name
{
    public static double[] CttLambdas { get; set; } = [0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0, 4.5, 5.0];

    public static NBackTask[] NBackTasks { get; set; } = [
        new(2, false),
        new(4, false),
        new(8, false),
        new(4, true),
        new(8, true)
    ];

    public Block[] Blocks { get; }
    public string NBackTaskProfile { get; }
    public int ParticipantID { get; }
    public List<QuestionnaireAnswer> QuestionnaireAnswers { get; } = [];

    public Session(int participantID, Block[] blocks, string nbackTaskProfile)
    {
        ParticipantID = participantID;
        Blocks = blocks;
        NBackTaskProfile = nbackTaskProfile;
    }

    public bool IsValidBlock(Block block)
    {
        return block.CttLambdaIndex >= 0 && block.CttLambdaIndex < CttLambdas.Length
            && block.NBackTaskIndex >= 0 && block.NBackTaskIndex < NBackTasks.Length;
    }

    public void SaveBlockOrder(string folder)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            Console.WriteLine("Invalid folder for saving session blocks.");
            return;
        }

        var filename = Path.Combine(folder, $"{ConditionsFilename}-{DateTime.Now:u}.txt".ToPath());
        try
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"NBTSetup\t{NBackTaskProfile}");
            sb.AppendLine($"CttIndex\tNbtIndex\tCttLambda\tNbtDigits\tNbtLayoutRandomized");

            foreach (var block in Blocks)
            {
                var lambda = block.CttLambdaIndex >= 0 && block.CttLambdaIndex < CttLambdas.Length
                    ? CttLambdas[block.CttLambdaIndex]
                    : -1;
                var (digits, layout) = block.NBackTaskIndex >= 0 && block.NBackTaskIndex < NBackTasks.Length
                    ? NBackTasks[block.NBackTaskIndex]
                    : new(-1, false);
                sb.AppendLine($"{block.CttLambdaIndex}\t{block.NBackTaskIndex}\t{lambda}\t{digits}\t{layout}");
            }

            File.WriteAllText(filename, sb.ToString());
            Console.WriteLine($"Session block parameters were saved to '{filename}'.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save task setup: {ex.Message}");
        }
    }

    // Internal

    readonly static string ConditionsFilename = "conditions";

}
