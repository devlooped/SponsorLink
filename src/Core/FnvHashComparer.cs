using System.Runtime.CompilerServices;

namespace Devlooped.Sponsors;

public class FnvHashComparer : IEqualityComparer<string>
{
    // FNV-1a 32 bit prime and offset basis
    const uint FnvPrime = 0x01000193;
    const uint FnvOffsetBasis = 0x811C9DC5;

    public static IEqualityComparer<string> Default { get; } = new FnvHashComparer();

    public bool Equals(string? x, string? y)
    {
        // If both are null, or both are same instance, consider them equal
        if (x == y) return true;
        if (x == null || y == null) return false;
        return x.Equals(y, StringComparison.Ordinal); // Use Ordinal for performance
    }

    public int GetHashCode(string obj)
    {
        // Convert the 32-bit unsigned hash to a signed int for .NET's GetHashCode
        return unchecked((int)Fnv1aHash(obj));
    }

    // Method to compute FNV-1a hash for a string
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint Fnv1aHash(string str)
    {
        var hash = FnvOffsetBasis;
        foreach (var c in str)
        {
            hash ^= c; // XOR the character
            hash *= FnvPrime; // Multiply by the FNV prime
        }
        return hash;
    }
}