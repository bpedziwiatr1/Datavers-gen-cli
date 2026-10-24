using System.Globalization;
using DataverseGen.Core.Reporting;
using Spectre.Console;

namespace DataverseGen.Cli.Ui;

/// <summary>Result tables (entities and custom APIs share one layout), issues, performance panel and footer.</summary>
internal static class SummaryRenderer
{
	public static void Render(IAnsiConsole console, Theme theme, PerformanceReport report, string? logPath)
	{
		console.WriteLine();
		RenderTable(console, theme, "Entities", report.Items.Where(item => item.Kind == ItemKind.Entity).ToArray());
		RenderTable(console, theme, "Custom APIs", report.Items.Where(item => item.Kind == ItemKind.CustomApi).ToArray());
		RenderOutputs(console, theme, report);
		RenderIssues(console, theme, report);
		RenderPerformance(console, theme, report);
		RenderFooter(console, theme, report, logPath);
	}

	private static void RenderTable(IAnsiConsole console, Theme theme, string title, IReadOnlyList<ItemSummary> items)
	{
		if (items.Count == 0)
		{
			return;
		}

		Table table = new Table()
		   .Border(TableBorder.Rounded)
		   .BorderStyle(Theme.Of(Theme.Muted))
		   .Title($"[bold]{title}[/] [{Theme.Muted.ToMarkup()}]({items.Count})[/]");

		table.AddColumn(new TableColumn(string.Empty).NoWrap());
		table.AddColumn(new TableColumn("Name").NoWrap());
		table.AddColumn("Details");
		table.AddColumn(new TableColumn("Retrieve ms").RightAligned());
		table.AddColumn(new TableColumn("Generate ms").RightAligned());
		table.AddColumn(new TableColumn("Files").RightAligned());

		foreach (ItemSummary item in items)
		{
			string color = Theme.ColorFor(item.Status).ToMarkup();

			table.AddRow(new Markup($"[{color}]{theme.IconFor(item.Status)}[/]"),
				new Text(SecretMasker.MaskSecrets(item.Name ?? string.Empty)),
				new Text(SecretMasker.MaskSecrets(item.Detail ?? string.Empty), Theme.Of(Theme.Muted)),
				new Text(Format.Millis(item.RetrieveElapsed)),
				new Text(Format.Millis(item.GenerateElapsed)),
				new Text(item.Files.ToString(CultureInfo.InvariantCulture)));
		}

		console.Write(table);
	}

	private static void RenderOutputs(IAnsiConsole console, Theme theme, PerformanceReport report)
	{
		string[] outputs = report.Items
		   .Where(item => item.Kind == ItemKind.Output)
		   .Select(item => $"{item.Name} ({Format.Bytes(item.Bytes)})")
		   .ToArray();

		if (outputs.Length == 0)
		{
			return;
		}

		console.Write(new Text($"{theme.Arrow} Also written: {string.Join(", ", outputs)}", Theme.Of(Theme.Muted)));
		console.WriteLine();
	}

	private static void RenderIssues(IAnsiConsole console, Theme theme, PerformanceReport report)
	{
		List<(ItemStatus Status, string Text)> issues = report.Items
		   .Where(item => item.Status != ItemStatus.Ok)
		   .Select(item => (item.Status, Text: Describe(item)))
		   .ToList();

		issues.AddRange(report.Messages.Select(message => (ItemStatus.Warning, message)));

		if (issues.Count == 0)
		{
			return;
		}

		console.WriteLine();
		console.Write(new Text("Warnings and failures", new Style(decoration: Decoration.Bold)));
		console.WriteLine();

		foreach ((ItemStatus status, string text) in issues)
		{
			console.Write(new Text($"  {theme.IconFor(status)} {SecretMasker.MaskSecrets(text)}", Theme.Of(Theme.ColorFor(status))));
			console.WriteLine();
		}
	}

