using Fractions;
using Nito.AsyncEx;
using Open.Collections;
using SolvePi.Utils.TaskScheduling;

namespace SolvePi.Methods;

public class BBD : Method<BBD>, IMethod
{
	public static string Name
		=> "Bailey-Borwein-Plouffe (BBD)";

	public static string Description
		=> "Compute hexidecimal digits of π";

	protected override async ValueTask ExecuteAsync(CancellationToken cancellationToken)
	{
		// Prepare
		const int batchSize = 64;

		Task decimalOutput = Task.CompletedTask;
		await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Starting...", async ctx =>
		{
			int byteCount = 0;
			int currentBatch = 0;
			var digits = new ConcurrentDictionary<int, byte[]>();

			var generatedHexDigitBatches = Channel.CreateBounded<(int batch, byte[] bytes)>(new BoundedChannelOptions(128)
			{
				SingleWriter = false,
				SingleReader = true
			});

			var generatedHexDigitOrdered = Channel.CreateUnbounded<(int batch, byte[] bytes)>(new UnboundedChannelOptions
			{
				SingleWriter = true,
				SingleReader = false
			});

			// Start
			//AnsiConsole.Write("3.");
			ctx.Status("Starting...");
			await Task.Delay(500); // Allow the status to be set before we start processing.

			var scheduler = new PriorityQueueTaskScheduler();

			var byteProcessor = scheduler[0].Run(
				() => generatedHexDigitOrdered.Reader.ByteDigitsToFraction(batchSize, CancellationToken.None).AsTask(),
				CancellationToken.None);

			var stopwatch = Stopwatch.StartNew();
			_ = scheduler[2].Run(
				() => GenerateBatches(generatedHexDigitBatches.Writer, batchSize, cancellationToken),
				CancellationToken.None);

			// Ensure batches are in order and write to the bytes channel.
			await scheduler[1].Run(
				() => generatedHexDigitBatches.Reader.ReadAll(ProcessHexDigitBatch, CancellationToken.None).AsTask(),
				CancellationToken.None);
			stopwatch.Stop();

			void ProcessHexDigitBatch((int batch, byte[] bytes) e)
			{
				if (e.batch != currentBatch)
				{
					digits.TryAdd(e.batch, e.bytes);
					return;
				}

				byte[]? next = e.bytes;
				do
				{
					AcceptOrderedBatch(e);
					byteCount += batchSize;
					currentBatch++;
					ctx.Status($"Batch {currentBatch:#,###}: {byteCount:#,###} bytes ({byteCount / stopwatch.Elapsed.TotalSeconds:#,###} per second)");
				}
				while (digits.TryRemove(currentBatch, out next));

				void AcceptOrderedBatch((int batch, byte[] bytes) e)
				{
					// e.bytes.AsSpan().WriteAsHexToConsole();
					if (!generatedHexDigitOrdered.Writer.TryWrite(e))
						throw new UnreachableException("The bytes channel should be unbound.");
				}
			}

			ctx.Status("Stopped hex proccessing.");
			generatedHexDigitOrdered.Writer.Complete();
			AnsiConsole.WriteLine();

			int charCount = currentBatch * batchSize * 2;
			double seconds = stopwatch.Elapsed.TotalSeconds;
			AnsiConsole.WriteLine();
			AnsiConsole.MarkupLine(
				"[green]Completed {0:#,###} hex digits of π in {1:#,###} seconds = {2:#,###} per second[/]",
				charCount, seconds, charCount / seconds);

			AnsiConsole.WriteLine();
			AnsiConsole.MarkupLine("[cyan]Decimal Conversion:[/]");

			ctx.Status("Starting...");
			stopwatch.Restart();
			Fraction decimalResult = 3 + await byteProcessor;

			// Larger than 3.15 signifies that something is wrong.
			Debug.Assert(decimalResult < (Fraction)3.15);

			decimalOutput = Task.Run(() =>
			{
				long total = decimalResult
					.ToDecimalChars(byteCount * 2)
					.PreCache(640, CancellationToken.None)
#if DEBUG
					// Sometimes the conversion looks as if the digits are doubled.
					// This is a test to ensure that the conversion is correct.	
					.Select((c, i) =>
					{
						switch(i)
						{
							case 0:
								Debug.Assert(c == '3');
								break;

							case 1:
								Debug.Assert(c == '.');
								break;

							case 2:
								Debug.Assert(c == '1');
								break;

							case 3:
								Debug.Assert(c == '4');
								break;
						}

						return c;
					})
#endif
					.WriteToConsole() - 2;

				double totalSeconds = seconds;
				stopwatch.Stop();
				seconds = stopwatch.Elapsed.TotalSeconds;
				totalSeconds += seconds;
				AnsiConsole.WriteLine(); // End of digits.
				AnsiConsole.WriteLine(); // Spacer.
				AnsiConsole.MarkupLine(
					"[green]Completed {0:#,###} decimal digits of π in {1:#,###} seconds = {2:#,###} per second[/]",
					total, seconds, total / seconds);
				AnsiConsole.WriteLine("{0:#,###} total seconds", totalSeconds);
			});
		});

		await decimalOutput;
	}

	protected static Task GenerateBatches(
		ChannelWriter<(int batch, byte[] bytes)> writer,
		int batchSize,
		CancellationToken cancellationToken)
		=> Parallel
			.ForAsync(
				0, int.MaxValue / batchSize,
				cancellationToken, async (i, _) =>
				{
					byte[] bytes = new byte[batchSize];
					{
						int offset = i * batchSize;
						Span<byte> span = bytes.AsSpan();
						for (int j = 0; j < batchSize; j++)
						{
							span[j] = GetHexByteOfPi(offset + j);
						}
					}

					if (writer.TryWrite((i, bytes)))
					{
						await Task.Yield();
						return;
					}

					await writer.WriteAsync((i, bytes), CancellationToken.None);
				})
			.ContinueWith(_ => writer.Complete(), CancellationToken.None);

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