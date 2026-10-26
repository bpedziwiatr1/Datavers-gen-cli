using DataverseGen.Core.Reporting;

namespace DataverseGen.Core.Tests.Reporting;

[TestClass]
public class PerformanceCollectorTests
{
	private static ItemResult Item(
		ItemKind kind,
		string name,
		ItemPhase phase,
		int ms,
		int files = 0,
		long bytes = 0,
		ItemStatus status = ItemStatus.Ok,
		string? detail = null)
	{
		return new ItemResult
		{
			Kind = kind,
			Name = name,
			Phase = phase,
			Status = status,
			Detail = detail!,
			Elapsed = TimeSpan.FromMilliseconds(ms),
			Files = files,
			Bytes = bytes
		};
	}

	[TestMethod]
	public void Build_MergesRetrieveAndGeneratePhasesPerItem()
	{
		PerformanceCollector collector = new();
		collector.ItemCompleted(Item(ItemKind.Entity, "Account", ItemPhase.Retrieve, 100, detail: "10 fields"));
		collector.ItemCompleted(Item(ItemKind.Entity, "account", ItemPhase.Generate, 30, 3, 900));

		PerformanceReport report = collector.Build();

		ItemSummary item = report.Items.Single();
		Assert.AreEqual(TimeSpan.FromMilliseconds(100), item.RetrieveElapsed);
		Assert.AreEqual(TimeSpan.FromMilliseconds(30), item.GenerateElapsed);
		Assert.AreEqual(3, item.Files);
		Assert.AreEqual(900, item.Bytes);
		Assert.AreEqual("10 fields", item.Detail);
		Assert.AreEqual(3, report.FilesWritten);
		Assert.AreEqual(900, report.BytesWritten);
	}

	[TestMethod]
	public void Build_KeepsWorstStatus()
	{
		PerformanceCollector collector = new();
		collector.ItemCompleted(Item(ItemKind.CustomApi, "api", ItemPhase.Retrieve, 1));
		collector.ItemCompleted(Item(ItemKind.CustomApi, "api", ItemPhase.Generate, 1, status: ItemStatus.Failed));

		Assert.AreEqual(ItemStatus.Failed, collector.Build().Items.Single().Status);
	}

	[TestMethod]
	public void Build_ReturnsTopThreeSlowestItemsIgnoringOutputs()
	{
		PerformanceCollector collector = new();
		collector.ItemCompleted(Item(ItemKind.Entity, "a", ItemPhase.Retrieve, 10));
		collector.ItemCompleted(Item(ItemKind.Entity, "b", ItemPhase.Retrieve, 50));
		collector.ItemCompleted(Item(ItemKind.Entity, "c", ItemPhase.Retrieve, 30));
		collector.ItemCompleted(Item(ItemKind.CustomApi, "d", ItemPhase.Retrieve, 40));
		collector.ItemCompleted(Item(ItemKind.Output, "XrmServiceContext.cs", ItemPhase.Generate, 999, 1));

		PerformanceReport report = collector.Build();

		CollectionAssert.AreEqual(new[] { "b", "d", "c" }, report.Slowest.Select(i => i.Name).ToArray());
		Assert.AreEqual(1, report.FilesWritten);
	}

	[TestMethod]
	public void Build_Sequential_SpeedUpIsExactlyOne()
	{
		PerformanceCollector collector = new();
		collector.ItemCompleted(Item(ItemKind.Entity, "a", ItemPhase.Retrieve, 400));
		collector.StageCompleted(Stage.Entities, TimeSpan.FromMilliseconds(500));

		PerformanceReport report = collector.Build();

		Assert.AreEqual(1, report.DegreeOfParallelism);
		Assert.AreEqual(1.0, report.SpeedUp);
	}

