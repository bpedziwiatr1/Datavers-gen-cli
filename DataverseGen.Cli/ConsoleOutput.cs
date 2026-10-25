using Spectre.Console;

namespace DataverseGen.Cli;

/// <summary>Plain (non-markup) colored console lines; text is never interpreted as Spectre markup.</summary>
internal static class ConsoleOutput
{
	public static void WriteLine(string text, Color? color = null)
	{
		AnsiConsole.Write(new Text(text, new Style(color)));
		AnsiConsole.WriteLine();
	}

	public static void WriteInfo(string text)
	{
		WriteLine(text, Color.DarkCyan);
	}

	public static void WriteSuccess(string text)
	{
		WriteLine(text, Color.Green);
	}

	public static void WriteWarning(string text)
	{
		WriteLine(text, Color.Olive);
	}

	public static void WriteError(string text)
	{
		WriteLine(text, Color.Red);
	}
}
