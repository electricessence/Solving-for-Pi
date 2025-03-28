using System.Numerics;
using System.Threading.Channels;
using System.Collections.Concurrent;
using Open.Collections;
using Spectre.Console;

namespace SolvePi.Methods;

public class MonteCarlo : Method<MonteCarlo>, IMethod
{
	public static string Name
		=> "Monte Carlo";

	public static string Description
		=> "Monte Carlo: estimate π using random points in a circle inscribed in a square";

	protected override async ValueTask ExecuteAsync(CancellationToken cancellationToken)
	{
		// Use a deterministic random for reproducible results
		var random = new Random(42);

		// Setup batch processing
		const int batchSize = 10_000;
		const int maxBatches = 10000;
		int totalPoints = 0;
		int pointsInCircle = 0;

		var stopwatch = Stopwatch.StartNew();
		var estimations = new List<(double estimation, int points)>(maxBatches);

		// Setup progress display
		var progressTask = new Progress<double>(value =>
		{
			AnsiConsole.MarkupLine($"[yellow]Processing: {value:P0}[/]");
		});

		// Create a table for displaying results
		var table = new Table()
			.Title("Monte Carlo Estimation of π")
			.AddColumn(new TableColumn("Points").RightAligned())
			.AddColumn(new TableColumn("In Circle").RightAligned())
			.AddColumn(new TableColumn("Ratio").RightAligned())
			.AddColumn(new TableColumn("π Estimate").RightAligned())
			.AddColumn(new TableColumn("Error").RightAligned());

		// Setup parallel processing with channels
		var channelOptions = new BoundedChannelOptions(24)
		{
			SingleWriter = false,
			SingleReader = true
		};

		var channel = Channel
			.CreateBounded<(int pointsIn, int totalBatch)>(channelOptions);

		// Start the computation
		_ = Parallel
			.ForAsync(
				0, maxBatches,
				cancellationToken, async (batchIndex, _) =>
				{
					int pointsInBatch = 0;

					for (int i = 0; i < batchSize; i++)
					{
						// Generate random point in square [-1,1] x [-1,1]
						double x = random.NextDouble() * 2 - 1;
						double y = random.NextDouble() * 2 - 1;

						// Check if point is inside unit circle
						if (x * x + y * y <= 1)
							pointsInBatch++;
					}

					// Report progress occasionally
					if (batchIndex % 100 == 0)
					{
						((IProgress<double>)progressTask).Report((double)batchIndex / maxBatches);
					}

					if (channel.Writer.TryWrite((pointsInBatch, batchSize)))
					{
						await Task.Yield();
						return;
					}

					await channel.Writer.WriteAsync((pointsInBatch, batchSize), CancellationToken.None);
				})
				.ContinueWith(_ => channel.Writer.Complete(), CancellationToken.None);

		AnsiConsole.WriteLine("Computing π using Monte Carlo method...");
		AnsiConsole.WriteLine($"Target: {maxBatches * batchSize:N0} total points");
		AnsiConsole.WriteLine();

		// Process results
		var lastUpdateTime = stopwatch.Elapsed;
		var updateInterval = TimeSpan.FromSeconds(1);

		await channel.Reader.ReadAll(e =>
		{
			pointsInCircle += e.pointsIn;
			totalPoints += e.totalBatch;

			// Calculate current estimation of π
			double piEstimate = 4.0 * pointsInCircle / totalPoints;
			estimations.Add((piEstimate, totalPoints));

			// Update display periodically
			var currentTime = stopwatch.Elapsed;
			if (currentTime - lastUpdateTime > updateInterval)
			{
				lastUpdateTime = currentTime;

				// Clear previous table
				AnsiConsole.Clear();

				// Show status
				AnsiConsole.MarkupLine($"[blue]Monte Carlo Estimation of π[/] - [yellow]{currentTime.TotalSeconds:F1}s elapsed[/]");
				AnsiConsole.WriteLine();

				// Update table with latest results
				table.Rows.Clear();
				table.AddRow(
					$"{totalPoints:N0}",
					$"{pointsInCircle:N0}",
					$"{(double)pointsInCircle / totalPoints:F6}",
					$"{piEstimate:F8}",
					$"{Math.Abs(Math.PI - piEstimate):F8}"
				);

				AnsiConsole.Write(table);

				// Show progress bar
				var progress = (double)totalPoints / (maxBatches * batchSize);
				AnsiConsole.Progress()
					.Columns(new ProgressColumn[]
					{
						new ProgressBarColumn(),
						new PercentageColumn(),
						new RemainingTimeColumn()
					})
					.Start(ctx =>
					{
						var task = ctx.AddTask("[green]Processing[/]");
						task.Value = progress * 100;
					});

				// Show estimation trend
				if (estimations.Count > 5)
				{
					AnsiConsole.WriteLine();
					AnsiConsole.MarkupLine("[blue]Convergence Trend:[/]");
					var trend = estimations.Skip(estimations.Count - 5).ToList();
					var smallTable = new Table()
						.AddColumn(new TableColumn("Points").RightAligned())
						.AddColumn(new TableColumn("π Estimate").RightAligned());

					foreach (var (est, pts) in trend)
					{
						smallTable.AddRow(
							$"{pts:N0}",
							$"{est:F8}"
						);
					}

					AnsiConsole.Write(smallTable);
				}
			}
		}, cancellationToken);

		stopwatch.Stop();

		// Final display of results
		AnsiConsole.Clear();
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[green]Calculation Complete[/]");

		double finalEstimate = 4.0 * pointsInCircle / totalPoints;
		double error = Math.Abs(Math.PI - finalEstimate);
		double percentError = error / Math.PI * 100;

		AnsiConsole.MarkupLine("[green]Final π estimate: [/][yellow]{0:F10}[/]", finalEstimate);
		AnsiConsole.MarkupLine("[green]Actual π value:  [/][yellow]{0:F10}[/]", Math.PI);
		AnsiConsole.MarkupLine("[green]Absolute error:  [/][yellow]{0:F10}[/]", error);
		AnsiConsole.MarkupLine("[green]Percent error:   [/][yellow]{0:F6}%[/]", percentError);
		AnsiConsole.MarkupLine("[green]Total points:    [/][yellow]{0:N0}[/]", totalPoints);
		AnsiConsole.MarkupLine("[green]Computation time:[/][yellow]{0:N1} seconds[/]", stopwatch.Elapsed.TotalSeconds);

		// Show convergence graph
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[blue]Convergence Analysis:[/]");

		// Display convergence as a bar chart (samples at regular intervals)
		var relevantEstimations = estimations
			.Where((_, i) => i % (estimations.Count / 20) == 0) // Take ~20 samples evenly distributed
			.Take(20)
			.ToList();

		if (relevantEstimations.Count != 0)
		{
			// Create bar chart for visualization
			var barChart = new BarChart()
				.Width(60)
				.Label("π Convergence")
				.CenterLabel();

			foreach (var (estimation, points) in relevantEstimations)
			{
				int sampleIndex = (int)(points / (double)batchSize);
				string label = $"{sampleIndex * batchSize / 1000}K";

				// Scale to make differences visible (show variance in the 4th+ decimal place)
				double scaledValue = Math.Abs(estimation - Math.PI) * 10000;
				barChart.AddItem(label, scaledValue > 0 ? scaledValue : 0.1, GetColorForError(Math.PI, estimation));
			}

			AnsiConsole.Write(barChart);
			AnsiConsole.MarkupLine("[dim](Chart shows error magnitude: |π estimate - π| × 10,000 - smaller bars are better)[/]");
		}

		AnsiConsole.WriteLine();

		var convergenceTable = new Table()
			.AddColumn("Sample")
			.AddColumn("Points")
			.AddColumn("π Estimate")
			.AddColumn("Error");

		// Show how the estimate converged at different sample sizes
		int sampleNumber = 1;
		foreach (var (estimation, points) in estimations
			.Where((_, i) => i % (estimations.Count / 10) == 0)
			.Take(10))
		{
			convergenceTable.AddRow(
				$"#{sampleNumber++}",
				$"{points:N0}",
				$"{estimation:F8}",
				$"{Math.Abs(Math.PI - estimation):F8}"
			);
		}

		AnsiConsole.Write(convergenceTable);

		// Display a fun fact
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[blue]Fun Fact:[/] The Monte Carlo method was invented during the Manhattan Project by " +
			"physicists working on nuclear weapon development. It was named after the Monte Carlo Casino in Monaco, " +
			"where the uncle of one of the researchers, Stanisław Ulam, often gambled.");

		return;
	}

	// Helper method to get color based on error magnitude
	private static Color GetColorForError(double actual, double estimate)
	{
		double error = Math.Abs(actual - estimate);

		if (error < 0.0001) return Color.Green;
		if (error < 0.001) return Color.LightGreen;
		if (error < 0.01) return Color.Yellow;
		if (error < 0.1) return Color.Orange1;
		return Color.Red;
	}
}
