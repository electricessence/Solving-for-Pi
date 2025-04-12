using Open.Collections;

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

		var byteProcessor = generatedHexDigitOrdered.Reader.ByteDigitsToFraction(batchSize, CancellationToken.None);
		Task decimalOutput = Task.CompletedTask;
		await AnsiConsole.Status()
			.Spinner(Spinner.Known.Dots)
			.StartAsync("Starting...", async ctx =>
			{
				// Start
				//AnsiConsole.Write("3.");
				//ctx.Status("Starting...");

				var stopwatch = Stopwatch.StartNew();
				_ = GenerateBatches(generatedHexDigitBatches.Writer, batchSize, cancellationToken);

				// Ensure batches are in order and write to the bytes channel.
				await generatedHexDigitBatches.Reader.ReadAll(e =>
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
						ctx.Status($"{byteCount:#,###} bytes (batch {currentBatch:#,###})");
					}
					while (digits.TryRemove(currentBatch, out next));

					void AcceptOrderedBatch((int batch, byte[] bytes) e)
					{
						// e.bytes.AsSpan().WriteAsHexToConsole();
						if (!generatedHexDigitOrdered.Writer.TryWrite(e))
							throw new UnreachableException("The bytes channel should be unbound.");
					}
				}, CancellationToken.None);
				stopwatch.Stop();

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
				var decimalResult = 3 + await byteProcessor;

				decimalOutput = Task.Run(() =>
				{
					long total = decimalResult
						.ToDecimalChars(byteCount * 2)
						.PreCache(640, CancellationToken.None)
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
					var bytes = new byte[batchSize];
					{
						int offset = i * batchSize;
						var span = bytes.AsSpan();
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