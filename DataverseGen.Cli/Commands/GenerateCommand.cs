using System.ComponentModel;
using CommunityToolkit.Diagnostics;
using DataverseGen.Core.Config;
using DataverseGen.Core.CustomApi;
using DataverseGen.Core.DataConverter;
using DataverseGen.Core.Generators.Scriban;
using DataverseGen.Core.Metadata;
using Meziantou.Framework.Win32;
using Newtonsoft.Json;
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
		FileLogReporter? fileLog = null;

		if (settings.Log is { IsSet: true })
		{
			string logPath = string.IsNullOrWhiteSpace(settings.Log.Value) ? DefaultLogPath : settings.Log.Value;

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

		using (fileLog)
		{
			ConsoleReporter console = new(settings.Verbose);
			IGenerationReporter reporter = fileLog is null ? console : new CompositeReporter(console, fileLog);

			return Run(settings, reporter);
		}
	}

	private static int Run(Settings settings, IGenerationReporter reporter)
	{
		try
		{
			reporter.Log(LogLevel.Debug, $"Config: {settings.ConfigPath}");
			ConfigModel config = GetConfig(settings.ConfigPath);
			ConfigValidation.Validate(config);
			DataverseConnector dataverseConnector = new(config.ConnectionString,
				config.EnableConnectionStringValidation,
				reporter);

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

			return 0;
		}
		catch (Exception e)
		{
			reporter.Log(LogLevel.Error, e.Message, e);

			return 1;
		}
	}

	private static ConfigModel GetConfig(string path)
	{
		if (!File.Exists(path))
		{
			throw new FileNotFoundException("Config not found", path);
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
