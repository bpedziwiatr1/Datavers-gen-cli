namespace DataverseGen.Core.Reporting;

public sealed class CompositeReporter : IGenerationReporter
{
	private readonly IGenerationReporter[] _reporters;

	public CompositeReporter(params IGenerationReporter[] reporters)
	{
		_reporters = reporters ?? Array.Empty<IGenerationReporter>();
	}

	public void Log(LogLevel level, string message, Exception exception = null)
	{
		foreach (IGenerationReporter reporter in _reporters)
		{
			reporter.Log(level, message, exception);
		}
	}

	public void StageStarted(Stage stage, int total)
	{
		foreach (IGenerationReporter reporter in _reporters)
		{
			reporter.StageStarted(stage, total);
		}
	}

	public void ItemStarted(ItemKind kind, ItemPhase phase, string name)
	{
		foreach (IGenerationReporter reporter in _reporters)
		{
			reporter.ItemStarted(kind, phase, name);
		}
	}

	public void ItemCompleted(ItemResult result)
	{
		foreach (IGenerationReporter reporter in _reporters)
		{
			reporter.ItemCompleted(result);
		}
	}

	public void StageCompleted(Stage stage, TimeSpan elapsed)
	{
		foreach (IGenerationReporter reporter in _reporters)
		{
			reporter.StageCompleted(stage, elapsed);
		}
	}

	public void RequestCompleted(Stage stage, TimeSpan elapsed)
	{
		foreach (IGenerationReporter reporter in _reporters)
		{
			reporter.RequestCompleted(stage, elapsed);
		}
	}
}
