using System.ComponentModel;
using CommunityToolkit.Diagnostics;
using DataverseGen.Core.Config;
using DataverseGen.Core.DataConverter;
using DataverseGen.Core.Pipeline;
using Meziantou.Framework.Win32;
using Newtonsoft.Json;
using DataverseGen.Cli.Ui;
using Spectre.Console;
using Spectre.Console.Cli;
using DataverseGen.Core.Reporting;

namespace DataverseGen.Cli.Commands;

internal sealed class GenerateCommand : AsyncCommand<GenerateCommand.Settings>
{
	private const string DefaultLogPath = "dataversegen.log";
	private const int CancelledExitCode = 130;

	internal sealed class Settings : CommandSettings
	{
		[CommandOption("-c|--config <PATH>")]
		[Description("Path to the config file. Defaults to dataversegen.config.json in the current directory.")]
		[DefaultValue("dataversegen.config.json")]
		public string ConfigPath { get; init; } = "dataversegen.config.json";

		[CommandOption("-v|--verbose")]
		[Description("Show debug output and full stack traces on the console.")]
		public bool Verbose { get; init; }

		[CommandOption("--log [PATH]")]
		[Description("Write a full-detail log file (default: dataversegen.log in the current directory, overwritten per run).")]
		public FlagValue<string?>? Log { get; init; }

		[CommandOption("-p|--parallel <N>")]
		[Description("Number of concurrent Dataverse requests / file writes. Default: the server's recommendation, 1-8. 1 = sequential.")]
		public int? Parallel { get; init; }

		[CommandOption("--no-clear")]
		[Description("Do not remove previously generated files that were not written in this run (overrides ClearOutputDirectory).")]
		public bool NoClear { get; init; }

		public override ValidationResult Validate()
		{
			return Parallel is < 1
				? ValidationResult.Error("--parallel must be at least 1.")
				: ValidationResult.Success();
		}
	}

	public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
	{
		IAnsiConsole console = AnsiConsole.Console;
		Theme theme = Theme.For(console);
		FileLogReporter? fileLog = null;
		string? logPath = null;

		if (settings.Log is { IsSet: true })
		{
			logPath = string.IsNullOrWhiteSpace(settings.Log.Value) ? DefaultLogPath : settings.Log.Value;

			try
			{
				fileLog = new FileLogReporter(logPath);
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
			{
				ConsoleOutput.WriteError($"Cannot write log file '{logPath}': {e.Message}");

				return 1;
			}
		}

		Banner.Render(console,
			theme,
			Banner.CreateInfo(settings.ConfigPath, DescribeMode(settings, logPath)));

		using (fileLog)
		{
			// Ctrl+C cancels the run instead of killing the process, so the log is flushed and the exit code is 130.
			using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			ConsoleCancelEventHandler onCancel = (_, e) =>
			{
				e.Cancel = true;
				cancellation.Cancel();
			};

			Console.CancelKeyPress += onCancel;

			try
			{
				bool live = SpectreReporter.CanRenderLive(console, Console.IsOutputRedirected);
				SpectreReporter ui = new(console, theme, settings.Verbose, live);
				IGenerationReporter sinks = fileLog is null ? ui : new CompositeReporter(ui, fileLog);
				PerformanceCollector performance = new(sinks);

				return await Run(settings, console, theme, ui, performance, fileLog, logPath, cancellation.Token);
			}
			finally
			{
				Console.CancelKeyPress -= onCancel;
			}
		}
	}

	private static string DescribeMode(Settings settings, string? logPath)
	{
		string parallelism = settings.Parallel?.ToString() ?? "auto";

		return $"verbose {(settings.Verbose ? "on" : "off")} · log {(logPath is null ? "off" : logPath)} · parallelism {parallelism}";
	}

	private static async Task<int> Run(
		Settings settings,
		IAnsiConsole console,
		Theme theme,
		SpectreReporter ui,
		PerformanceCollector reporter,
		FileLogReporter? fileLog,
		string? logPath,
		CancellationToken cancellationToken)
	{
		try
		{
			reporter.Log(LogLevel.Debug, $"Config: {settings.ConfigPath}");

			// Credential prompt and connection-string validation run before the live display starts:
			// prompts and live rendering cannot overlap.
			ConfigModel config = GetConfig(settings.ConfigPath);
			ConfigValidation.Validate(config);

			DataverseConnector dataverseConnector = new(config.ConnectionString,
				config.EnableConnectionStringValidation,
				reporter);
			GenerationOptions options = new()
			{
				Parallelism = settings.Parallel,
				NoClear = settings.NoClear
			};

			await ui.RunAsync(() => GenerationPipeline.RunAsync(config, dataverseConnector, options, reporter, cancellationToken));

			PerformanceReport report = reporter.Build();
			SummaryRenderer.Render(console, theme, report, logPath);
			fileLog?.Log(LogLevel.Info, report.ToText());

			return 0;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			fileLog?.Log(LogLevel.Warning, "Cancelled by user.");
			console.WriteLine();
			console.Write(new Text($"{theme.WarnIcon} Cancelled.", Theme.Of(Theme.Warning)));
			console.WriteLine();

			return CancelledExitCode;
		}
		catch (Exception e)
		{
			// The console gets the error panel; the log file always gets the full exception.
			fileLog?.Log(LogLevel.Error, e.Message, e);
			console.WriteLine();
			ErrorPanel.Render(console, theme, e, settings.Verbose, logPath);

			return 1;
		}
	}

	private static ConfigModel GetConfig(string path)
	{
		if (!File.Exists(path))
		{
			throw new UserFacingException($"Config not found: {path}",
				"Run `dataversegen init` to create an example config, or pass --config <PATH>.");
		}

		string json = File.ReadAllText(path);
		ConfigModel? deserializedObject = JsonConvert.DeserializeObject<ConfigModel>(json);
		Guard.IsNotNull(deserializedObject);

		return deserializedObject.ConnectionString is not (null or "")
			? deserializedObject
			: GetCredentials(deserializedObject);
	}

	private static ConfigModel GetCredentials(ConfigModel deserializedObject)
	{
		string credentialKey = $"DataverseGen-{deserializedObject.Namespace}";
		Credential? creds = CredentialManager.ReadCredential(credentialKey);

		if (creds is not null)
		{
			deserializedObject.ConnectionString = creds.Password;

			return deserializedObject;
		}

		CredentialResult? newCreds = null;

		while (newCreds is null)
		{
			newCreds = CredentialManager.PromptForCredentials(captionText:"New connection string prompt",
				messageText:$"Provide valid connection string for {credentialKey}",
				userName:"DO NOT USE",
				saveCredential:CredentialSaveOption.Hidden);
		}

		CredentialManager.WriteCredential(credentialKey,
			"UNUSED",
			newCreds.Password,
			CredentialPersistence.LocalMachine);

		deserializedObject.ConnectionString = newCreds.Password;

		return deserializedObject;
	}
}
