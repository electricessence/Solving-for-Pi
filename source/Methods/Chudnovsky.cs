namespace SolvePi.Methods;

public class Chudnovsky : Method<Chudnovsky>, IMethod
{
	public static string Name
		=> nameof(Chudnovsky);

	public static string Description
		=> "Approximate π using Chudnovsky algorithm";

	protected override ValueTask ExecuteAsync(CancellationToken cancellationToken)
	{
		const int iter = 100;

		Fraction S = 0;

		BigInteger k; // iteration k

		// constant values
		BigInteger k1 = 545140134, k2 = 13591409, k3 = -640320;
		BigInteger k4 = 426880, k5 = 10005;
		Fraction k5sqrt = k5.SquareRoot();

		IEnumerable<char> prevString = [];
		int length = 1000;

		for (
			k = 0;
			!cancellationToken.IsCancellationRequested && k <= iter - 1;
			k++)
		{
			BigInteger numerator
				= (6 * k).Factorial() * (k1 * k + k2);

			BigInteger denominator
				= (3 * k).Factorial() * k.Factorial().Pow(3) * k3.Pow(3 * k);

			S += numerator / denominator;

			AnsiConsole.Write("k = {0} : ", k + 1);

			Fraction pi = k4 * k5sqrt / S;
			var next = pi.ToDecimalChars(length);
			next.WriteToConsole();
			AnsiConsole.WriteLine();

			if (next.SequenceEqual(prevString))
			{
				length += 1000;
			}

			prevString = next;
		}

		return default;
	}
}
