using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Devlooped.Sponsors;

static class Extensions
{
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
