namespace DataverseGen.Core.Reporting;

public sealed class NullReporter : IGenerationReporter
{
	public static readonly NullReporter Instance = new();

	public void Log(LogLevel level, string message, Exception exception = null) { }

	public void StageStarted(Stage stage, int total) { }

	public void ItemCompleted(ItemResult result) { }

	public void StageCompleted(Stage stage, TimeSpan elapsed) { }

	public void ItemStarted(ItemKind kind, ItemPhase phase, string name) { }

	public void RequestCompleted(Stage stage, TimeSpan elapsed) { }
}
