﻿using Open.Collections;
using System.Threading.Tasks.Dataflow;

namespace SolvePi.Methods;

public class BBD : Method<BBD>, IMethod
{
	public static string Name
		=> "Bailey-Borwein-Plouffe (BBD)";

	public static string Description
		=> "Compute hexidecimal digits of π";

	protected override async ValueTask ExecuteAsync(CancellationToken cancellationToken)
	{
		var bytes = new List<byte>(ushort.MaxValue);

		const int batchSize = 8;
		int currentBatch = 0;
		var pool = MemoryPool<byte>.Shared;
		var digits = new ConcurrentDictionary<int, IMemoryOwner<byte>>();

		var channelOptions = new BoundedChannelOptions(24)
		{
			SingleWriter = false,
			SingleReader = true
		};

		var channel = Channel
			.CreateBounded<(int batch, IMemoryOwner<byte> lease)>(channelOptions);

		var stopwatch = Stopwatch.StartNew();
		_ = Parallel
			.ForAsync(
				0, int.MaxValue / batchSize,
				cancellationToken, async (i, _) =>
			{
				var lease = pool.Rent(batchSize);
				{
					int offset = i * batchSize;
					var span = lease.Memory.Slice(0, batchSize).Span;
					for (int j = 0; j < batchSize; j++)
					{
						span[j] = GetHexByteOfPi(offset + j);
					}
				}

				if(channel.Writer.TryWrite((i, lease)))
				{
					await Task.Yield();
					return;
				}

				await channel.Writer.WriteAsync((i, lease), CancellationToken.None);
			})
			.ContinueWith(_ => channel.Writer.Complete(), CancellationToken.None);

		AnsiConsole.Write("3.");
		await channel.Reader.ReadAll(e =>
		{
			if (e.batch != currentBatch)
			{
				digits.TryAdd(e.batch, e.lease);
				return;
			}

			var next = e.lease;
			do
			{
				WriteBatch(next);
			}
			while (digits.TryRemove(currentBatch, out next));
		}, CancellationToken.None);
		stopwatch.Stop();

		AnsiConsole.WriteLine();

		int charCount = currentBatch * batchSize * 2;
		double seconds = stopwatch.Elapsed.TotalSeconds;
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[green]Completed {0} hex digits of π in {1:0} seconds = {2:0} per second[/]", charCount, seconds, charCount / seconds);

		AnsiConsole.WriteLine();
		AnsiConsole.WriteLine("Decimal Conversion:");

		var decimalResult = 3 + bytes.ByteDigitsToFraction();
		decimalResult.ToDecimalChars(bytes.Count * 2).PreCache(640, CancellationToken.None).WriteToConsole();

		void WriteBatch(IMemoryOwner<byte> lease)
		{
			using var _ = lease;
			foreach (byte value in lease.Memory.Span.Slice(0, batchSize))
			{
				bytes.Add(value);
				AnsiConsole.Write("{0:X2}", value);
			}

			currentBatch++;
		}
	}

	public static byte GetHexDigitOfPi(int n)
	{
		double s1 = Series(1, n);
		double s2 = Series(4, n);
		double s3 = Series(5, n);
		double s4 = Series(6, n);

		double pid = 4.0 * s1 - 2.0 * s2 - s3 - s4;
		pid -= Math.Floor(pid);
		return (byte)(16.0 * pid);
	}

	public static byte GetHexByteOfPi(int n)
	{
		int digitIndex = n * 2;
		byte high = GetHexDigitOfPi(digitIndex);
		byte low = GetHexDigitOfPi(digitIndex + 1);

		return (byte)((high << 4) | low);
	}

	public static IEnumerable<byte> GetHexBytesOfPi(int start, int count)
	{
		for (int i = start; i < start + count; i++)
		{
			yield return GetHexByteOfPi(i);
		}
	}

	private static double Series(int m, int n)
	{
		const double epsilon = 1e-17;
		double sum = 0.0;

		// Sum the series up to n
		for (int k = 0; k < n; k++)
		{
			int denom = 8 * k + m;
			double pow = n - k;
			double term = ModPow16(pow, denom);
			sum += term / denom;
			sum -= Math.Floor(sum);
		}

		// Compute a few terms where k >= n
		for (int k = n; k <= n + 100; k++)
		{
			int denom = 8 * k + m;
			double term = Math.Pow(16.0, n - k) / denom;
			if (term < epsilon) break;
			sum += term;
			sum -= Math.Floor(sum);
		}

		return sum;
	}

	static double ModPow16(double p, int m)
	{
		if (m == 1.0) return 0.0;

		double result = 1.0;
		double baseValue = 16.0;
		while (p > 0)
		{
			if (p % 2 == 1)
			{
				double n = result * baseValue;
				result = n % m;
			}

			double bv2 = baseValue * baseValue;
			baseValue = bv2 % m;
			p = Math.Floor(p / 2);
		}

		return result;
	}
}