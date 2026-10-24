namespace DataverseGen.Core.Reporting;

/// <summary>
///  Sink for everything Core wants to tell the user. Core never writes to the console itself;
///  the host (CLI) decides about presentation, verbosity and log files.
/// </summary>
public interface IGenerationReporter
{
	void Log(LogLevel level, string message, Exception exception = null);

	void StageStarted(Stage stage, int total);

	void ItemCompleted(ItemResult result);

	void StageCompleted(Stage stage, TimeSpan elapsed);
}
