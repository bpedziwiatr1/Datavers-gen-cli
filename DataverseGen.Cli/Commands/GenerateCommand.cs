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
using static DataverseGen.Core.ColorConsole;

namespace DataverseGen.Cli.Commands;

internal sealed class GenerateCommand : Command<GenerateCommand.Settings>
{
	internal sealed class Settings : CommandSettings
	{
		[CommandOption("-c|--config <PATH>")]
		[Description("Path to the config file. Defaults to dataversegen.config.json in the current directory.")]
		[DefaultValue("dataversegen.config.json")]
		public string ConfigPath { get; init; } = "dataversegen.config.json";
	}

	public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
	{
		try
		{
			ConfigModel config = GetConfig(settings.ConfigPath);
			ConfigValidation.Validate(config);
			DataverseConnector dataverseConnector = new(config.ConnectionString,
				config.EnableConnectionStringValidation);

			DataverseMetadataConverter dataConverter = new(dataverseConnector,
				config.ThrowOnEntityNotFound,
				config.Entities);

			MappingEntity[] data = dataConverter.GetMappedEntities();
			WriteLine(@"Finish Load data");

			DataverseCustomApiConverter customApiConverter = new(dataverseConnector, config.CustomApis);
			CustomApiModel[] customApis = customApiConverter.GetCustomApis();

			Context generatorContext = new()
			{
				Namespace = config.Namespace,
				Entities = data,
				CustomApis = customApis
			};

			WriteLine(@"Start generator");

			switch (config.TemplateEngine.Name)
			{
				case "scriban":
					ScribanRun(config, generatorContext);

					break;

				default:
					WriteLine($@"Unsupported generator {config.TemplateEngine}, run scriban");
					ScribanRun(config, generatorContext);

					break;
			}

			return 0;
		}
		catch (Exception e)
		{
			WriteError(e.ToString());
			WriteError(@"#StackTrace:");
			WriteError(e.StackTrace);

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

	private static void ScribanRun(ConfigModel config, Context context)
	{
		ScribanGenerator scribanGenerator = new(config.TemplateName,
			config.TemplateDirectoryName,
			string.IsNullOrWhiteSpace(config.EntitiesOutDirectory) ? config.OutDirectory : config.EntitiesOutDirectory,
			context,
			config.TemplateEngine,
			config.CustomApiOutDirectory,
			config.LegacyEntityFileNames);

		scribanGenerator.GenerateTemplate();
	}
}
