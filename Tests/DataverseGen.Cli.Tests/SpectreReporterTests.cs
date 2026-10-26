using DataverseGen.Cli.Ui;
using DataverseGen.Core.Reporting;
using Spectre.Console.Testing;

namespace DataverseGen.Cli.Tests;

[TestClass]
public class SpectreReporterTests
{
	private static TestConsole CreateConsole(bool interactive = false)
	{
		TestConsole console = new();
		console.Profile.Width = 140;
		console.Profile.Capabilities.Interactive = interactive;

		return console;
	}

	[TestMethod]
	public void CanRenderLive_RequiresInteractiveConsoleAndNonRedirectedOutput()
	{
		Assert.IsTrue(SpectreReporter.CanRenderLive(CreateConsole(true), false));
		Assert.IsFalse(SpectreReporter.CanRenderLive(CreateConsole(true), true));
		Assert.IsFalse(SpectreReporter.CanRenderLive(CreateConsole(false), false));
	}

	[TestMethod]
	public void PlainMode_WritesStageAndItemLines()
	{
		TestConsole console = CreateConsole();
		SpectreReporter reporter = new(console, new Theme(true), false, false);

		reporter.Run(() =>
		{
			reporter.StageStarted(Stage.Entities, 2);
			reporter.ItemCompleted(new ItemResult
			{
				Kind = ItemKind.Entity, Name = "account", Detail = "5 fields", Elapsed = TimeSpan.FromMilliseconds(20)
			});
			reporter.ItemCompleted(new ItemResult { Kind = ItemKind.Entity, Name = "nope", Status = ItemStatus.NotFound });
			reporter.StageCompleted(Stage.Entities, TimeSpan.FromSeconds(2));
		});

		Assert.Contains("› Entities (2)", console.Output);
		Assert.Contains("✔ account · 5 fields (20 ms)", console.Output);
		Assert.Contains("⚠ Entity not found: nope", console.Output);
		Assert.Contains("Entities done in 2.00 s", console.Output);
		Assert.DoesNotContain("\u001b", console.Output);
	}

	[TestMethod]
	public void Log_HidesDebugAndStackTraceUnlessVerbose()
	{
		TestConsole quiet = CreateConsole();
		TestConsole verbose = CreateConsole();
		InvalidOperationException error = new("boom");

		foreach ((TestConsole console, bool isVerbose) in new[] { (quiet, false), (verbose, true) })
		{
			SpectreReporter reporter = new(console, new Theme(true), isVerbose, false);
			reporter.Log(LogLevel.Debug, "debug-line");
			reporter.Log(LogLevel.Error, "error-line", error);
		}

		Assert.DoesNotContain("debug-line", quiet.Output);
		Assert.Contains("error-line", quiet.Output);
		Assert.DoesNotContain("InvalidOperationException", quiet.Output);
		Assert.Contains("debug-line", verbose.Output);
		Assert.Contains("InvalidOperationException", verbose.Output);
	}

	[TestMethod]
	public void Log_IgnoresMarkup()
	{
		TestConsole console = CreateConsole();
		SpectreReporter reporter = new(console, new Theme(true), false, false);

		reporter.Log(LogLevel.Warning, "[red]bad[/] and more");

		Assert.Contains("[red]bad[/]", console.Output);
	}

	[TestMethod]
	public void PlainMode_LabelsOutputItemsAsFiles()
	{
		TestConsole console = CreateConsole();
		SpectreReporter reporter = new(console, new Theme(true), false, false);

		reporter.ItemCompleted(new ItemResult
		{
			Kind = ItemKind.Output, Name = "XrmServiceContext.cs", Phase = ItemPhase.Generate, Status = ItemStatus.Failed
		});

		Assert.Contains("File failed: XrmServiceContext.cs", console.Output);
		Assert.DoesNotContain("Entity failed", console.Output);
	}

