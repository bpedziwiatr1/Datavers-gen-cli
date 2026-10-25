using DataverseGen.Core.Reporting;
using Spectre.Console;

namespace DataverseGen.Cli.Ui;

/// <summary>Palette and icons shared by all UI components; falls back to ASCII when Unicode is unavailable.</summary>
internal sealed class Theme
{
	public static readonly Color Accent = new(217, 119, 87);
	public static readonly Color Muted = Color.Grey;
	public static readonly Color Success = Color.Green;
	public static readonly Color Warning = Color.Yellow;
	public static readonly Color Error = Color.Red;

	public Theme(bool unicode)
	{
		IsUnicode = unicode;
		OkIcon = unicode ? "✔" : "+";
		WarnIcon = unicode ? "⚠" : "!";
		FailIcon = unicode ? "✖" : "x";
		Arrow = unicode ? "›" : ">";
		Dot = unicode ? "·" : "-";
	}

	public bool IsUnicode { get; }

	public string OkIcon { get; }

	public string WarnIcon { get; }

	public string FailIcon { get; }

	public string Arrow { get; }

	public string Dot { get; }

	public static Theme For(IAnsiConsole console)
	{
		return new Theme(console.Profile.Capabilities.Unicode);
	}

	public string IconFor(ItemStatus status)
	{
		return status switch
		{
			ItemStatus.Ok => OkIcon,
			ItemStatus.Failed => FailIcon,
			_ => WarnIcon
		};
	}

	public static Color ColorFor(ItemStatus status)
	{
		return status switch
		{
			ItemStatus.Ok => Success,
			ItemStatus.Failed => Error,
			_ => Warning
		};
	}

	public static Style Of(Color color)
	{
		return new Style(color);
	}
}
