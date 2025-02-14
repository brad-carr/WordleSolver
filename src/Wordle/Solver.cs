using Humanizer;
using Wordle.Core;
using Wordle.Feedback;
using Wordle.Interaction;

namespace Wordle;

public sealed class Solver
{
    public const int WordLength = 5;
    public const int DefaultMaxAttempts = 6;

    internal static readonly string SolvedFeedback = new('c', WordLength);
    public IFeedbackProvider FeedbackProvider { get; }
    public Word[] SolutionWordList { get; }
    private readonly IConsole _console;
    private readonly IGuesser _guesser;
    
    public Solver(
        IConsole console,
        IGuesser guesser,
        IFeedbackProvider feedbackProvider,
        string[] solutionWordList
    )
    {
        (_console, _guesser, FeedbackProvider, SolutionWordList) = (
            console,
            guesser,
            feedbackProvider,
            solutionWordList.Select(Word.Create).ToArray()
        );
    }

    public Solver(IConsole console, IGuesser guesser, IFeedbackProvider feedbackProvider)
        : this(console, guesser, feedbackProvider, WordListReader.SolutionWordLiterals().ToArray()) { }

    public (Word? solution, IReadOnlyCollection<Word> guesses, string? reason) Solve(
        DateOnly publicationDate, 
        int maxAttempts = DefaultMaxAttempts
    )
    {
        var seed = GetSeed(publicationDate);
        var random = new Random(seed);
        return Solve(random, maxAttempts);
    }

    public (Word? solution, IReadOnlyCollection<Word> guesses, string? failureReason) Solve(
        Random random, 
        int maxAttempts = DefaultMaxAttempts
    )
    {
        var remainingWords = SolutionWordList;
        var solution = Word.Empty;
        var guesses = new List<Word>(maxAttempts);
        var attemptNo = 0;
        var forbiddenCharsBySlot = new BitMask[WordLength];
        var maybeCharsBySlot = new BitMask[WordLength];
        var charsNotInSolution = BitMask.Empty;
        var charsAlreadySeen = BitMask.Empty;
        
        while (attemptNo < maxAttempts)
        {
            var remainingAttempts = maxAttempts - attemptNo++;

            var guess = 
                remainingWords.Length == 1 
                ? remainingWords[0]
                : _guesser.Guess(
                    random, 
                    solution, 
                    remainingWords, 
                    new Knowledge(charsAlreadySeen, charsNotInSolution, forbiddenCharsBySlot, maybeCharsBySlot), 
                    attemptNo, 
                    remainingAttempts);
            guesses.Add(guess);
            charsAlreadySeen |= guess.UniqueChars;
            
            _console.WriteLine(
                $"Suggestion $magenta({attemptNo}): $green({guess}) - out of $magenta({"possibility".ToQuantity(remainingWords.Length)})"
            );

            var feedback = FeedbackProvider.GetFeedback(guess, remainingWords.Length);
            if (feedback == null)
            {
                return (null, guesses, "failed to acquire feedback for guess");
            }
            if (feedback == SolvedFeedback)
            {
                return (guess, guesses, null);
            }

            var operations = feedback
                .Zip(guess)
                .Select((x, i) => (f: x.First, c: x.Second, i))
                .OrderBy(x => x.f); // ensures processing order 'c' -> 'm' -> 'n'

            var misplacedCharIndexes = new BitMask();
            foreach (var (f, c, i) in operations)
            {
                switch (f)
                {
                    case FeedbackOption.Correct:
                        if (solution[i] == 0)
                        {
                            solution = solution.SetCharAtPos(c, i);
                            remainingWords = remainingWords.Where(w => w[i] == c).ToArray();
                            maybeCharsBySlot[i] = BitMask.Empty;
                        }
                        break;

                    case FeedbackOption.Misplaced:
                        forbiddenCharsBySlot[i] = forbiddenCharsBySlot[i].Set(c);
                        misplacedCharIndexes = misplacedCharIndexes.Set(c);
                        remainingWords = remainingWords
                            .Where(w => w[i] != c && w.Contains(c))
                            .ToArray();
                        break;
                    case FeedbackOption.NoMoreOccurrences:
                        if (misplacedCharIndexes.IsSet(c))
                        {
                            // eliminate words having char at the same position
                            remainingWords = remainingWords.Where(w => w[i] != c).ToArray();

                            // below filter looks useful but never eliminated a single word in the tests
                            // remainingWords = remainingWords.Where(w => w.ContainsLetterAtPositionsOtherThan(c, i)).ToArray();
                            break;
                        }

                        forbiddenCharsBySlot[i] = forbiddenCharsBySlot[i].Set(c);
                        if (!solution.Contains(c))
                        {
                            charsNotInSolution = charsNotInSolution.Set(c);
                        }

                        for (var j = 0; j < WordLength && remainingWords.Length > 0; j++)
                        {
                            if (solution[j] == 0)
                            {
                                remainingWords = remainingWords.Where(w => w[j] != c).ToArray();
                            }
                        }
                        break;
                }

                if (remainingWords.Length < 2)
                {
                    break; // solved, word missing from dictionary or potential bug
                }
            }

            switch (remainingWords.Length)
            {
                case 0:
                    _console.WriteLine("$red(No remaining words, check input)");
                    return (null, guesses, "algorithm failure, no remaining words available");
                case 1:
                    continue;
            }

            AddCommonPositionalCharsToSolution(remainingWords, ref solution);

            // Set possible chars per remaining slot
            foreach (var slot in solution.UnsolvedPositions())
            {
                maybeCharsBySlot[slot] =
                    remainingWords.Aggregate(new BitMask(), (cur, word) => cur.Set(word[slot]))
                    & ~forbiddenCharsBySlot[slot];
            }
        }

        return (null, guesses, "maximum attempts reached without solution");
    }

    private static void AddCommonPositionalCharsToSolution(Word[] remainingWords, ref Word solution)
    {
        unchecked
        {
            var firstRemainingWord = remainingWords[0];

            for (var i = 0; i < WordLength; i++)
            {
                if (solution[i] > 0)
                {
                    continue;
                }

                var allMatch = true;
                var charToMatch = firstRemainingWord[i];
                for (var j = 1; j < remainingWords.Length; j++)
                {
                    if (remainingWords[j][i] == charToMatch) continue;
                    allMatch = false;
                    break;
                }

                if (allMatch)
                {
                    solution = solution.SetCharAtPos(charToMatch, i);
                }
            }
        }
    }

    internal static int GetSeed(DateOnly publicationDate) =>
        publicationDate.Year * 10000 + publicationDate.Month * 100 + publicationDate.Day;
}