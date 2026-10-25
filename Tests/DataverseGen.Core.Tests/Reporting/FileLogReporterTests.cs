using DataverseGen.Core.Reporting;

namespace DataverseGen.Core.Tests.Reporting;

[TestClass]
public class FileLogReporterTests
{
	private string _directory = null!;

	[TestInitialize]
	public void Init()
	{
		_directory = Path.Combine(Path.GetTempPath(), $"dvgen-log-{Guid.NewGuid():N}");
		Directory.CreateDirectory(_directory);
	}

	[TestCleanup]
	public void Cleanup()
	{
		Directory.Delete(_directory, true);
	}

	[TestMethod]
	public void Log_WritesEveryLevelWithTimestampAndLevel()
	{
		string path = Path.Combine(_directory, "run.log");

		using (FileLogReporter reporter = new(path))
		{
			foreach (LogLevel level in Enum.GetValues<LogLevel>())
			{
				reporter.Log(level, $"message-{level}");
			}
		}

		string[] lines = File.ReadAllLines(path);

		Assert.HasCount(Enum.GetValues<LogLevel>().Length, lines);
		Assert.Contains("[DEBUG  ] message-Debug", lines[0]);
		Assert.Contains("[ERROR  ] message-Error", lines[^1]);
		Assert.MatchesRegex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \[", lines[0]);
	}

	[TestMethod]
	public void Log_OverwritesPreviousRun()
	{
		string path = Path.Combine(_directory, "run.log");

		using (FileLogReporter first = new(path))
		{
			first.Log(LogLevel.Info, "first-run");
		}

		using (FileLogReporter second = new(path))
		{
			second.Log(LogLevel.Info, "second-run");
		}

		string content = File.ReadAllText(path);

		Assert.DoesNotContain("first-run", content);
		Assert.Contains("second-run", content);
	}

	[TestMethod]
	public void Log_WritesFullStackTraceOfException()
	{
		string path = Path.Combine(_directory, "run.log");
		Exception exception;

		try
		{
			ThrowSomething();
			return;
		}
		catch (InvalidOperationException e)
		{
			exception = e;
		}

		using (FileLogReporter reporter = new(path))
		{
			reporter.Log(LogLevel.Error, exception.Message, exception);
		}

		string content = File.ReadAllText(path);

		Assert.Contains("boom", content);
		Assert.Contains(nameof(ThrowSomething), content);
		Assert.Contains("at ", content);
	}

	[TestMethod]
	public void Log_MasksSecretsInMessagesAndExceptions()
	{
		string path = Path.Combine(_directory, "run.log");

		using (FileLogReporter reporter = new(path))
		{
			reporter.Log(LogLevel.Info, "Url=x;ClientSecret=top-secret;");
			reporter.Log(LogLevel.Error,
				"failed",
				new ArgumentException("bad", "Url=x;Password=pw-secret;"));
			reporter.ItemCompleted(new ItemResult
			{
				Kind = ItemKind.Entity,
				Name = "account",
				Status = ItemStatus.Failed,
				Detail = "Thumbprint=thumb-secret"
			});
		}

		string content = File.ReadAllText(path);

		Assert.DoesNotContain("top-secret", content);
		Assert.DoesNotContain("pw-secret", content);
		Assert.DoesNotContain("thumb-secret", content);
	}

	[TestMethod]
	public void Log_IsSafeWhenWrittenFromManyThreads()
	{
		string path = Path.Combine(_directory, "run.log");
		const int threads = 16;
		const int perThread = 200;

		using (FileLogReporter reporter = new(path))
		{
			Parallel.For(0,
				threads,
				new ParallelOptions { MaxDegreeOfParallelism = threads },
				t =>
				{
					for (int i = 0; i < perThread; i++)
					{
						reporter.Log(LogLevel.Debug, $"thread-{t}-line-{i}-end");
					}
				});
		}

		string[] lines = File.ReadAllLines(path);

		Assert.HasCount(threads * perThread, lines);
		Assert.IsTrue(lines.All(l => l.Contains("[DEBUG  ] thread-") && l.EndsWith("-end", StringComparison.Ordinal)),
			"Lines were interleaved or truncated.");
		Assert.HasCount(threads * perThread, lines.Distinct().ToArray());
	}

	private static void ThrowSomething()
	{
		throw new InvalidOperationException("boom");
	}
}
