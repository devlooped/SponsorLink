using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Devlooped.Sponsors;

static class Extensions
{
    public static Array Cast(this Array array, Type elementType)
    {
        //Convert the object list to the destination array type.
        var result = Array.CreateInstance(elementType, array.Length);
        Array.Copy(array, result, array.Length);
        return result;
    }

    public static void Assert(this ILogger logger, [DoesNotReturnIf(false)] bool condition, [CallerArgumentExpression(nameof(condition))] string? message = default, params object?[] args)
    {
        if (!condition)
        {
            //Debug.Assert(condition, message);
            logger.LogError(message, args);
            throw new InvalidOperationException(message);
        }
    }
}
