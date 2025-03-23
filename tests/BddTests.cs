namespace SolvePi.Tests;

public static class BddTests
{
	const string Expected = "243F6A8885A308D31319";

	[Fact]
    public static void HexBytesValidation()
    {
		var sb = new StringBuilder(Expected.Length);
		foreach(int digit in BBD.GetHexBytesOfPi(0, Expected.Length / 2))
			sb.Append(digit.ToString("X2"));

		Assert.Equal(Expected, sb.ToString());
	}
}
