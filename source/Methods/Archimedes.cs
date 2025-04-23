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


# Circumscribed Polygon:

We will circumscribe a circle with a polygon and then use the perimeter of the polygon to approximate π.
Starting with a hexagon, which is basically 6 triangles.
You can then take a 30-60-90 triangle to start as half of one of the triangles.

  |
  | s1          p
s | --
  | s2
  ----------------------
            r

Where a is the short side tangent to the circle, b is the radius of the circle, and c is the long side ending at one of the points of the polygon.
From now on we'll use r as the radius of the circle, s as the short side, and p as the long side.
p = √(s^2 + r^2)
r = √(p^2 - s^2)
s = √(p^2 - r^2)

Since this is a 30-60-90 triangle, we can use the following relationships:
p = s * 2
s = r / √3
p = 2r / √3

In the case where r is 1:
s = 1 / √3 ≈ 0.5773502691896257
p = 2 / √3 ≈ 1.1547005383792517

Because of the bisector theorem, we can bisect this triangle and get length of the next short side but taking
the ratio of p/r will equal the ratio of the bisected triangle segments of s.
x is the value we are solving for.

Bisector theorem
p/r = (s - x) / x
p/r = s/x - 1
s/x = p/r + 1
s = x(p/r + 1)
x = s / (p/r + 1)

First result of x is:
x = (1 / √3) / (2 / √3 + 1)
x = 1 / (√3 * (2/√3 + 1))

1/x = 2 + √3											≈ 3.732050807568877
x = 1 / (2 + √3)										≈ 0.267949192431122

1/x^2 = (2 + √3)(2 + √3) = 4 + 4√3 + 3 = 7 + 4√3		≈ 13.92820323027551
x^2 = 1 / (7 + 4√3)

If r is always 1, then the next iteration is:
sNext = x = 1 / (2 + √3)								≈ 0.267949192431122
pNext = √(sNext^2 + 1) = √(1 / (7 + 4√3) + 1)			≈ 1.035276180410083
xNext = sNext / (pNext + 1)
xNext = 1 / ((2 + √3) * (√(1 / (7 + 4√3) + 1) + 1))		≈ 0.131652497587395
1/xNext = (2 + √3) * (√(1 / (7 + 4√3) + 1) + 1)			≈ 7.59575411272515

*/

public class Archimedes : Method<Archimedes>, IMethod
{
	public static string Name
		=> nameof(Archimedes);

	public static string Description
		=> "Approximate π using Archimedes' method";

	protected override ValueTask ExecuteAsync(CancellationToken _)
	{
		BigInteger sides = 12;

		Fraction inscribedP = 2;
		Fraction inscribedS = 1;

		Fraction r = 6;
		Fraction r2 = 36; // r * r
		Fraction s2 = 12; // s * s = 2r / 3
		Fraction p2 = 48; // p * p = 2s * 2s = 4s^2

		Fraction s = s2.Sqrt(100);
		Fraction p = p2.Sqrt(100);

		for (int i = 1; i < 60; i++)
		{
			Fraction newInscribedSegment = GetInscribedSplitSegmentFraction(inscribedP, inscribedS);

			var inscribedPermiter = sides * newInscribedSegment;
			var tau = inscribedPermiter / inscribedP;
			var innerPi = tau / 2;

			Fraction x = s / (p / r + 1);
			var circumscribedPerimiterHalf = sides * x;
			var outerPi = circumscribedPerimiterHalf / r;

			AnsiConsole.WriteLine();
			AnsiConsole.MarkupLine("[blue]Iteration {0}:[/]", i);
			//AnsiConsole.WriteLine("For a {0} sided polygon where the radius is {1}, the segment length is {2}.", sides, inscribedP, newInscribedSegment.ToDecimal());
			//AnsiConsole.WriteLine("The segment length is {0}.", newInscribedSegment.ToDecimal());
			//AnsiConsole.WriteLine("Therefore the perimeter is: {0}", inscribedPermiter.ToDecimal());
			//AnsiConsole.WriteLine("Therefore:");
			//AnsiConsole.WriteLine("τ = {0}", tau.ToDecimal());

			decimal innerPiDecimal = innerPi.ToDecimal();
			decimal outerPiDecimal = outerPi.ToDecimal();

			AnsiConsole.MarkupLine("{0} <≈ [yellow]π[/] <≈ {1}", innerPiDecimal, outerPiDecimal);

			inscribedS = newInscribedSegment / 2;
			s = x;
			s2 = s * s;
			p = (s2 + r2).Sqrt(100);

			sides *= 2;
		}

		return default;
	}

	static Fraction GetInscribedSplitSegmentFraction(
		Fraction r, Fraction s,
		int accuracy = 100)
	{
		Fraction r2 = r * r;
		Fraction s2 = s * s;
		Fraction a = (r2 - s2).Sqrt(accuracy);
		return (2 * (r2 - r * a)).Sqrt(accuracy);
	}
}
