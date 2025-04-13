using Open.Collections;
using R3;
using SolvePi.Utils.TaskScheduling;
using System.Collections.Immutable;

namespace SolvePi.Methods;

public class BBD : Method<BBD>, IMethod
{
	public static string Name
		=> "Bailey-Borwein-Plouffe (BBD)";

	public static string Description
		=> "Compute using hexidecimal digits of π";

	public class Processor(int batchSize)
	{
		private readonly PriorityQueueTaskScheduler _scheduler = new();

		private readonly Subject<Memory<byte>> _hexDigits = new();
		public Observable<Memory<byte>> HexDigits => _hexDigits;

		public int ByteCount { get; private set; }
		public int BatchesProcessed { get; private set; }
		public int BatchCount { get; private set; }

		private readonly Subject<int> _batchProcessed = new();
		public Observable<int> BatchProcessed => _batchProcessed;


		private readonly Subject<int> _batchesQueued = new();
		public Observable<int> BatchesQueued => _batchesQueued;

		int _started = 0;

		public async Task<Task<Fraction>> RunAsync(
			CancellationToken cancellationToken)
		{
			if(Interlocked.CompareExchange(ref _started, 1, 0) != 0)
				throw new InvalidOperationException("Already started.");

			var digits = new ConcurrentDictionary<int, IMemoryOwner<byte>>();

			var generatedHexDigitOrdered = Channel.CreateUnbounded<(int batch, IMemoryOwner<byte> lease)>(new UnboundedChannelOptions
			{
				SingleWriter = true,
				SingleReader = false
			});

			var generatedHexDigitBatches = Channel.CreateBounded<(int batch, IMemoryOwner<byte> lease)>(new BoundedChannelOptions(128)
			{
				SingleWriter = false,
				SingleReader = true
			});

			var byteProcessor = _scheduler[0].Run(
				async () =>
				{
					try
					{
						return await generatedHexDigitOrdered.Reader.ByteDigitsToFraction(batchSize, () => _batchProcessed.OnNext(++BatchesProcessed), CancellationToken.None);
					}
					finally
					{
						_batchProcessed.OnCompleted();
					}
				},
				CancellationToken.None);

			_ = _scheduler[2].Run(
				() => GenerateBatches(generatedHexDigitBatches.Writer, batchSize, cancellationToken),
				CancellationToken.None);

			// Ensure batches are in order and write to the bytes channel.
			await _scheduler[1].Run(
				() => generatedHexDigitBatches.Reader.ReadAll(ProcessHexDigitBatch, CancellationToken.None).AsTask(),
				CancellationToken.None);
			generatedHexDigitOrdered.Writer.Complete();
			_batchesQueued.OnCompleted();

			return byteProcessor;

			void ProcessHexDigitBatch((int batch, IMemoryOwner<byte> lease) e)
			{
				if (e.batch != BatchCount)
				{
					digits.TryAdd(e.batch, e.lease);
					return;
				}

				var next = e.lease;
				do
				{
					AcceptOrderedBatch((BatchCount, next));
					ByteCount += batchSize;
					_batchesQueued.OnNext(++BatchCount);
				}
				while (digits.TryRemove(BatchCount, out next));

				void AcceptOrderedBatch((int batch, IMemoryOwner<byte> lease) e)
				{
					var mem = e.lease.Memory.Slice(0, batchSize);
#if DEBUG
					if (e.batch == 0)
					{
						Debug.Assert(BatchCount == 0);
						int maxSize = Math.Min(batchSize, FirstHexDigitBytes.Length);
						var expected = FirstHexDigitBytes.AsSpan(0, maxSize);
						var actual = mem.Span.Slice(0, maxSize);
						Debug.Assert(expected.SequenceEqual(actual), "The first hex digits should match.");
					}
#endif
					_hexDigits.OnNext(mem);
					if (!generatedHexDigitOrdered.Writer.TryWrite(e))
						throw new UnreachableException("The bytes channel should be unbound.");
				}
			}
		}
	}

	private const string FirstHexDigits
		= "243F6A8885A308D313198A2E037073";

	public static ImmutableArray<byte> FirstHexDigitBytes
		=> [.. ConvertHexStringToBytes(FirstHexDigits)];

	protected override async ValueTask ExecuteAsync(CancellationToken cancellationToken)
	{
		// Prepare
		const int batchSize = 128;
		double seconds = 0;
		double totalSeconds = 0;
		Stopwatch stopwatch = new();
		var processor = new Processor(batchSize);
		Task<Fraction> byteProcessor = Task.FromResult(Fraction.Zero);

		// Step 1: process the hex digits and return an active task that is generating the fraction.
		await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Starting...", async ctx =>
		{
			// Start
			//AnsiConsole.Write("3.");
			ctx.Status("Starting...");
			await Task.Delay(500); // Allow the status to be set before we start processing.

			using var _ = processor.BatchesQueued.Subscribe(
				batch => ctx.Status($"Batch {batch:#,###}: {processor.ByteCount:#,###} bytes ({processor.ByteCount / stopwatch.Elapsed.TotalSeconds:#,###} per second)"));

			stopwatch.Start();
			byteProcessor = await processor.RunAsync(cancellationToken);
			stopwatch.Stop();
		});

