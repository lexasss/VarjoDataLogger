namespace VarjoDataLogger.Study.Questions;

public class Scale : Question
{
    public int ScaleMin { get; init; } = 1;
    public int ScaleMax { get; init; } = 7;
    public string ScaleMinText { get; init; } = "Very difficult";
    public string ScaleMaxText { get; init; } = "Very easy";

    public override string[] GetQuestionTextLines()
    {
        var scale = "";
        for (int i = ScaleMin; i <= ScaleMax; i++)
            scale += $"{LINE} {i} {LINE}";

        var spaces = new string(' ', Math.Max(1, scale.Length - ScaleMinText.Length - ScaleMaxText.Length));
        var labels = ScaleMinText + spaces + ScaleMaxText;

        var lines = new List<string>
        {
            Text,
            "",
            labels,
            scale
        };

        return lines.ToArray();
    }

    public override string ReadAnswer()
    {
        int rating;
        for (; ; )
        {
            var input = Console.ReadLine();
            if (!int.TryParse(input, out rating) || rating < ScaleMin || rating > ScaleMax)
            {
                Console.WriteLine($"Please enter a number between {ScaleMin} and {ScaleMax}.");
            }
            else
            {
                break;
            }
        }

        return rating.ToString();
    }

    // Internal 

    readonly string LINE = "--";
}
