namespace Wordle;

internal static class Linq
{
    /// <summary>Chooses a random element from a sequence, using only a single pass of the sequence.</summary>
    /// <remarks>Uses the Fisher-Yates reservoir sampling algorithm.</remarks>
    public static T? RandomElement<T>(this IEnumerable<T> items, Random rand)
        where T : class
    {
        var i = 0;
        T? selection = null;
        foreach (var item in items)
        {
            var next = rand.Next(++i);
            if (next == 0)
            {
                selection = item;
            }
        }
        return selection;
    }

    /// <summary>Checks if a sequence contains a single instance of a given item.</summary>
    public static bool ContainsOnce<T>(this IEnumerable<T> items, T comparand)
        where T : IEquatable<T>
    {
        var n = 0;
        foreach (var item in items)
        {
            if (item.Equals(comparand))
            {
                if (++n > 1)
                {
                    return false;
                }
            }
        }

        return n == 1;
    }
}