		AnsiConsole.WriteLine();

		seconds = stopwatch.Elapsed.TotalSeconds;
		totalSeconds = seconds;

		int totalBatches = processor.BatchCount;
		int charCount = totalBatches * batchSize * 2;
		int processed = processor.BatchesProcessed;
		int remaining = totalBatches - processed;

		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine(
			"[green]Completed {0:#,###} hex digits of π in {1:#,###} seconds = {2:#,###} per second[/]",
			charCount, seconds, charCount / seconds);

		AnsiConsole.MarkupLine(
			"[cyan]Already processed {0:#,###} of {1:#,###} total batches.[/]",
			processed, totalBatches);

		Fraction pi = 3;
		await AnsiConsole
			.Progress()
			.Columns(
			[
				new SpinnerColumn(),            // Spinner
				new TaskDescriptionColumn(),    // Task description
				new ProgressBarColumn(),        // Progress bar
				new PercentageColumn(),         // Percentage
				new ElapsedTimeColumn(),		// Elapsed time
				new RemainingTimeColumn(),      // Remaining time
			])
			.StartAsync(async ctx =>
			{
				var task = ctx.AddTask($"[cyan]Remaining {remaining:#,###} batches[/]");
				task.MaxValue = remaining;

				stopwatch.Restart();
				using var _ = processor.BatchProcessed.Subscribe(
					batch => task.Value = batch - processed);

				pi = 3 + await byteProcessor;
				stopwatch.Stop();
			});

		seconds = stopwatch.Elapsed.TotalSeconds;
		totalSeconds += seconds;

		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[green]Average of {0:#,###} digits converted to fraction per second[/]",
			remaining * batchSize / seconds);

#if DEBUG
		// Larger than 3.15 signifies that something is wrong.
		decimal dr = pi.ToDecimal();
		if (dr > 3.15m)
		{
			AnsiConsole.MarkupLine("[red]The result was greater than 3.15: {0}[/]", dr);
			return;
		}
#endif
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[cyan]Decimal Conversion:[/]");

		using var output = new Subject<char>();
		using var stream = File.OpenWrite($"pi.decimal.{DateTime.Now:yyyyMMddHHmmss}.txt");
		using var writer = new StreamWriter(stream);
		using var chunky = output.Chunk(80).Subscribe(chunk =>
		{
			writer.Write(chunk);
			AnsiConsole.Write(chunk);
		});

		stopwatch.Restart();
		long total = pi
			.ToDecimalChars(processor.ByteCount * 2)
			.PreCache(640, CancellationToken.None)
			.PublishTo(output) - 2;
		stopwatch.Stop();

		seconds = stopwatch.Elapsed.TotalSeconds;
		totalSeconds += seconds;

		AnsiConsole.WriteLine(); // End of digits.
		AnsiConsole.WriteLine(); // Spacer.
		AnsiConsole.MarkupLine(
			"[green]Converted {0:#,###} decimal digits of π in {1:#,###} seconds = {2:#,###} per second[/]",
			total, seconds, total / seconds);
		AnsiConsole.WriteLine("{0:#,###} total seconds", totalSeconds);

	}

	protected static Task GenerateBatches(
		ChannelWriter<(int batch, IMemoryOwner<byte> lease)> writer,
		int batchSize,
		CancellationToken cancellationToken)
		=> Parallel
			.ForAsync(
				0, int.MaxValue / batchSize,
				cancellationToken, async (i, _) =>
				{
					var lease = MemoryPool<byte>.Shared.Rent(batchSize);
					{
						int offset = i * batchSize;
						Span<byte> span = lease.Memory.Span;
						for (int j = 0; j < batchSize; j++)
						{
							span[j] = GetHexByteOfPi(offset + j);
						}
					}

					if (writer.TryWrite((i, lease)))
					{
						return;
					}

					await writer.WriteAsync((i, lease), CancellationToken.None);
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

	// A function that can take 2 characters of hex and convert them to a byte.
	private static byte HexToByte(ReadOnlySpan<char> hex)
	{
		if (hex.Length != 2)
			throw new ArgumentException("Hex string must be 2 characters long.");
		return (byte)((GetHexValue(hex[0]) << 4) | GetHexValue(hex[1]));
	}

	private static int GetHexValue(char hex)
	{
		if (hex is >= '0' and <= '9')
			return hex - '0';
		if (hex is >= 'A' and <= 'F')
			return hex - 'A' + 10;
		if (hex is >= 'a' and <= 'f')
			return hex - 'a' + 10;
		throw new ArgumentException($"Invalid hex character: {hex}");
	}

	private static IEnumerable<byte> ConvertHexStringToBytes(string hex)
	{
		if(hex.Length % 2 != 0)
			throw new ArgumentException("Hex string must have an even length.");

		for (int i = 0; i < hex.Length; i += 2)
		{
			yield return HexToByte(hex.AsSpan(i, 2));
		}
	}
}