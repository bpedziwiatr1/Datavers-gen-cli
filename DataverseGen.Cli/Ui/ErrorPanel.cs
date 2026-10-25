using DataverseGen.Core.Reporting;
using Newtonsoft.Json;
using Spectre.Console;

namespace DataverseGen.Cli.Ui;

/// <summary>An error with an actionable hint, shown without a stack trace.</summary>
internal sealed class UserFacingException : Exception
{
	public UserFacingException(string message, string hint) : base(message)
	{
		Hint = hint;
	}

	public string Hint { get; }
}

/// <summary>Short message + hint; the stack trace only with <c>--verbose</c> (it is always in the log file).</summary>
internal static class ErrorPanel
{
	public static void Render(IAnsiConsole console, Theme theme, Exception exception, bool verbose, string? logPath)
	{
		string message = SecretMasker.MaskSecrets(exception.Message);
		string hint = GetHint(exception, verbose, logPath);

		Grid body = new();
		body.AddColumn();
		body.AddRow(new Text(message));
		body.AddEmptyRow();
		body.AddRow(new Markup($"[{Theme.Muted.ToMarkup()}]{theme.Arrow} {Markup.Escape(hint)}[/]"));

		if (verbose)
		{
			body.AddEmptyRow();
			body.AddRow(new Text(SecretMasker.MaskSecrets(exception.ToString()), Theme.Of(Theme.Muted)));
		}

		console.Write(new Panel(body)
		   .Header($"[bold {Theme.Error.ToMarkup()}] {theme.FailIcon} Error [/]")
		   .Border(BoxBorder.Rounded)
		   .BorderStyle(Theme.Of(Theme.Error))
		   .Expand());
	}

	internal static string GetHint(Exception exception, bool verbose, string? logPath)
	{
		string? specific = exception switch
		{
			UserFacingException user => user.Hint,
			JsonException =>
				"dataversegen.config.json is not valid JSON; check commas and quotes.",
			InvalidOperationException => "Fix the configuration (see README, Configuration Fields).",
			_ when exception.Message.Contains("Connection did not connect", StringComparison.OrdinalIgnoreCase) =>
				"Check ConnectionString, network access and credentials.",
			_ => null
		};

		string more = verbose
			? string.Empty
			: logPath is null
				? " Re-run with -v for the stack trace, or --log for a full log file."
				: $" Re-run with -v for the stack trace; full details are in {logPath}.";

		return specific is null ? more.Trim() : $"{specific}{more}";
	}
}
