namespace SolvePi.Utils;

public static partial class FractionExtensions
{
	public static async ValueTask<Fraction> ByteDigitsToFraction(
		this ChannelReader<IMemoryOwner<byte>> bytes)
	{
		var result = Fraction.Zero;
		long offset = 0;
		// Read bytes from the channel until completion
		await bytes.ReadAll((lease, batch) =>
		{
			using var _ = lease; // Ensure the lease is disposed after use
			var span = lease.Memory.Span; // Get the span of bytes from the lease
			int len = span.Length; // Get the length of the current lease
			for (int i = 0; i < len; ++i)
				result += CombineDigitsInByte(span[i], offset + i);

			if (batch % 10 == 9) // Optional: Reduce the fraction every 10 batches for performance
			{
				// This is just to keep the result manageable and avoid overflow.
				result = result.Reduce();
			}

			offset += len; // Increment offset by the length of the current lease
		});

		return result.Reduce();
	}

	public static async ValueTask<Fraction> ByteDigitsToFraction(
		this ChannelReader<IMemoryOwner<byte>> bytes,
		int expectedBatchSize)
	{
		var result = Fraction.Zero;
		// Read bytes from the channel until completion
		await bytes.ReadAll((lease, batch) =>
		{
			using var _ = lease; // Ensure the lease is disposed after use
			var span = lease.Memory.Span; // Get the span of bytes from the lease
			int len = span.Length; // Get the length of the current lease
			Debug.Assert(len == expectedBatchSize);

			long offset = batch * expectedBatchSize; // Calculate the starting offset for this batch
			for (int i = 0; i < len; ++i)
				result += CombineDigitsInByte(span[i], offset + i);

			if (batch % 10 == 9) // Optional: Reduce the fraction every 10 batches for performance
			{
				// This is just to keep the result manageable and avoid overflow.
				result = result.Reduce();
			}
		});

		return result.Reduce();
	}

	public static ValueTask<Fraction> ByteDigitsToFraction(
		this ChannelReader<(int batch, IMemoryOwner<byte> lease)> bytes)
		=> bytes.Transform(static e => e.lease).ByteDigitsToFraction();

	public static async ValueTask<Fraction> ByteDigitsToFraction(
		this ChannelReader<(int batch, IMemoryOwner<byte> lease)> bytes,
		int expectedBatchSize, Action? onBatchProcessed = null,
		CancellationToken cancellationToken = default)
	{
		var result = Fraction.Zero;

		// Read bytes from the channel until completion
		await bytes
			.Transform(e =>
			{
				using var lease = e.lease;
				var batchSum = Fraction.Zero;
				var span = lease.Memory.Span;
				Debug.Assert(span.Length == expectedBatchSize);

				long offset = e.batch * expectedBatchSize; // Calculate the starting offset for this batch
				for (int i = 0; i < expectedBatchSize; ++i)
					batchSum += CombineDigitsInByte(span[i], offset + i);

				return batchSum.Reduce();
			})
			// Pipe the results in parallel to another channel.
			.Pipe(Environment.ProcessorCount, sum => sum, singleReader: true, cancellationToken: cancellationToken)
			// Read the resultant values one at a time and create a sum.
			.ReadAll(sum =>
			{
				result += sum;
				onBatchProcessed?.Invoke();
			}, cancellationToken);

		return result.Reduce();
	}

	public static Fraction ByteDigitsToFraction(this (int batch, IMemoryOwner<byte> lease) batch, int batchSize)
	{
		using var lease = batch.lease;
		ReadOnlySpan<byte> span = lease.Memory.Span;
		int start = batchSize * batch.batch;

		var result = Fraction.Zero;
		int len = span.Length;
		for (int i = 0; i < len; i++)
		{
			result += CombineDigitsInByte(span[i], i + start);
		}

		return result.Reduce();
	}
}
