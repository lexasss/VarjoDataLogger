namespace VarjoDataLogger.Study;

public enum QuestionType
{
    Scale,
}

public class Question
{
    public QuestionType Type { get; set; } = QuestionType.Scale;
    public string Text { get; set; } = string.Empty;
    public string ID { get; set; } = string.Empty;
    public int ScaleMin { get; set; } = 1;
    public int ScaleMax { get; set; } = 7;
    public string ScaleMinText { get; set; } = "Very difficult";
    public string ScaleMaxText { get; set; } = "Very easy";

    public string[] GetQuestionTextLines()
    {
        var lines = new List<string>
        {
            Text,
        };

        if (Type == QuestionType.Scale)
        {
            var scale = "";
            for (int i = ScaleMin; i <= ScaleMax; i++)
                scale += $"{LINE} {i} {LINE}";

            var spaces = new string(' ', Math.Max(1, scale.Length - ScaleMinText.Length - ScaleMaxText.Length));
            var labels = ScaleMinText + spaces + ScaleMaxText;

            lines.Add("");
            lines.Add(labels);
            lines.Add(scale);
        }
        else throw new NotImplementedException();

        return lines.ToArray();
    }

    public string ReadAnswer()
    {
        if (Type == QuestionType.Scale)
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
        else throw new NotImplementedException();
    }

    // Internal 

    readonly string LINE = "--";

}
