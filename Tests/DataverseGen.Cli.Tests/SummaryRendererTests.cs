using DataverseGen.Cli.Ui;
using DataverseGen.Core.Reporting;
using Spectre.Console.Testing;

namespace DataverseGen.Cli.Tests;

[TestClass]
public class SummaryRendererTests
{
	private static TestConsole CreateConsole()
	{
		TestConsole console = new();
		console.Profile.Width = 160;

		return console;
	}

	private static PerformanceReport BuildReport(Action<PerformanceCollector>? extra = null)
	{
		PerformanceCollector collector = new();
		collector.ItemCompleted(new ItemResult
		{
			Kind = ItemKind.Entity, Name = "account", Detail = "120 fields", Elapsed = TimeSpan.FromMilliseconds(340)
		});
		collector.ItemCompleted(new ItemResult
		{
			Kind = ItemKind.Entity, Name = "account", Phase = ItemPhase.Generate, Elapsed = TimeSpan.FromMilliseconds(12), Files = 3, Bytes = 2048
		});
		collector.ItemCompleted(new ItemResult
		{
			Kind = ItemKind.CustomApi, Name = "new_ping", Detail = "1 parameters · 0 response properties", Elapsed = TimeSpan.FromMilliseconds(50)
		});
		collector.ItemCompleted(new ItemResult
		{
			Kind = ItemKind.CustomApi, Name = "new_ping", Phase = ItemPhase.Generate, Elapsed = TimeSpan.FromMilliseconds(4), Files = 1, Bytes = 512
		});
		collector.StageCompleted(Stage.Entities, TimeSpan.FromMilliseconds(400));
		collector.StageCompleted(Stage.Generate, TimeSpan.FromMilliseconds(30));
		extra?.Invoke(collector);

		return collector.Build();
	}

	private static string[] Columns(string line)
	{
		return line.Split('│', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
	}

	[TestMethod]
	public void Render_EntityAndCustomApiTablesShareTheSameColumns()
	{
		TestConsole console = CreateConsole();

		SummaryRenderer.Render(console, new Theme(true), BuildReport(), null);

		string[] headerLines = console.Output.Split('\n')
		   .Where(line => line.Contains("Retrieve ms"))
		   .ToArray();

		Assert.HasCount(2, headerLines);
		CollectionAssert.AreEqual(Columns(headerLines[0]), Columns(headerLines[1]));
		Assert.Contains("Entities (1)", console.Output);
		Assert.Contains("Custom APIs (1)", console.Output);
		Assert.Contains("account", console.Output);
		Assert.Contains("120 fields", console.Output);
		Assert.Contains("340", console.Output);
		Assert.Contains("new_ping", console.Output);
	}

	[TestMethod]
	public void Render_OmitsTableWithoutItems()
	{
		TestConsole console = CreateConsole();
		PerformanceCollector collector = new();
		collector.ItemCompleted(new ItemResult { Kind = ItemKind.Entity, Name = "account" });

		SummaryRenderer.Render(console, new Theme(true), collector.Build(), null);

		Assert.DoesNotContain("Custom APIs", console.Output);
	}

	[TestMethod]
	public void Render_ListsWarningsAndFailuresAtTheEndWithFooter()
	{
		TestConsole console = CreateConsole();
		PerformanceReport report = BuildReport(c =>
		{
			c.ItemCompleted(new ItemResult { Kind = ItemKind.Entity, Name = "missing", Status = ItemStatus.NotFound });
			c.Log(LogLevel.Warning, "No custom APIs configured");
		});

		SummaryRenderer.Render(console, new Theme(true), report, "run.log");

		Assert.Contains("Warnings and failures", console.Output);
		Assert.Contains("Entity missing: not found", console.Output);
		Assert.Contains("No custom APIs configured", console.Output);
		Assert.Contains("2 warning(s)/failure(s)", console.Output);
		Assert.Contains("Log: run.log", console.Output);
	}

	[TestMethod]
	public void Render_WithoutIssuesHasNoWarningSectionAndNoLogLine()
	{
		TestConsole console = CreateConsole();

		SummaryRenderer.Render(console, new Theme(true), BuildReport(), null);

		Assert.DoesNotContain("Warnings and failures", console.Output);
		Assert.DoesNotContain("Log:", console.Output);
		Assert.Contains("Done in", console.Output);
		Assert.Contains("4 files", console.Output);
	}

	[TestMethod]
	public void Render_ShowsPerformancePanel()
	{
		TestConsole console = CreateConsole();

		SummaryRenderer.Render(console,
			new Theme(true),
			BuildReport(c => c.RequestCompleted(Stage.Entities, TimeSpan.FromMilliseconds(10))),
			null);

		Assert.Contains("Performance", console.Output);
		Assert.Contains("Stage durations (ms)", console.Output);
		Assert.Contains("Dataverse requests", console.Output);
		Assert.Contains("speed-up 1.0x", console.Output);
		Assert.Contains("Slowest", console.Output);
	}

	[TestMethod]
	public void Render_ListsOutputsThatBelongToNoItem()
	{
		TestConsole console = CreateConsole();
		PerformanceCollector collector = new();
		collector.ItemCompleted(new ItemResult
		{
			Kind = ItemKind.Output, Name = "XrmServiceContext.cs", Phase = ItemPhase.Generate, Files = 1, Bytes = 4096
		});

		SummaryRenderer.Render(console, new Theme(true), collector.Build(), null);

		Assert.Contains("Also written: XrmServiceContext.cs (4.0 KB)", console.Output);
	}

	[TestMethod]
	public void Render_MasksSecretsAndIgnoresMarkup()
	{
		TestConsole console = CreateConsole();
		PerformanceCollector collector = new();
		collector.ItemCompleted(new ItemResult
		{
			Kind = ItemKind.Entity, Name = "[red]x[/]", Status = ItemStatus.Failed, Detail = "boom ClientSecret=topsecret;Url=u"
		});

		SummaryRenderer.Render(console, new Theme(true), collector.Build(), null);

		Assert.Contains("[red]x[/]", console.Output);
		Assert.DoesNotContain("topsecret", console.Output);
		Assert.Contains("ClientSecret=***", console.Output);
	}

	[TestMethod]
	public void Render_UsesAsciiIconsWithoutUnicode()
	{
		TestConsole console = CreateConsole();

		SummaryRenderer.Render(console, new Theme(false), BuildReport(), null);

		Assert.DoesNotContain("✔", console.Output);
		Assert.Contains("+", console.Output);
	}
}