	[TestMethod]
	public void Build_Parallel_SpeedUpIsItemTimeOverStageWallClock()
	{
		PerformanceCollector collector = new();

		for (int i = 0; i < 4; i++)
		{
			collector.ItemCompleted(Item(ItemKind.Entity, $"e{i}", ItemPhase.Retrieve, 400));
		}

		collector.StageCompleted(Stage.Entities, TimeSpan.FromMilliseconds(400));

		PerformanceReport report = collector.Build(4);

		Assert.AreEqual(4, report.DegreeOfParallelism);
		Assert.AreEqual(TimeSpan.FromMilliseconds(1600), report.SumOfItemTimes);
		Assert.AreEqual(4.0, report.SpeedUp, 0.0001);
	}

	[TestMethod]
	public void Build_ComputesThroughputFromStageDurations()
	{
		PerformanceCollector collector = new();
		collector.ItemCompleted(Item(ItemKind.Entity, "a", ItemPhase.Retrieve, 10));
		collector.ItemCompleted(Item(ItemKind.Entity, "b", ItemPhase.Retrieve, 10));
		collector.ItemCompleted(Item(ItemKind.Entity, "a", ItemPhase.Generate, 10, 3, 1));
		collector.ItemCompleted(Item(ItemKind.Entity, "b", ItemPhase.Generate, 10, 3, 1));
		collector.StageCompleted(Stage.Entities, TimeSpan.FromSeconds(1));
		collector.StageCompleted(Stage.Generate, TimeSpan.FromSeconds(2));

		PerformanceReport report = collector.Build();

		Assert.AreEqual(2.0, report.EntitiesPerSecond, 0.0001);
		Assert.AreEqual(3.0, report.FilesPerSecond, 0.0001);
		CollectionAssert.AreEqual(new[] { Stage.Entities, Stage.Generate }, report.Stages.Select(s => s.Key).ToArray());
	}

	[TestMethod]
	public void Build_CountsRequestsAndCollectsWarningsAndErrors()
	{
		PerformanceCollector collector = new();
		collector.RequestCompleted(Stage.Entities, TimeSpan.FromMilliseconds(20));
		collector.RequestCompleted(Stage.CustomApis, TimeSpan.FromMilliseconds(30));
		collector.Log(LogLevel.Debug, "ignored");
		collector.Log(LogLevel.Warning, "bad");
		collector.Log(LogLevel.Error, "worse");

		PerformanceReport report = collector.Build();

		Assert.AreEqual(2, report.RequestCount);
		Assert.AreEqual(TimeSpan.FromMilliseconds(50), report.RequestTime);
		CollectionAssert.AreEqual(new[] { "bad", "worse" }, report.Messages.ToArray());
		Assert.IsGreaterThan(0, report.PeakWorkingSetBytes);
	}

	[TestMethod]
	public void Collector_IsThreadSafe()
	{
		PerformanceCollector collector = new();

		Parallel.For(0,
			1000,
			new ParallelOptions { MaxDegreeOfParallelism = 8 },
			i =>
			{
				collector.ItemCompleted(Item(ItemKind.Entity, $"e{i % 100}", ItemPhase.Retrieve, 1));
				collector.ItemCompleted(Item(ItemKind.Entity, $"e{i % 100}", ItemPhase.Generate, 1, 1, 10));
				collector.RequestCompleted(Stage.Entities, TimeSpan.FromMilliseconds(1));
				collector.Log(LogLevel.Warning, "w");
			});

		PerformanceReport report = collector.Build(8);

		Assert.HasCount(100, report.Items);
		Assert.AreEqual(1000, report.FilesWritten);
		Assert.AreEqual(10000, report.BytesWritten);
		Assert.AreEqual(1000, report.RequestCount);
		Assert.HasCount(1000, report.Messages);
		Assert.AreEqual(TimeSpan.FromMilliseconds(2000), report.SumOfItemTimes);
	}

	[TestMethod]
	public void ToText_ContainsKeyFigures()
	{
		PerformanceCollector collector = new();
		collector.ItemCompleted(Item(ItemKind.Entity, "account", ItemPhase.Generate, 5, 3, 10));
		collector.StageCompleted(Stage.Generate, TimeSpan.FromMilliseconds(5));

		string text = collector.Build().ToText();

		Assert.Contains("Performance report", text);
		Assert.Contains("Stage Generate", text);
		Assert.Contains("Files written:      3", text);
		Assert.Contains("Entity account", text);
	}
}
