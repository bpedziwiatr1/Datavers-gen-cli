using DataverseGen.Core.Reporting;

namespace DataverseGen.Core.Tests.Reporting;

[TestClass]
public class CompositeReporterTests
{
	[TestMethod]
	public void FansOutEveryCallToAllReporters()
	{
		RecordingReporter first = new();
		RecordingReporter second = new();
		CompositeReporter composite = new(first, second);
		Exception exception = new InvalidOperationException("x");
		ItemResult item = new() { Kind = ItemKind.Entity, Name = "account", Status = ItemStatus.Ok };

		composite.Log(LogLevel.Warning, "careful", exception);
		composite.StageStarted(Stage.Entities, 3);
		composite.ItemCompleted(item);
		composite.StageCompleted(Stage.Entities, TimeSpan.FromSeconds(1));

		foreach (RecordingReporter reporter in new[] { first, second })
		{
			Assert.HasCount(1, reporter.Logs);
			Assert.AreEqual(LogLevel.Warning, reporter.Logs[0].Level);
			Assert.AreEqual("careful", reporter.Logs[0].Message);
			Assert.AreSame(exception, reporter.Logs[0].Exception);
			Assert.AreEqual((Stage.Entities, 3), reporter.Started.Single());
			Assert.AreSame(item, reporter.Items.Single());
			Assert.AreEqual(Stage.Entities, reporter.Completed.Single());
		}
	}

	[TestMethod]
	public void NullReporter_AcceptsAllCalls()
	{
		NullReporter.Instance.Log(LogLevel.Error, "x", new Exception());
		NullReporter.Instance.StageStarted(Stage.Generate, 1);
		NullReporter.Instance.ItemCompleted(new ItemResult());
		NullReporter.Instance.StageCompleted(Stage.Generate, TimeSpan.Zero);
	}
}
