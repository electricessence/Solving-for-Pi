using SolvePi.Methods;

namespace SolvePi;

public static class Program
{
	private static readonly CancellationTokenSource CancellationTokenSource;
	internal static readonly CancellationToken Cancellation;

	static Program()
	{
		CancellationTokenSource = new CancellationTokenSource();
		Cancellation = CancellationTokenSource.Token;
	}

	public static async Task<int> Main(string[] args)
	{
		string[] localArgs = args;

		// if there are no args, allow for selecting a method from a drop-down.
		if (args.Length == 0)
		{
			string[] methods =
			[
				nameof(Archimedes),
				nameof(BBD),
				nameof(Chudnovsky),
				nameof(MonteCarlo)
			];

			string selectedMethod = AnsiConsole
				.Prompt(new SelectionPrompt<string>()
					.Title("Please select a method for solving π:")
					.AddChoices(methods));

			localArgs = [selectedMethod];
		}

		var app = new CommandApp();
		app.Configure(
			config => config
				.SetApplicationName("SolvePi")
				.PropagateExceptions()
				.AddMethod<Archimedes>()
				.AddMethod<BBD>()
				.AddMethod<Chudnovsky>()
				.AddMethod<MonteCarlo>());

		// Start listening for keypresses (non-blocking)
		var waitForKeyPress = Task.Run(async () =>
		{
			AnsiConsole.MarkupLine("[grey]Press [bold yellow]Q[/] or [bold yellow]ESC[/] to cancel...[/]");
			while (!Cancellation.IsCancellationRequested)
			{
				if (Console.KeyAvailable)
				{
					var key = Console.ReadKey(true).Key;
					if (key is ConsoleKey.Q or ConsoleKey.Escape)
					{
						CancellationTokenSource.Cancel();
					}
				}

				await Task.Delay(500);
			}
		});

		return await app.RunAsync(localArgs);
	}
}