	[TestMethod]
	public void LiveMode_RendersProgressWithoutThrowingAndPrintsOnlyProblems()
	{
		TestConsole console = CreateConsole(true);
		SpectreReporter reporter = new(console, new Theme(true), false, true);

		reporter.Run(() =>
		{
			reporter.StageStarted(Stage.Connect, 1);
			reporter.StageCompleted(Stage.Connect, TimeSpan.FromMilliseconds(5));
			reporter.StageStarted(Stage.Entities, 2);
			reporter.ItemCompleted(new ItemResult { Kind = ItemKind.Entity, Name = "account" });
			reporter.ItemCompleted(new ItemResult { Kind = ItemKind.Entity, Name = "gone", Status = ItemStatus.NotFound });
			reporter.StageCompleted(Stage.Entities, TimeSpan.FromMilliseconds(5));
			reporter.StageStarted(Stage.Generate, 1);
			reporter.ItemCompleted(new ItemResult
			{
				Kind = ItemKind.Entity, Name = "account", Phase = ItemPhase.Generate, Files = 3
			});
			reporter.StageCompleted(Stage.Generate, TimeSpan.FromMilliseconds(5));
		});

		Assert.Contains("Entities", console.Output);
		Assert.Contains("Generate", console.Output);
		Assert.Contains("2/2", console.Output);
		Assert.Contains("Entity not found: gone", console.Output);
		Assert.DoesNotContain("✔ account", console.Output);
	}

	[TestMethod]
	public void LiveMode_ShowsItemsInFlightUntilTheyComplete()
	{
		TestConsole console = CreateConsole(true);
		SpectreReporter reporter = new(console, new Theme(true), false, true);

		reporter.Run(() =>
		{
			reporter.StageStarted(Stage.Generate, 3);
			reporter.ItemStarted(ItemKind.Entity, ItemPhase.Generate, "account");
			reporter.ItemCompleted(new ItemResult { Kind = ItemKind.Entity, Name = "account", Phase = ItemPhase.Generate, Files = 3 });
			reporter.ItemStarted(ItemKind.Entity, ItemPhase.Generate, "contact");
			reporter.ItemStarted(ItemKind.CustomApi, ItemPhase.Generate, "new_ping");
		});

		Assert.Contains("contact +1", console.Output);
		Assert.DoesNotContain("account +", console.Output);
	}

	[TestMethod]
	public void PlainMode_IgnoresItemStarted()
	{
		TestConsole console = CreateConsole();
		SpectreReporter reporter = new(console, new Theme(true), false, false);

		reporter.ItemStarted(ItemKind.Entity, ItemPhase.Retrieve, "account");

		Assert.AreEqual(string.Empty, console.Output.Trim());
	}

	[TestMethod]
	public void Truncate_ShortensLongNamesWithEllipsis()
	{
		Assert.AreEqual("abc", Format.Truncate("abc", 5));
		Assert.AreEqual("abcd…", Format.Truncate("abcdefgh", 5));
	}

	[TestMethod]
	public void LiveMode_GrowsTotalWhenMoreItemsThanAnnounced()
	{
		TestConsole console = CreateConsole(true);
		SpectreReporter reporter = new(console, new Theme(true), false, true);

		reporter.Run(() =>
		{
			reporter.StageStarted(Stage.Entities, 1);
			reporter.ItemCompleted(new ItemResult { Kind = ItemKind.Entity, Name = "a" });
			reporter.ItemCompleted(new ItemResult { Kind = ItemKind.Entity, Name = "activityparty" });
			reporter.StageCompleted(Stage.Entities, TimeSpan.Zero);
		});

		Assert.Contains("2/2", console.Output);
	}

	[TestMethod]
	public void LiveMode_PropagatesExceptionsFromThePipeline()
	{
		TestConsole console = CreateConsole(true);
		SpectreReporter reporter = new(console, new Theme(true), false, true);

		Assert.ThrowsExactly<InvalidOperationException>(
			() => reporter.Run(() => throw new InvalidOperationException("x")));
	}
}