	private static string Describe(ItemSummary item)
	{
		string kind = item.Kind == ItemKind.CustomApi ? "Custom API" : "Entity";
		string state = item.Status switch
		{
			ItemStatus.Failed => "failed",
			ItemStatus.NotFound => "not found",
			_ => "warning"
		};
		string detail = string.IsNullOrEmpty(item.Detail) ? string.Empty : $" - {item.Detail}";

		return $"{kind} {item.Name}: {state}{detail}";
	}

	private static void RenderPerformance(IAnsiConsole console, Theme theme, PerformanceReport report)
	{
		BarChart chart = new BarChart()
		   .Width(50)
		   .Label("[bold]Stage durations (ms)[/]")
		   .CenterLabel()
		   .ShowValues();

		foreach (KeyValuePair<Stage, TimeSpan> stage in report.Stages)
		{
			chart.AddItem(stage.Key.ToString(), Math.Round(stage.Value.TotalMilliseconds), Theme.Accent);
		}

		CultureInfo c = CultureInfo.InvariantCulture;
		Grid figures = new();
		figures.AddColumn(new GridColumn().NoWrap().PadRight(2));
		figures.AddColumn();

		AddFigure(figures, "Wall clock", Format.Duration(report.WallClock));
		AddFigure(figures, "Dataverse requests", $"{report.RequestCount} ({Format.Duration(report.RequestTime)} total)");
		AddFigure(figures, "Parallelism", report.DegreeOfParallelism.ToString(c));
		AddFigure(figures, "Files written", $"{report.FilesWritten} ({Format.Bytes(report.BytesWritten)})");
		AddFigure(figures, "Throughput", string.Create(c, $"{report.EntitiesPerSecond:F1} entities/s, {report.FilesPerSecond:F1} files/s"));
		AddFigure(figures, "Item time / wall", string.Create(c, $"{Format.Duration(report.SumOfItemTimes)} (speed-up {report.SpeedUp:F1}x)"));
		AddFigure(figures, "Peak memory", Format.Bytes(report.PeakWorkingSetBytes));

		foreach ((ItemSummary item, int index) in report.Slowest.Select((item, index) => (item, index)))
		{
			AddFigure(figures, index == 0 ? "Slowest" : string.Empty, $"{item.Name} ({Format.Duration(item.TotalElapsed)})");
		}

		Grid layout = new();
		layout.AddColumn(new GridColumn().PadRight(4));
		layout.AddColumn();
		// BarChart cannot render without items (e.g. a run that failed before any stage completed).
		if (report.Stages.Count > 0)
		{
			layout.AddRow(chart, figures);
		}
		else
		{
			layout.AddRow(new Text(string.Empty), figures);
		}

		console.Write(new Panel(layout)
		   .Header("[bold] Performance [/]")
		   .Border(BoxBorder.Rounded)
		   .BorderStyle(Theme.Of(Theme.Muted)));
	}

	private static void AddFigure(Grid grid, string label, string value)
	{
		grid.AddRow(new Markup($"[{Theme.Muted.ToMarkup()}]{Markup.Escape(label)}[/]"), new Text(value));
	}

	private static void RenderFooter(IAnsiConsole console, Theme theme, PerformanceReport report, string? logPath)
	{
		int issues = report.Items.Count(item => item.Status != ItemStatus.Ok) + report.Messages.Count;
		Color color = issues == 0 ? Theme.Success : Theme.Warning;
		string icon = issues == 0 ? theme.OkIcon : theme.WarnIcon;
		string issueText = issues == 0 ? string.Empty : $" {theme.Dot} {issues} warning(s)/failure(s)";

		console.Write(new Text(
			$"{icon} Done in {Format.Duration(report.WallClock)} {theme.Dot} {report.FilesWritten} files ({Format.Bytes(report.BytesWritten)}){issueText}",
			new Style(color, decoration: Decoration.Bold)));
		console.WriteLine();

		if (logPath is not null)
		{
			console.Write(new Text($"{theme.Arrow} Log: {logPath}", Theme.Of(Theme.Muted)));
			console.WriteLine();
		}
	}
}
