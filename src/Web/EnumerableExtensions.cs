using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Devlooped.Sponsors;

public static class EnumerableExtensions
{
    /// <summary>
    /// Batches the source sequence into chunks of the specified size.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    /// <param name="source">The source sequence to batch.</param>
    /// <param name="size">The maximum size of each batch. Must be greater than 0.</param>
    /// <returns>An IEnumerable of IEnumerables, each representing a batch of elements.</returns>
    /// <exception cref="ArgumentNullException">Thrown if source is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if size is less than or equal to 0.</exception>
    public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> source, int size)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size), "Size must be greater than 0.");

        return BatchIterator(source, size);
    }

    static IEnumerable<IEnumerable<T>> BatchIterator<T>(IEnumerable<T> source, int size)
    {
        var batch = new List<T>(size);
        foreach (var item in source)
        {
            batch.Add(item);
            if (batch.Count == size)
            {
                yield return batch;
                batch = new List<T>(size);
            }
        }
        if (batch.Count > 0)
        {
            yield return batch;
        }
    }
}