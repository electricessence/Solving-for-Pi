using Open.Collections;

namespace SolvePi.Methods;

public class Chudnovsky : Method<Chudnovsky>, IMethod
{
	public static string Name
		=> nameof(Chudnovsky);

	public static string Description
		=> "Approximate π using Chudnovsky algorithm";

	// constant values
	private static readonly BigInteger K1 = 545140134, K2 = 13591409, K3 = -640320;
	private static readonly BigInteger K4 = 426880, K5 = 10005;
	private static readonly Fraction K5sqrt = K5.SquareRoot();
	private static readonly Fraction K4Xk5Sqrt = K4 * K5sqrt;

	protected override async ValueTask ExecuteAsync(CancellationToken cancellationToken)
	{
		const int batchSize = 60;

		var channelOptions = new BoundedChannelOptions(24)
		{
			SingleWriter = false,
			SingleReader = true
		};

		var channel = Channel
			.CreateBounded<Fraction>(channelOptions);

		_ = Parallel
			.ForAsync(
				0, int.MaxValue / batchSize,
				cancellationToken, async (i, _) =>
				{
					Fraction batch = Fraction.Zero;
					{
						int offset = i * batchSize;
						for (int j = 0; j < batchSize; j++)
						{
							batch += GetIteration(offset + j);
						}
					}

					if (channel.Writer.TryWrite(batch))
					{
						await Task.Yield();
						return;
					}

					await channel.Writer.WriteAsync(batch, CancellationToken.None);
				})
			.ContinueWith(_ => channel.Writer.Complete(), CancellationToken.None);

		var pool = ArrayPool<char>.Shared;
		ArrayPoolSegment<char> prev = pool.RentSegment(8);
		int length = 1000;
		Fraction S = 0;

		await channel.Reader
			.Pipe(batch => S += batch, 10, false, CancellationToken.None)
			.Pipe(3, static sum => K4Xk5Sqrt / sum, 10, false, CancellationToken.None)
			.ReadUntilCancelled(cancellationToken, pi =>
			{
				using var _ = prev;
				var lease = pool.RentSegment(length);
				var next = lease.Segment;
				pi.ToDecimalChars(next.AsSpan());

				AnsiConsole.WriteLine();
				Console.WriteLine(next.Array!, next.Offset, next.Count);
				AnsiConsole.MarkupLine($"[blue]Digits: {length}[/]");

				if (next.Count == prev.Segment.Count
				&& next.SequenceEqual(prev))
				{
					length += 1000;
				}

				prev = lease;
			});
	}

	static Fraction GetIteration(BigInteger k)
	{
		BigInteger numerator
				= (6 * k).Factorial() * (K1 * k + K2);

		BigInteger denominator
			= (3 * k).Factorial() * k.Factorial().Pow(3) * K3.Pow(3 * k);

		return numerator / denominator;
	}
}
