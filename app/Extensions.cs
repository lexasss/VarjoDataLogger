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
    public static double ToRad(this double angle) => angle * Math.PI / 180;
}

internal static class RandomExt
{
    public static Random Shuffle<T>(this Random rng, IList<T> array)
    {
        void Shuffle()
        {
            int n = array.Count;
            while (n > 1)
            {
                int k = rng.Next(n--);
                (array[k], array[n]) = (array[n], array[k]);
            }
        }

        int repetitions = rng.Next(8) + 3;  // 3..10 repetitions
        for (int i = 0; i < repetitions; i++)
        {
            Shuffle();
        }

        return rng;
    }
}