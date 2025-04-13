namespace SolvePi;

public interface IMethod
{
	static abstract string Name { get; }
	static abstract string Description { get; }
}

public abstract class Method<TMethod> : AsyncCommand
	where TMethod : IMethod
{
	public sealed override async Task<int> ExecuteAsync(CommandContext context)
	{
		var rule = new Rule($"[bold yellow]{TMethod.Description}[/]");
		AnsiConsole.Write(rule);

		await ExecuteAsync(Program.Cancellation);
		return 0;
	}

	protected abstract ValueTask ExecuteAsync(
		CancellationToken cancellationToken);
}

public static class MethodExtensions
{
	public static IConfigurator AddMethod<TMethod>(
		this IConfigurator configurator)
		where TMethod : class, IMethod, ICommand
	{
		configurator
			.AddCommand<TMethod>(typeof(TMethod).Name)
			.WithDescription(TMethod.Description);

		return configurator;
	}
}