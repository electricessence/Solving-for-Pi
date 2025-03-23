namespace SolvePi.Methods;

/*

We are attempting to approximate pi by using Archimedes' method of inscribing and circumscribing polygons around a circle.
To do this, we first must establish the simple formula for measuring the segment
of one of the triangles that occurs when you bicect an existing triangle in half.
 
Given these terms:
r = radius
s = side length (would be 1/2 the length of r if the starting point was a hexagon filling the circle)
a = the altitude of the triangle
h = r - a
p = the segment length of the bisected triangle if both other sides are equal to r.

r and s should be known values before starting.


r² = s² + a²

First we have to solve for the altitude.
a² = s² - r²
a = √(s² - r²)

For our case, s will always be less than r, so to avoid a square root of a negative number, we'll swap.
a = √(r² - s²)

Then we can discover the remainder.
s² = r² - a²
s² = p² - h²
r² - a² = p² - h²
p² = r² - a² + h²
p² = r² - a² + (r - a)²
p² = r² - a² + r² - 2ra + a²
p² = 2(r² - ra)
p = √(2(r² - r√(s² - r²)))

*/

public class Archimedes : Method<Archimedes>, IMethod
{
	public static string Name
		=> nameof(Archimedes);

	public static string Description
		=> "Approximate π using Archimedes' method";

	protected override ValueTask ExecuteAsync(CancellationToken _)
	{
		Fraction r = 2;
		Fraction s = 1;
		BigInteger sides = 12;

		for (int i = 1; i < 60; i++)
		{
			Fraction p = GetSplitSegmentFraction(r, s);

			var permiter = sides * p;
			var tau = permiter / r;
			var pi = tau / 2;
			AnsiConsole.WriteLine();
			AnsiConsole.MarkupLine("[blue]Iteration {0}:[/]", i);
			AnsiConsole.WriteLine("For a {0} sided polygon where the radius is {1}, the segment length is {2}.", sides, r, p.ToDecimal());
			AnsiConsole.WriteLine("The segment length is {0}.", p.ToDecimal());
			AnsiConsole.WriteLine("Therefore the perimeter is: {0}", permiter.ToDecimal());
			AnsiConsole.WriteLine("Therefore:");
			AnsiConsole.WriteLine("τ = {0}", tau.ToDecimal());

			decimal discrepancy = (decimal)Math.Abs(Math.PI - pi.ToDouble());
			decimal piDecimal = pi.ToDecimal();
			if (discrepancy == decimal.Zero)
			{
				AnsiConsole.MarkupLine("[green]π[/] = [white]{0}[/]", piDecimal);
				AnsiConsole.MarkupLine("π discrepancy: [green]within range[/]");
				break;
			}

			AnsiConsole.MarkupLine("[yellow]π[/] = {0}", piDecimal);
			AnsiConsole.MarkupLine("π discrepancy: [yellow]{0}[/]", discrepancy);

			s = p / 2;
			sides *= 2;
		}

		return default;
	}

	static Fraction GetSplitSegmentFraction(
		Fraction r, Fraction s,
		int accuracy = 100)
	{
		Fraction r2 = r * r;
		Fraction s2 = s * s;
		Fraction a = (r2 - s2).Sqrt(accuracy);
		return (2 * (r2 - r * a)).Sqrt(accuracy);
	}
}
