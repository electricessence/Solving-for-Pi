namespace SolvePi.Utils;
internal static class CollectionExtensions
{
	private readonly struct ShortBatch<T>(
		IMemoryOwner<T> original, int length) : IMemoryOwner<T>
	{
		public Memory<T> Memory { get; } = original.Memory.Slice(0, length);

		public void Dispose()
			=> original.Dispose();
	}

	public static IMemoryOwner<T> Trim<T>(this IMemoryOwner<T> original, int length)
	{
		Debug.Assert(length > 0);
		return new ShortBatch<T>(original, length);// original.Memory.Length > length ? new ShortBatch<T>(original, length) : original;
	}

	public static IEnumerable<IMemoryOwner<T>> Batch<T>(
		this IEnumerable<T> source,
		int minBatchSize,
		MemoryPool<T>? pool = null)
	{
		var enumerator = source.GetEnumerator();

		pool ??= MemoryPool<T>.Shared;
		while (enumerator.MoveNext())
		{
			var batch = pool.Rent(minBatchSize);
			int count = 0;
			var span = batch.Memory.Span;

		next:
			span[count++] = enumerator.Current;
			if (count == span.Length)
			{
				yield return batch;
				batch = pool.Rent(minBatchSize);
				count = 0;
				continue;
			}

			if (enumerator.MoveNext())
			{
				goto next;
			}

			yield return new ShortBatch<T>(batch, count);
			break;
		}
	}

	public static IEnumerable<IMemoryOwner<T>> BatchFixed<T>(
		this IEnumerable<T> source,
		int batchSize,
		MemoryPool<T>? pool = null)
	{
		var enumerator = source.GetEnumerator();

		pool ??= MemoryPool<T>.Shared;
		while (enumerator.MoveNext())
		{
			var batch = pool.Rent(batchSize);
			int count = 0;
			var span = batch.Memory.Span;

		next:
			span[count++] = enumerator.Current;
			if (count == batchSize)
			{
				yield return batch;
				batch = pool.Rent(batchSize);
				count = 0;
				continue;
			}

			if (enumerator.MoveNext())
			{
				goto next;
			}

			yield return new ShortBatch<T>(batch, count);
			break;
		}
	}
}
