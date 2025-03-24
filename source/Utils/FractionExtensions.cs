using System.Diagnostics.CodeAnalysis;

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
			for (int i = 0; i < digits; i++)
			{
				fraction *= 10;
				int digit = (int)fraction;
				yield return (char)('0' + digit);
				fraction -= digit;
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
			if(i == bufferLength)
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
		this IEnumerable<byte> bytes)
	{
		var result = Fraction.Zero;

		int i = 0;
		foreach (byte b in bytes)
		{
			// split the digits into two parts
			int d2 = b % 16;
			int d1 = (b - d2) / 16;
			result += GetFromDigit(d1, ++i, BaseValue16);
			result += GetFromDigit(d2, ++i, BaseValue16);
		}

		return result;

		static Fraction GetFromDigit(int value, int digit, BigInteger baseValue)
		{
			var denominator = baseValue.Pow(digit);
			return new Fraction(value, denominator);
		}
	}
}
