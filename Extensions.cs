namespace VarjoDataLogger;

internal static class StringExt
{
    public static string ToPath(this string s, string replacement = "-")
    {
        var invalidChars = System.IO.Path.GetInvalidFileNameChars();
        string[] temp = s.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(replacement, temp);
    }
}

internal static class DoubleExt
{
    public static double ToRange(this double self, double min, double max) => Math.Max(min, Math.Min(self, max));
}

