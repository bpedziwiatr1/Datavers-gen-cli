using System.ComponentModel;
using CommunityToolkit.Diagnostics;
using DataverseGen.Core.Config;
using DataverseGen.Core.CustomApi;
using DataverseGen.Core.DataConverter;
using DataverseGen.Core.Generators.Scriban;
using DataverseGen.Core.Metadata;
using Meziantou.Framework.Win32;
using Newtonsoft.Json;
using DataverseGen.Cli.Ui;
using Spectre.Console;
using Spectre.Console.Cli;
using DataverseGen.Core.Reporting;

namespace DataverseGen.Cli.Commands;

internal sealed class GenerateCommand : Command<GenerateCommand.Settings>
{
	private const string DefaultLogPath = "dataversegen.log";

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
	}

	public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
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
			bool live = SpectreReporter.CanRenderLive(console, Console.IsOutputRedirected);
			SpectreReporter ui = new(console, theme, settings.Verbose, live);
			IGenerationReporter sinks = fileLog is null ? ui : new CompositeReporter(ui, fileLog);
			PerformanceCollector performance = new(sinks);

			return Run(settings, console, theme, ui, performance, fileLog, logPath);
		}
	}

	private static string DescribeMode(Settings settings, string? logPath)
	{
		// Metadata retrieval and generation are sequential until the parallel pipeline lands.
		return $"verbose {(settings.Verbose ? "on" : "off")} · log {(logPath is null ? "off" : logPath)} · parallelism 1";
	}

	private static int Run(
		Settings settings,
		IAnsiConsole console,
		Theme theme,
		SpectreReporter ui,
		PerformanceCollector reporter,
		FileLogReporter? fileLog,
		string? logPath)
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

			ui.Run(() => Generate(config, dataverseConnector, reporter));

			PerformanceReport report = reporter.Build();
			SummaryRenderer.Render(console, theme, report, logPath);
			fileLog?.Log(LogLevel.Info, report.ToText());

			return 0;
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

	private static void Generate(ConfigModel config, DataverseConnector dataverseConnector, IGenerationReporter reporter)
	{
		DataverseMetadataConverter dataConverter = new(dataverseConnector,
			config.ThrowOnEntityNotFound,
			config.Entities,
			reporter);

		MappingEntity[] data = dataConverter.GetMappedEntities();

		DataverseCustomApiConverter customApiConverter = new(dataverseConnector, config.CustomApis, reporter);
		CustomApiModel[] customApis = customApiConverter.GetCustomApis();

		Context generatorContext = new()
		{
			Namespace = config.Namespace,
			Entities = data,
			CustomApis = customApis
		};

		switch (config.TemplateEngine.Name)
		{
			case "scriban":
				ScribanRun(config, generatorContext, reporter);

				break;

			default:
				reporter.Log(LogLevel.Warning, $@"Unsupported generator {config.TemplateEngine}, run scriban");
				ScribanRun(config, generatorContext, reporter);

				break;
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

	private static void ScribanRun(ConfigModel config, Context context, IGenerationReporter reporter)
	{
		ScribanGenerator scribanGenerator = new(config.TemplateName,
			config.TemplateDirectoryName,
			string.IsNullOrWhiteSpace(config.EntitiesOutDirectory) ? config.OutDirectory : config.EntitiesOutDirectory,
			context,
			config.TemplateEngine,
			config.CustomApiOutDirectory,
			config.LegacyEntityFileNames,
			reporter);

		scribanGenerator.GenerateTemplate();
	}
}
