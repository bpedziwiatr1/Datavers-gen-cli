using System.Globalization;
using System.Text;

namespace DataverseGen.Core.Reporting;

/// <summary>
///  Writes every message (including Debug and full exception stack traces) to a file, regardless of the console
///  verbosity. The file is overwritten per run. Thread-safe; expects secrets to be masked upstream (see <see cref="MaskingReporter" />).
/// </summary>
public sealed class FileLogReporter : IGenerationReporter, IDisposable
{
	private readonly object _lock = new();
	private readonly StreamWriter _writer;

	public FileLogReporter(string path)
	{
		string directory = Path.GetDirectoryName(Path.GetFullPath(path));

		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		_writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read),
			new UTF8Encoding(false))
		{
			AutoFlush = true
		};
	}

	public void Log(LogLevel level, string message, Exception exception = null)
	{
		string text = exception is null ? message : $"{message}{Environment.NewLine}{exception}";

		Write(level.ToString().ToUpperInvariant(), text);
	}

	public void StageStarted(Stage stage, int total)
	{
		Write("STAGE", $"{stage} started ({total})");
	}

	public void ItemStarted(ItemKind kind, ItemPhase phase, string name)
	{
		Write("ITEM", $"{kind} {name} [{phase}]: started");
	}

	public void ItemCompleted(ItemResult result)
	{
		string detail = string.IsNullOrEmpty(result.Detail) ? string.Empty : $" · {result.Detail}";

		Write("ITEM",
			$"{result.Kind} {result.Name} [{result.Phase}]: {result.Status}{detail} ({result.Elapsed:g}, {result.Files} files, {result.Bytes} bytes)");
	}

	public void StageCompleted(Stage stage, TimeSpan elapsed)
	{
		Write("STAGE", $"{stage} completed in {elapsed:g}");
	}

	public void RequestCompleted(Stage stage, TimeSpan elapsed)
	{
		Write("REQUEST", $"{stage} request completed in {elapsed:g}");
	}

	public void Dispose()
	{
		lock (_lock)
		{
			_writer.Dispose();
		}
	}

	private void Write(string level, string message)
	{
		string line =
			$"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)} [{level,-7}] {message}";

		lock (_lock)
		{
			_writer.WriteLine(line);
		}
	}
}
