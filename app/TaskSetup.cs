using System.Text;

namespace VarjoDataLogger;

public record class TaskCondition(int CttLambdaIndex, int NBackTaskIndex)
{
    public bool IsValid => CttLambdaIndex >= 0 && NBackTaskIndex >= 0;
}

public class TaskSetup
{
    public bool Randomized { get; set; } = false;
    public int Repetitions { get; set; } = 1;
    public int[] CttLambdaIndexes { get; set; } = [-1];
    public int[] NBackTaskIndexes { get; set; } = [-1];

    public TaskSetup() { }

    public TaskCondition[] GetAllTasks()
    {
        var result = new List<TaskCondition>();
        for (int i = 0; i < Repetitions; i++)
        {
            foreach (var lambdaIndex in CttLambdaIndexes)
            {
                foreach (var nbackTaskIndex in NBackTaskIndexes)
                {
                    result.Add(new TaskCondition(lambdaIndex, nbackTaskIndex));
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

    public static TaskSetup Load(string? filename, int index = 0)
    {
        if (string.IsNullOrEmpty(filename) || !File.Exists(filename))
        {
            return new TaskSetup();
        }

        try
        {
            var json = File.ReadAllText(filename);
            var taskSets = System.Text.Json.JsonSerializer.Deserialize<TaskSetup[]>(json) ?? [new TaskSetup()];
            return taskSets[index];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load the task setup from '{filename}': {ex.Message}");
            return new TaskSetup();
        }
    }

    public static void SaveTo(string folder, TaskCondition[] tasks)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            Console.WriteLine("Invalid folder for saving task setup.");
            return;
        }

        var filename = Path.Combine(folder, $"{ConditionsFilename}-{DateTime.Now:u}.txt".ToPath());
        try
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"CTT\tNBackTask\tLambda\tDigits\tLayout");

            foreach (var task in tasks)
            {
                var lambda = CttLambdas.ContainsKey(task.CttLambdaIndex) ? CttLambdas[task.CttLambdaIndex] : -1;
                var (digits, layout) = NBackTasks.ContainsKey(task.NBackTaskIndex) ? NBackTasks[task.NBackTaskIndex] : (-1, -1);
                sb.AppendLine($"{task.CttLambdaIndex}\t{task.NBackTaskIndex}\t{lambda}\t{digits}\t{layout}");
            }

            File.WriteAllText(filename, sb.ToString());
            Console.WriteLine($"Task setup saved to '{filename}'.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save task setup: {ex.Message}");
        }
    }

    // Internal

    readonly static string ConditionsFilename = "conditions";
    readonly static Dictionary<int, double> CttLambdas = new()
    {
        { 0, 0.5 },
        { 1, 1.0 },
        { 2, 1.5 },
        { 3, 2.0 },
        { 4, 2.5 },
        { 5, 3.0 },
        { 6, 3.5 },
        { 7, 4.0 },
        { 8, 4.5 },
        { 9, 5.0 },
    };

    readonly static Dictionary<int, (int, int)> NBackTasks = new()
    {
        { 0, (2, 1) },
        { 1, (4, 1) },
        { 2, (8, 1) },
        { 3, (4, 2) },
        { 4, (8, 2) },
    };
}
