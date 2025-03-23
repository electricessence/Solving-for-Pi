namespace SolvePi;

public readonly struct Hexadecimal(Memory<byte> bytes)
{
	public readonly Memory<byte> Bytes = bytes;

	public static implicit operator Hexadecimal(Memory<byte> bytes) => new(bytes);

	public static implicit operator Memory<byte>(Hexadecimal hex) => hex.Bytes;

	public static implicit operator Hexadecimal(string hex) => new(Convert.FromHexString(hex));

	public static implicit operator string(Hexadecimal hex) => Convert.ToHexString(hex.Bytes.Span);
}
