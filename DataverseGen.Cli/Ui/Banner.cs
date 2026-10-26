using System.Reflection;
using System.Runtime.InteropServices;
using Spectre.Console;

namespace DataverseGen.Cli.Ui;

/// <summary>Everything the header shows; optional fields are omitted when null.</summary>
internal sealed record BannerInfo(
	string Version,
	string Runtime,
	string WorkingDirectory,
	string? ConfigPath = null,
	string? Mode = null);

/// <summary>Compact header: name and version on the first line, context below.</summary>
internal static class Banner
{
	private const int LogoWidth = 72;

	private static readonly string[] Logo = """
	    ____        __       _    __                       ______
	   / __ \____ _/ /_____ | |  / /__  _____________     / ____/__  ____
	  / / / / __ `/ __/ __ `/ | / / _ \/ ___/ ___/ _ \   / / __/ _ \/ __ \
	 / /_/ / /_/ / /_/ /_/ /| |/ /  __/ /  (__  )  __/  / /_/ /  __/ / / /
	/_____/\__,_/\__/\__,_/ |___/\___/_/  /____/\___/   \____/\___/_/ /_/
	""".ReplaceLineEndings("\n").Split('\n');

	public static BannerInfo CreateInfo(string? configPath = null, string? mode = null)
	{
		return new BannerInfo(GetVersion(),
			RuntimeInformation.FrameworkDescription,
			Directory.GetCurrentDirectory(),
			configPath,
			mode);
	}

	public static string GetVersion()
	{
		Assembly? assembly = Assembly.GetEntryAssembly();
		string? version = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
			?? assembly?.GetName().Version?.ToString();

		// Drop the build metadata (commit hash) that GitVersion appends after '+'.
		int plus = version?.IndexOf('+') ?? -1;

		return plus > 0 ? version![..plus] : version ?? "unknown";
	}

	public static void Render(IAnsiConsole console, Theme theme, BannerInfo info)
	{
		if (console.Profile.Width >= LogoWidth)
		{
			RenderLogo(console);
		}

		console.Write(new Markup(
			$"[bold {Theme.Accent.ToMarkup()}]{theme.Arrow} DataverseGen[/] [{Theme.Muted.ToMarkup()}]{Markup.Escape(info.Version)} {theme.Dot} {Markup.Escape(info.Runtime)}[/]"));
		console.WriteLine();

		Grid grid = new();
		grid.AddColumn(new GridColumn().NoWrap().PadLeft(2).PadRight(2));
		grid.AddColumn();

		AddRow(grid, "cwd", info.WorkingDirectory);

		if (info.ConfigPath is not null)
		{
			AddRow(grid, "config", info.ConfigPath);
		}

		if (info.Mode is not null)
		{
			AddRow(grid, "mode", info.Mode);
		}

		console.Write(grid);
		console.WriteLine();
	}

	/// <summary>The ASCII logo with a vertical gradient from the accent colour to a lighter tone.</summary>
	private static void RenderLogo(IAnsiConsole console)
	{
		Color from = Theme.Accent;
		Color to = new(245, 190, 160);

		for (int i = 0; i < Logo.Length; i++)
		{
			double t = Logo.Length == 1 ? 0 : (double)i / (Logo.Length - 1);
			Color color = new((byte)(from.R + ((to.R - from.R) * t)),
				(byte)(from.G + ((to.G - from.G) * t)),
				(byte)(from.B + ((to.B - from.B) * t)));

			console.Write(new Text(Logo[i], new Style(color)));
			console.WriteLine();
		}

		console.WriteLine();
	}

	private static void AddRow(Grid grid, string label, string value)
	{
		grid.AddRow(new Markup($"[{Theme.Muted.ToMarkup()}]{label}[/]"), new Text(value));
	}
}
