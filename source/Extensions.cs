using Fractions;
using System.Numerics;

namespace SolvePi;

public static class Extensions
{
	public static long ModPow(this int baseValue, int exponent, int modulus)
	{
		if (modulus == 1)
			return 0;

		long result = 1;
		long b = baseValue;

		for (int i = 0; i < exponent; i++)
		{
			long n = result * b;
			result = n % modulus;
		}

		return result;
	}

	public static Fraction HexToFraction(this IEnumerable<byte> hex)
	{
		var result = Fraction.Zero;

		int i = 0;
		foreach (byte digit in hex)
		{
			var denominator = BigInteger.Pow(16, i + 1);
			result += new Fraction(digit, denominator);
			++i;
		}

		return result;
	}
}
