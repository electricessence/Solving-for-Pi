using System.Runtime.CompilerServices;

namespace SolvePi.Utils;

public static class FractionExtensions
{
	static readonly Fraction DefaultPrecision = new(1, 10000000000);
	public static Fraction SquareRoot(
		this Fraction value,
		Fraction? precision = default)
	{
		Fraction x = value;
		Fraction y = 1;
		var p = precision ?? DefaultPrecision;

		while (x - y > p)
		{
			x = (x + y) / 2;
			y = value / x;
		}

		return x;
	}

	public static Fraction SquareRoot(
		this BigInteger value,
		Fraction? precision = default)
	{
		Fraction x = value;
		Fraction y = 1;
		var p = precision ?? DefaultPrecision;

		while (x - y > p)
		{
			x = (x + y) / 2;
			y = value / x;
		}

		return x;
	}

	// BigInteger Factorial function
	public static BigInteger Factorial(
		this BigInteger n)
	{
		BigInteger result = 1;
		for (int i = 2; i <= n; i++)
		{
			result *= i;
		}

		return result;
	}

	public static BigInteger Pow(
		this BigInteger x, int y)
		=> BigInteger.Pow(x, y);

	public static BigInteger Pow(
		this BigInteger x, BigInteger y)
	{
		BigInteger result = 1;
		for (BigInteger i = 0; i < y; i++)
		{
			result *= x;
		}

		return result;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void WriteAsHexToConsole(this ReadOnlySpan<byte> span)
	{
		foreach (byte value in span)
		{
			AnsiConsole.Write("{0:X2}", value);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void WriteAsHexToConsole(this Span<byte> span)
		=> WriteAsHexToConsole((ReadOnlySpan<byte>)span);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void WriteAsHexToConsole(this ReadOnlyMemory<byte> mem)
		=> WriteAsHexToConsole(mem.Span);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void WriteAsHexToConsole(this Memory<byte> mem)
		=> WriteAsHexToConsole((ReadOnlySpan<byte>)mem.Span);

	public static IEnumerable<char> ToDecimalChars(
		this Fraction fraction, int digits)
	{
		var wholePart = (BigInteger)fraction;
		string wholePartString = wholePart.ToString();
		fraction -= wholePart;
		if (fraction == Fraction.Zero)
			return wholePartString;

		return wholePartString
			.Append('.')
			.Concat(Remaining(fraction, digits));

		static IEnumerable<char> Remaining(
			Fraction fraction, int digits)
		{
			unchecked
			{
				for (int i = 0; i < digits; i++)
				{
					fraction *= 10;
					int digit = (int)fraction;
					yield return (char)('0' + digit);
					fraction -= digit;
				}
			}
		}
	}

	public static ReadOnlySpan<char> ToDecimalChars(
		this Fraction fraction, Span<char> target)
	{
		var wholePart = (BigInteger)fraction;
		string wholePartString = wholePart.ToString();
		fraction -= wholePart;
		if (fraction == Fraction.Zero)
			return wholePartString;

		wholePartString.AsSpan().CopyTo(target);
		target[wholePartString.Length] = '.';

		for (int i = wholePartString.Length + 1; i < target.Length; i++)
		{
			fraction *= 10;
			int digit = (int)fraction;
			target[i] = (char)('0' + digit);
			fraction -= digit;
			fraction = fraction.Reduce();
		}

		return target;
	}

	public static void WriteToConsole(
		this IEnumerable<char> chars, int bufferLength = 32)
	{
		char[] buffer = new char[bufferLength];
		int i = 0;
		foreach (char b in chars)
		{
			buffer[i++] = b;
			if (i == bufferLength)
			{
				Console.Write(buffer);
				i = 0;
			}
		}

		if (i > 0)
		{
			Console.Write(buffer, 0, i);
		}
	}

	static readonly BigInteger BaseValue16 = 16;

	public static Fraction ByteDigitsToFraction(
		this ReadOnlySpan<byte> bytes)
	{
		Fraction result = Fraction.Zero;

		int len = bytes.Length;
		for (int i = 0; i < len; ++i)
			result += CombineDigitsInByte(bytes[i], i);

		return result;
	}

	public static async ValueTask<Fraction> ByteDigitsToFraction(this ChannelReader<byte> bytes)
	{
		var result = Fraction.Zero;
		// Read bytes from the channel until completion
		await bytes.ReadAll(
			(b, i) => result += CombineDigitsInByte(b, i));

		return result;
	}

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
		this ChannelReader<(int batch, IMemoryOwner<byte> lease)> bytes, int expectedBatchSize)
	{
		var result = Fraction.Zero;
		var sync = new Lock();
		// Read bytes from the channel until completion
		await bytes
			.Transform(e =>
			{
				using var lease = e.lease;
				var batchSum = Fraction.Zero;
				var span = lease.Memory.Span;
				int len = span.Length;
				Debug.Assert(len == expectedBatchSize);

				long offset = e.batch * expectedBatchSize; // Calculate the starting offset for this batch
				for (int i = 0; i < len; ++i)
					batchSum += CombineDigitsInByte(span[i], offset + i);

				return batchSum.Reduce();
			})
			.ReadAllConcurrently(Environment.ProcessorCount, sum =>
			{
				lock (sync)
				{
					result += sum;
				}
			});

		return result.Reduce();
	}

	public static async ValueTask<Fraction> ByteDigitsToFraction(
		this ChannelReader<(int batch, byte[] bytes)> bytes, int expectedBatchSize)
	{
		var result = Fraction.Zero;
		var sync = new Lock();
		// Read bytes from the channel until completion
		await bytes
			.Transform(e =>
			{
				var batchSum = Fraction.Zero;
				var span = e.bytes.AsSpan();
				int len = span.Length;
				Debug.Assert(len == expectedBatchSize);

				long offset = e.batch * expectedBatchSize; // Calculate the starting offset for this batch
				for (int i = 0; i < len; ++i)
					batchSum += CombineDigitsInByte(span[i], offset + i);

				return batchSum.Reduce();
			})
			.ReadAllConcurrently(Environment.ProcessorCount, sum =>
			{
				lock (sync)
				{
					result += sum;
				}
			});

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

	// Adjust batch size as needed for performance
	public static Fraction ByteDigitsToFraction(
		this IEnumerable<byte> bytes, int batchSize = 1024)
	{
		Fraction sum = Fraction.Zero;
		var sync = new Lock();

		var batches = bytes
			.BatchFixed(batchSize)
			.Select((lease, index) => (index, lease));

		// Process each batch of bytes in parallel, but lock on the sum.
		Parallel.ForEach(batches, batch =>
		{
			var result = ByteDigitsToFraction(batch, batchSize);
			// Lock to safely update the shared sum variable.
			lock (sync)
			{
				// Safely add the result of this batch to the sum.
				sum += result;
				sum = sum.Reduce(); // Optional: Reduce the fraction to keep it simplified.
			}
		});

		return sum;
	}

	static Fraction GetFromDigit(int value, long digit, BigInteger baseValue)
	{
		var denominator = baseValue.Pow(digit);
		return new Fraction(value, denominator);
	}

	const int MaxDigits = int.MaxValue / 2 - 3;

	// Separate the nibbles.
	static Fraction CombineDigitsInByte(byte b, long i)
	{
		Debug.Assert(i < MaxDigits, "i is too large");
		unchecked
		{
			// Safe if i is within reasonable bounds for a digit position
			long n = i * 2;
			// No overflow risk in these operations as we're working with a byte (0-255)
			int d1 = (b >> 4) & 0xF;  // High nibble - always 0-15
			int d2 = b & 0xF;         // Low nibble - always 0-15

			return GetFromDigit(d1, n + 1, BaseValue16)
				 + GetFromDigit(d2, n + 2, BaseValue16);
		}
	}

	// Separate the nibbles.
	//static Fraction CombineDigitsInByte(int b, int i)
	//{
	//	int n = i * 8;
	//	Fraction result = Fraction.Zero;
	//	for (int shift = 0; shift < 32; shift += 4) // Changed from 8 to 32 to process all 8 nibbles
	//	{
	//		int digit = (b >> shift) & 0xF;
	//		result += GetFromDigit(digit, n + (shift / 4) + 1, BaseValue16);
	//	}

	//	return result;
	//}

	public static Fraction Sum(this IEnumerable<Fraction> fractions)
		=> fractions.Aggregate(Fraction.Zero, (a, v) => a + v);

	public static Fraction Sum(this ParallelQuery<Fraction> fractions)
		=> fractions.Aggregate(Fraction.Zero, (a, v) => a + v);

	public static Fraction Sum(this ReadOnlySpan<Fraction> fractions)
	{
		// Sum of Span...
		var result = Fraction.Zero;
		int len = fractions.Length;
		for (int i = 0; i < len; i++)
		{
			result += fractions[i];
		}

		return result;
	}
}
