namespace SolvePi.Utils;

public static class MathExtensions
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
}
