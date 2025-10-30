using System.Collections.ObjectModel;
using System.Text;

namespace VarjoDataLogger.Study;

public record class NbtLayout(int DigitCount, bool IsRandomized)
{
    public string AsDescription()
    {
        var order = IsRandomized ? "randomized" : "fixed";
        return $"{DigitCount} {order} numbers";
    }
}

public class QuestionWithAnswer
{
    public Question Question { get; }
    public string? Answer { get; set; }

    public QuestionWithAnswer(Question question)
    {
        Question = question;
    }
}

public class Session
{
    public static double[] CttLambdas { get; set; } = [0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0, 4.5, 5.0];
    public static string[] NbtProfiles { get; set; } = ["system"];
    public static NbtLayout[] NbtLayouts { get; set; } = [
        new(2, false),
        new(4, false),
        new(8, false),
        new(4, true),
        new(8, true)
    ];

    public Block[] Blocks { get; }
    public string NbtProfile { get; }
    public int ParticipantID { get; }
    public ReadOnlyDictionary<Block, QuestionWithAnswer[]> QuestionsAndAnswers { get; }

    public Session(int participantID, Block[] blocks, string nbtProfile, Question[] questions)
    {
        ParticipantID = participantID;
        Blocks = blocks;
        NbtProfile = nbtProfile;

        var blockQuestions = new Dictionary<Block, QuestionWithAnswer[]>();
        foreach (var block in blocks)
            blockQuestions.Add(block, questions.Select(q => new QuestionWithAnswer(q)).ToArray());

        QuestionsAndAnswers = new(blockQuestions);

        _questionIds = questions.Select(q => q.ID).ToArray();
    }

    public static bool IsValidBlock(Block block)
    {
        return block.CttLambdaIndex >= 0 && block.CttLambdaIndex < CttLambdas.Length
            && block.NbtLayoutIndex >= 0 && block.NbtLayoutIndex < NbtLayouts.Length;
    }

    public void SaveConditionsAndAnswers(Settings settings)
    {
        Console.WriteLine();

        var folder = settings.LogFolder;

        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            folder = Logger.SelectLogFolder();
            if (folder != null)
                settings.LogFolder = folder;
            else
            {
                Console.WriteLine("Session block parameters were NOT saved.");
                return;
            }
        }

        var filename = Path.Combine(folder, $"blocks-{DateTime.Now:u}.txt".ToPath());
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"CttIndex\tCttLambda\tNbtIndex\tNbtDigits\tNbtLayoutRandomized\tNbtProfile\t" 
                + string.Join('\t', _questionIds));

            foreach (var block in Blocks)
            {
                var lambda = block.CttLambdaIndex >= 0 && block.CttLambdaIndex < CttLambdas.Length
                    ? CttLambdas[block.CttLambdaIndex]
                    : -1;
                var (digits, layout) = block.NbtLayoutIndex >= 0 && block.NbtLayoutIndex < NbtLayouts.Length
                    ? NbtLayouts[block.NbtLayoutIndex]
                    : new(-1, false);
                var qas = QuestionsAndAnswers[block];
                var qasStr = string.Join('\t', qas.Select(qa => qa.Answer));
                sb.AppendLine($"{block.CttLambdaIndex}\t{lambda}\t{block.NbtLayoutIndex}\t{digits}\t{layout}\t{NbtProfile}\t{qasStr}");
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

    readonly string[] _questionIds;
}
