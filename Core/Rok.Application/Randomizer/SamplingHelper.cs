namespace Rok.Application.Randomizer;


public static class SamplingHelper
{
    /// <summary>
    /// Samples up to <paramref name="sampleCount"/> distinct items from <paramref name="source"/>
    /// using a partial Fisher–Yates shuffle. The source is not mutated.
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    /// <param name="source">Source sequence to sample from.</param>
    /// <param name="sampleCount">Maximum number of items to sample.</param>
    /// <param name="rnd">Optional Random instance; if null Random.Shared is used.</param>
    /// <returns>List of sampled items (count <= sampleCount).</returns>
    public static List<T> SamplePartialFisherYates<T>(IEnumerable<T> source, int sampleCount, Random? rnd = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (sampleCount <= 0)
            return new List<T>();

        List<T> list = source.ToList();
        int n = list.Count;
        if (n == 0)
            return new List<T>();

        int m = Math.Min(sampleCount, n);
        rnd ??= Random.Shared;

        List<T> result = new(m);

        for (int i = 0; i < m; i++)
        {
            int j = rnd.Next(i, n);

            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
            result.Add(list[i]);
        }

        return result;
    }
}