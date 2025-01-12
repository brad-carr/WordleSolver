using System.Collections;
using System.Diagnostics;

namespace Wordle;

[DebuggerDisplay("{ToString()} ({_bits})")]
public readonly struct Word : IReadOnlyList<byte>, IEquatable<Word>
{
    private const byte CharCount = 26;
    private const byte FirstChar = 1;
    
    public static IReadOnlyCollection<byte> Alphabet { get; } = GenerateAlphabet();

    private static byte[] GenerateAlphabet()
    {
        var result = new byte[CharCount];
        var next = FirstChar;
        for (var i=0; i<CharCount; i++)
        {
            result[i] = next++;
        }

        return result;
    }

    public static Word Empty { get; } = new(0UL);

    private readonly ulong _bits;

    private Word(ulong bits) => _bits = bits;

    public int Count => Solver.WordLength;

    public int Length => Solver.WordLength;

    public static Word Create(string word)
    {
        var bits = 0UL;
        var i = Solver.WordLength;
        unchecked
        {
            while (i-- > 0)
            {
                var c = word[i];
                bits <<= 5;

                if (c != ' ')
                {
                    bits |= (byte)(c - 'a' + 1);
                }
            }
        }

        return new Word(bits);
    }

    public bool Contains(byte findChar)
    {
        var bits = _bits;
        unchecked
        {
            for (var i = 0; i < Solver.WordLength; i++)
            {
                if ((byte)(bits & 31) == findChar)
                {
                    return true;
                }

                bits >>= 5;
            }
        }

        return false;
    }

    public bool ContainsOnce(byte findChar, out int foundPos)
    {
        foundPos = -1;
        var bits = _bits;

        unchecked
        {
            for (var i = 0; i < Solver.WordLength; i++)
            {
                if ((byte)(bits & 31) == findChar)
                {
                    if (foundPos >= 0)
                    {
                        return false;
                    }

                    foundPos = i;
                }

                bits >>= 5;
            }
        }

        return foundPos >= 0;
    }

    public byte this[int pos] 
    {
        get
        {
            unchecked
            {
                return (byte)((_bits >> pos * 5) & 31);    
            }
        }
    }

    public Word SetCharAtPos(byte charToSet, int pos)
    {
        var shift = pos * 5;
        var positionalChar = (ulong)charToSet << shift;
        var clearMask = ~(31UL << shift);
        return new Word((_bits & clearMask) | positionalChar);
    }

    public override string ToString()
    {
        var result = "";
        var bits = _bits;
        for (var i = 0; i < Solver.WordLength; i++)
        {
            var b = (char)(bits & 31); 
            result += b == 0 ? ' ' : (char)(b + 'A' -1);
            bits >>= 5;
        }

        return result;
    }

    public IEnumerator<byte> GetEnumerator()
    {
        var bits = _bits;
        for (var i = 0; i < Solver.WordLength; i++)
        {
            yield return (byte)(bits & 31);
            bits >>= 5;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(Word other) => _bits == other._bits;

    public override bool Equals(object? obj) => obj is Word other && Equals(other);

    public override int GetHashCode() => _bits.GetHashCode();

    public static bool operator ==(Word first, Word second) => first.Equals(second);

    public static bool operator !=(Word first, Word second) => !(first == second);
}