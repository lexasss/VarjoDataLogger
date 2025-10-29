namespace VarjoDataLogger.Study;

public enum QuestionnaireType
{
    Scale,
    YesNo
}

public class Questionnaire
{
    public QuestionnaireType Type { get; set; } = QuestionnaireType.Scale;
    public string Text { get; set; } = string.Empty;
    public string ID { get; set; } = string.Empty;
    public int ScaleMin { get; set; } = 1;
    public int ScaleMax { get; set; } = 7;
    public string ScaleMinText { get; set; } = "Very difficult";
    public string ScaleMaxText { get; set; } = "Very easy";

    public string[] GetScaleText()
    {
        if (Type == QuestionnaireType.Scale)
        {
            var line = "---";
            var scale = "";
            for (int i = ScaleMin; i <= ScaleMax; i++)
                scale += $"{line} {i} {line}";

            var spaces = new string(' ', Math.Max(1, scale.Length - ScaleMinText.Length - ScaleMaxText.Length));
            var labels = ScaleMinText + spaces + ScaleMaxText;
            
            return [labels, scale];
        }

        return [];
    }

    public string GetAnswer()
    {
        if (Type == QuestionnaireType.Scale)
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
}

public record class QuestionnaireAnswer(string Question, object answer);
