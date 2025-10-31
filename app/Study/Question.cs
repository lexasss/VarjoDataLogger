using System.Text.Json.Serialization;
using VarjoDataLogger.Study.Questions;

namespace VarjoDataLogger.Study;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Scale), "Scale")]
public abstract class Question
{
    public string Text { get; init; } = string.Empty;
    public string ID { get; init; } = string.Empty;

    public abstract string[] GetQuestionTextLines();

    public abstract string ReadAnswer();
}
