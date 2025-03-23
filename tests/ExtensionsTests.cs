namespace SolvePi.Tests;

public class MathExtensionsTests
{
	[Theory]
	[InlineData(16, 0, 7, 1)]   // 16^0 % 7 = 1
	[InlineData(16, 1, 7, 2)]   // 16^1 = 16 % 7 = 2
	[InlineData(16, 2, 7, 4)]   // 256 % 7 = 4
	[InlineData(16, 3, 7, 1)]   // 4096 % 7 = 1
	[InlineData(2, 10, 1000, 24)] // 2^10 = 1024 % 1000 = 24
	[InlineData(5, 0, 1, 0)]    // special case: any ^ 0 % 1 = 0
	public void ModPow_WorksCorrectly(int b, int exp, int mod, long expected)
	{
		long result = b.ModPow(exp, mod);
		Assert.Equal(expected, result);
	}
}