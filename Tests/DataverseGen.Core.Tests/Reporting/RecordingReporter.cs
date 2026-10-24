using DataverseGen.Core.Reporting;

namespace DataverseGen.Core.Tests;

internal sealed class RecordingReporter : IGenerationReporter
{
	public List<(LogLevel Level, string Message, Exception? Exception)> Logs { get; } = [];

	public List<ItemResult> Items { get; } = [];

	public List<(Stage Stage, int Total)> Started { get; } = [];

	public List<Stage> Completed { get; } = [];

	public void Log(LogLevel level, string message, Exception? exception = null)
	{
		Logs.Add((level, message, exception));
	}

	public void StageStarted(Stage stage, int total)
	{
		Started.Add((stage, total));
	}

	public void ItemCompleted(ItemResult result)
	{
		Items.Add(result);
	}

	public void StageCompleted(Stage stage, TimeSpan elapsed)
	{
		Completed.Add(stage);
	}
}
