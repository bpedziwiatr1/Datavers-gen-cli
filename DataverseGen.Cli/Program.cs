using System.Reflection;
using System.Runtime.InteropServices;
using CommunityToolkit.Diagnostics;
using DataverseGen.Core.Config;
using DataverseGen.Core.DataConverter;
using DataverseGen.Core.CustomApi;
using DataverseGen.Core.Generators.Scriban;
using DataverseGen.Core.Metadata;
using Meziantou.Framework.Win32;
using Newtonsoft.Json;
using static DataverseGen.Core.ColorConsole;

namespace DataverseGen.Cli;

//*
// NOTE!!
// DO NOT USE 'Trim unused assemblies'! It'll break json deserialization!
//*//
internal static class Program
{
	private const string DataverseGenConfigJson = "dataversegen.config.json";
	private const string NoPauseArgument = "--no-pause";

	private const string Title = @"
    ____        __       _    __                       ______         
   / __ \____ _/ /_____ | |  / /__  _____________     / ____/__  ____ 
  / / / / __ `/ __/ __ `/ | / / _ \/ ___/ ___/ _ \   / / __/ _ \/ __ \
 / /_/ / /_/ / /_/ /_/ /| |/ /  __/ /  (__  )  __/  / /_/ /  __/ / / /
/_____/\__,_/\__/\__,_/ |___/\___/_/  /____/\___/   \____/\___/_/ /_/                                                                       
";

	private static ConfigModel GetConfig()
	{
		if (!File.Exists(DataverseGenConfigJson))
		{
			throw new FileNotFoundException("Config not found in current directory",
				DataverseGenConfigJson);
		}

		string json = File.ReadAllText(DataverseGenConfigJson);
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

	private static void Main(string[] args)
	{
		// Pause and resize only when started in its own console window (e.g. double-clicked exe),
		// not when run from an existing terminal (e.g. as a dotnet tool).
		bool ownConsoleWindow = IsOwnConsoleWindow();
		bool pauseOnExit = ownConsoleWindow
			&& !Console.IsInputRedirected
			&& !args.Contains(NoPauseArgument, StringComparer.OrdinalIgnoreCase);

		try
		{
			if (ownConsoleWindow)
			{
				TrySetWindowSize(140, 30);
			}

			WriteLine(Title);
			WriteVersion();

			ConfigModel config = GetConfig();
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

			Context context = new()
			{
				Namespace = config.Namespace,
				Entities = data,
				CustomApis = customApis
			};

			WriteLine(@"Start generator");

			switch (config.TemplateEngine.Name)
			{
				case "scriban":
					ScribanRun(config, context);

					break;

				default:
					WriteLine($@"Unsupported generator {config.TemplateEngine}, run scriban");
					ScribanRun(config, context);

					break;
			}
		}
		catch (Exception e)
		{
			WriteError(e.ToString());
			WriteError(@"#StackTrace:");
			WriteError(e.StackTrace);

			throw;
		}
		finally
		{
			if (pauseOnExit)
			{
				WriteSuccess(@"Bye Bye, see you next time Press any Key to exit");
				Console.ReadKey();
			}
			else
			{
				WriteSuccess(@"Bye Bye, see you next time");
			}
		}
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern uint GetConsoleProcessList(uint[] processList, uint processCount);

	private static bool IsOwnConsoleWindow()
	{
		if (Console.IsOutputRedirected)
		{
			return false;
		}

		uint[] processList = new uint[2];

		return GetConsoleProcessList(processList, (uint)processList.Length) == 1;
	}

	private static void TrySetWindowSize(int width, int height)
	{
		try
		{
			Console.SetWindowSize(width, height);
		}
		catch (Exception e) when (e is IOException or ArgumentOutOfRangeException or PlatformNotSupportedException)
		{
			// Window size is cosmetic; ignore hosts that do not allow resizing.
		}
	}

	private static void WriteVersion()
	{
		Assembly? assembly = Assembly.GetEntryAssembly();

		if (assembly is null)
		{
			WriteLine("Unable to get entry assembly version.");

			return;
		}

		string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
			?? assembly.GetName().Version?.ToString();
		WriteLine($"Runtime Version: {version}");
	}

	private static void ScribanRun(ConfigModel config, Context context)
	{
		ScribanGenerator scribanGenerator = new(config.TemplateName,
			config.TemplateDirectoryName,
			config.OutDirectory,
			context,
			config.TemplateEngine);

		scribanGenerator.GenerateTemplate();
	}
}
