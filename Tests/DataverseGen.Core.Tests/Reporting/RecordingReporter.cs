using DataverseGen.Core.Reporting;

namespace DataverseGen.Core.Tests;

/// <summary>Records every call; safe to use from parallel workers.</summary>
internal sealed class RecordingReporter : IGenerationReporter
{
	private readonly object _lock = new();

	public List<(LogLevel Level, string Message, Exception? Exception)> Logs { get; } = [];

	public List<ItemResult> Items { get; } = [];

	public List<(Stage Stage, int Total)> Started { get; } = [];

	public List<Stage> Completed { get; } = [];

	public void Log(LogLevel level, string message, Exception? exception = null)
	{
		lock (_lock)
		{
			Logs.Add((level, message, exception));
		}
	}

	public void StageStarted(Stage stage, int total)
	{
		lock (_lock)
		{
			Started.Add((stage, total));
		}
	}

	public void ItemCompleted(ItemResult result)
	{
		lock (_lock)
		{
			Items.Add(result);
		}
	}

	public void StageCompleted(Stage stage, TimeSpan elapsed)
	{
		lock (_lock)
		{
			Completed.Add(stage);
		}
	}
}
