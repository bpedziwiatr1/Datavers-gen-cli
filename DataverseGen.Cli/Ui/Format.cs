using System.Globalization;

namespace DataverseGen.Cli.Ui;

internal static class Format
{
	public static string Duration(TimeSpan elapsed)
	{
		CultureInfo c = CultureInfo.InvariantCulture;

		if (elapsed.TotalSeconds >= 60)
		{
			return string.Create(c, $"{(int)elapsed.TotalMinutes} min {elapsed.Seconds:D2} s");
		}

		return elapsed.TotalSeconds >= 1
			? string.Create(c, $"{elapsed.TotalSeconds:F2} s")
			: string.Create(c, $"{elapsed.TotalMilliseconds:F0} ms");
	}

	/// <summary>Shortens <paramref name="text" /> to <paramref name="max" /> characters, ending with an ellipsis.</summary>
	public static string Truncate(string text, int max)
	{
		return text.Length <= max ? text : string.Concat(text.AsSpan(0, max - 1), "…");
	}

	/// <summary>Milliseconds for table cells; empty when the phase did not run.</summary>
	public static string Millis(TimeSpan elapsed)
	{
		return elapsed <= TimeSpan.Zero
			? "-"
			: string.Create(CultureInfo.InvariantCulture, $"{elapsed.TotalMilliseconds:F0}");
	}

	public static string Bytes(long bytes)
	{
		CultureInfo c = CultureInfo.InvariantCulture;

		return bytes switch
		{
			>= 1024 * 1024 => string.Create(c, $"{bytes / (1024.0 * 1024.0):F1} MB"),
			>= 1024 => string.Create(c, $"{bytes / 1024.0:F1} KB"),
			_ => string.Create(c, $"{bytes} B")
		};
	}
}
