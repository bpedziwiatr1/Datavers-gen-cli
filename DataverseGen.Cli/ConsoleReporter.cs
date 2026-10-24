using DataverseGen.Core.Reporting;
using Spectre.Console;
using LogLevel = DataverseGen.Core.Reporting.LogLevel;

namespace DataverseGen.Cli;

/// <summary>Console sink: Debug lines only with <c>--verbose</c>, stack traces only with <c>--verbose</c>.</summary>
internal sealed class ConsoleReporter : IGenerationReporter
{
	private readonly bool _verbose;

	public ConsoleReporter(bool verbose)
	{
		_verbose = verbose;
	}

	public void Log(LogLevel level, string message, Exception? exception = null)
	{
		if (level == LogLevel.Debug && !_verbose)
		{
			return;
		}

		ConsoleOutput.WriteLine(SecretMasker.MaskSecrets(message), GetColor(level));

		if (exception is not null && _verbose)
		{
			ConsoleOutput.WriteLine(SecretMasker.MaskSecrets(exception.ToString()), Color.Grey);
		}
	}

	public void StageStarted(Stage stage, int total)
	{
		string? text = stage switch
		{
			Stage.Entities => $"Retrieving entities ({total})...",
			Stage.CustomApis => $"Loading custom APIs ({total})...",
			Stage.Generate => "Generating code...",
			_ => null
		};

		if (text is not null)
		{
			ConsoleOutput.WriteInfo(text);
		}
	}

	public void ItemCompleted(ItemResult result)
	{
		string kind = result.Kind == ItemKind.Entity ? "Entity" : "Custom API";
		string detail = string.IsNullOrEmpty(result.Detail) ? string.Empty : $" · {result.Detail}";

		switch (result.Status)
		{
			case ItemStatus.Ok:
				ConsoleOutput.WriteSuccess(SecretMasker.MaskSecrets($"✔ {result.Name}{detail}"));

				break;

			case ItemStatus.Failed:
				ConsoleOutput.WriteError(SecretMasker.MaskSecrets($"✘ {kind} failed: {result.Name}{detail}"));

				break;

			case ItemStatus.NotFound:
				ConsoleOutput.WriteWarning(SecretMasker.MaskSecrets($"⚠ {kind} not found: {result.Name}{detail}"));

				break;

			default:
				ConsoleOutput.WriteWarning(SecretMasker.MaskSecrets($"⚠ {kind} {result.Name}{detail}"));

				break;
		}
	}

	public void StageCompleted(Stage stage, TimeSpan elapsed)
	{
		ConsoleOutput.WriteInfo($"{stage} done in {elapsed:g}");
	}

	private static Color GetColor(LogLevel level)
	{
		return level switch
		{
			LogLevel.Debug => Color.Grey,
			LogLevel.Success => Color.Green,
			LogLevel.Warning => Color.Olive,
			LogLevel.Error => Color.Red,
			_ => Color.DarkCyan
		};
	}
}
