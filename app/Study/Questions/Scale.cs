namespace VarjoDataLogger.Study.Questions;

public class Scale : Question
{
    public int Min { get; init; } = 1;
    public int Max { get; init; } = 7;
    public string MinLabel { get; init; } = "Very difficult";
    public string MaxLabel { get; init; } = "Very easy";

    public override string[] GetQuestionTextLines()
    {
        var scale = "";
        for (int i = Min; i <= Max; i++)
            scale += $"{LINE} {i} {LINE}";

        var spaces = new string(' ', Math.Max(1, scale.Length - MinLabel.Length - MaxLabel.Length));
        var labels = MinLabel + spaces + MaxLabel;

        return [
            Text,
            "",
            labels,
            scale
        ];
    }

    public override string ReadAnswer()
    {
        int rating;
        for (; ; )
        {
            var input = Console.ReadLine();
            if (!int.TryParse(input, out rating) || rating < Min || rating > Max)
            {
                Console.WriteLine($"Please enter a number between {Min} and {Max}.");
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
