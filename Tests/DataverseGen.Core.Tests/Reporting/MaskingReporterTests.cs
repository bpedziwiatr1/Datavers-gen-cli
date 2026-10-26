using DataverseGen.Core.Reporting;

namespace DataverseGen.Core.Tests.Reporting;

[TestClass]
public class MaskingReporterTests
{
	[TestMethod]
	public void Log_MasksMessageAndExceptionText()
	{
		RecordingReporter inner = new();
		MaskingReporter reporter = new(inner);

		reporter.Log(LogLevel.Warning,
			"Url=x;ClientSecret=top-secret;",
			new ArgumentException("bad Password=pw-secret;"));

		(LogLevel level, string message, Exception? exception) = inner.Logs.Single();
		Assert.AreEqual(LogLevel.Warning, level);
		Assert.DoesNotContain("top-secret", message);
		Assert.DoesNotContain("pw-secret", exception!.Message);
		Assert.DoesNotContain("pw-secret", exception.ToString());
		Assert.Contains("ArgumentException", exception.ToString());
	}

	[TestMethod]
	public void Log_WithoutException_PassesNull()
	{
		RecordingReporter inner = new();

		new MaskingReporter(inner).Log(LogLevel.Info, "plain");

		Assert.IsNull(inner.Logs.Single().Exception);
	}

	[TestMethod]
	public void ItemCompleted_MasksNameAndDetailAndKeepsTheRest()
	{
		RecordingReporter inner = new();
		MaskingReporter reporter = new(inner);

		reporter.ItemCompleted(new ItemResult
		{
			Kind = ItemKind.CustomApi,
			Phase = ItemPhase.Generate,
			Name = "Key=name-secret",
			Status = ItemStatus.Failed,
			Detail = "Thumbprint=thumb-secret",
			Elapsed = TimeSpan.FromSeconds(1),
			Files = 2,
			Bytes = 30
		});

		ItemResult result = inner.Items.Single();
		Assert.DoesNotContain("name-secret", result.Name);
		Assert.DoesNotContain("thumb-secret", result.Detail);
		Assert.AreEqual(ItemKind.CustomApi, result.Kind);
		Assert.AreEqual(ItemPhase.Generate, result.Phase);
		Assert.AreEqual(ItemStatus.Failed, result.Status);
		Assert.AreEqual(TimeSpan.FromSeconds(1), result.Elapsed);
		Assert.AreEqual(2, result.Files);
		Assert.AreEqual(30, result.Bytes);
	}

	[TestMethod]
	public void ItemStarted_MasksName()
	{
		RecordingReporter inner = new();

		new MaskingReporter(inner).ItemStarted(ItemKind.Entity, ItemPhase.Retrieve, "Password=pw-secret");

		Assert.DoesNotContain("pw-secret", inner.ItemsStarted.Single().Name);
	}

	[TestMethod]
	public void StageAndRequestCalls_AreForwarded()
	{
		RecordingReporter inner = new();
		MaskingReporter reporter = new(inner);

		reporter.StageStarted(Stage.Entities, 2);
		reporter.RequestCompleted(Stage.Entities, TimeSpan.FromMilliseconds(5));
		reporter.StageCompleted(Stage.Entities, TimeSpan.Zero);

		Assert.AreEqual((Stage.Entities, 2), inner.Started.Single());
		Assert.AreEqual((Stage.Entities, TimeSpan.FromMilliseconds(5)), inner.Requests.Single());
		Assert.AreEqual(Stage.Entities, inner.Completed.Single());
	}

	[TestMethod]
	public void NullInner_FallsBackToNullReporter()
	{
		MaskingReporter reporter = new(null!);

		reporter.Log(LogLevel.Info, "ok");
	}

	[TestMethod]
	public void FileLogBehindMasking_NeverWritesRawSecrets()
	{
		string path = Path.Combine(Path.GetTempPath(), $"dvgen-mask-{Guid.NewGuid():N}.log");

		try
		{
			using (FileLogReporter file = new(path))
			{
				MaskingReporter reporter = new(file);

				reporter.Log(LogLevel.Error, "failed", new InvalidOperationException("ClientSecret=raw-secret;"));
				reporter.ItemCompleted(new ItemResult { Name = "account", Detail = "Password=raw-secret" });
			}

			Assert.DoesNotContain("raw-secret", File.ReadAllText(path));
		}
		finally
		{
			File.Delete(path);
		}
	}

	[TestMethod]
	public void MaskingReporter_IsThreadSafe()
	{
		RecordingReporter inner = new();
		MaskingReporter reporter = new(inner);

		Parallel.For(0,
			500,
			new ParallelOptions { MaxDegreeOfParallelism = 8 },
			i =>
			{
				reporter.Log(LogLevel.Info, $"Password=p{i};");
				reporter.ItemCompleted(new ItemResult { Name = $"e{i}", Detail = $"Key=k{i}" });
			});

		Assert.HasCount(500, inner.Logs);
		Assert.HasCount(500, inner.Items);
		Assert.IsFalse(inner.Logs.Any(log => log.Message.Contains('p') && !log.Message.Contains("***")));
	}
}
