namespace DataverseGen.Core.Reporting;

/// <summary>
///  Sink for everything Core wants to tell the user. Core never writes to the console itself;
///  the host (CLI) decides about presentation, verbosity and log files.
///  Implementations must be thread-safe: metadata retrieval and file generation call every member concurrently
///  from worker threads. Each item gets exactly one <see cref="ItemCompleted" /> per phase.
/// </summary>
public interface IGenerationReporter
{
	void Log(LogLevel level, string message, Exception exception = null);

	void StageStarted(Stage stage, int total);

	void ItemCompleted(ItemResult result);

	void StageCompleted(Stage stage, TimeSpan elapsed);

	/// <summary>An item (entity, custom API, output file) is about to be processed; lets the UI show what is in flight.</summary>
	void ItemStarted(ItemKind kind, ItemPhase phase, string name) { }

	/// <summary>One request to Dataverse finished (used for the performance report).</summary>
	void RequestCompleted(Stage stage, TimeSpan elapsed) { }
}
