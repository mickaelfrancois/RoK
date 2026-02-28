namespace Rok.Application.Services;

public static class Levenshtein
{
    public static int GetThreshold(string keyword) => keyword.Length switch
    {
        <= 3 => 0,
        <= 5 => 1,
        <= 8 => 2,
        _ => 3
    };

    public static int ComputeLevenshtein(string a, string b)
    {
        int aLength = a.Length;
        int bLength = b.Length;

        if (aLength == 0)
            return bLength;
        if (bLength == 0)
            return aLength;

        int[] prev = Enumerable.Range(0, bLength + 1).ToArray();

        int[] curr = new int[bLength + 1];

        for (int i = 1; i <= aLength; i++)
        {
            curr[0] = i;

            for (int j = 1; j <= bLength; j++)
                curr[j] = a[i - 1] == b[j - 1] ? prev[j - 1] : 1 + Math.Min(prev[j - 1], Math.Min(prev[j], curr[j - 1]));

            Array.Copy(curr, prev, bLength + 1);
        }

        return curr[bLength];
    }
}
