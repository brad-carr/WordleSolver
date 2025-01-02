namespace Wordle.Feedback;

public sealed class DynamicFeedbackProvider(string Solution) : IFeedbackProvider
{
    public string? GetFeedback(string guess, int remainingWordCount)
    {
        guess = guess ?? throw new ArgumentNullException(nameof(guess));

        if (guess.Length != Solution.Length)
        {
            throw new ArgumentException(
                $"Invalid guess length; expected {Solution.Length}, got {guess.Length}.",
                nameof(guess)
            );
        }

        var sieve = Solution.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());

        var feedbackArray = new char[guess.Length];
        for (var i = 0; i < guess.Length; i++)
        {
            var c = guess[i];
            if (Solution[i] == c)
            {
                feedbackArray[i] = 'c';
                DecrementSieve(sieve, c);
            }
        }

        for (var i = 0; i < guess.Length; i++)
        {
            if (feedbackArray[i] == 'c')
            {
                continue;
            }

            var c = guess[i];
            if (sieve.TryGetValue(c, out var count))
            {
                feedbackArray[i] = 'm';
                DecrementSieve(sieve, c);
            }
            else
            {
                feedbackArray[i] = 'n';
            }
        }

        return new string(feedbackArray);
    }

    private static void DecrementSieve(Dictionary<char, int> sieve, char c)
    {
        if (!sieve.TryGetValue(c, out var count))
        {
            throw new KeyNotFoundException($"Missing character '{c}' in sieve.");
        }

        switch (count)
        {
            case 0:
                throw new InvalidOperationException(
                    $"Did not expect to find keys with zero value in sieve."
                );
            case 1:
                sieve.Remove(c);
                break;
            default:
                sieve[c]--;
                break;
        }
    }
}