using DataverseGen.Core.Config;
using Newtonsoft.Json;

namespace DataverseGen.Core.Scaffolding;

public sealed class ConfigScaffoldOptions
{
	public string Namespace { get; set; } = "My.Dataverse.Model";

	/// <summary>Generator type: <c>C#</c> or <c>ts</c>.</summary>
	public string Type { get; set; } = "C#";

	public bool IsSingleOutput { get; set; }

	public string OutDirectory { get; set; } = "out";
}

/// <summary>Writes a starter <c>dataversegen.config.json</c>.</summary>
public static class ConfigScaffolder
{
	public const string ConfigFileName = "dataversegen.config.json";

	public static ConfigModel BuildConfig(ConfigScaffoldOptions options)
	{
		return new ConfigModel
		{
			ConnectionString = string.Empty,
			Entities = ["account", "contact"],
			CustomApis = [],
			Namespace = options.Namespace,
			OutDirectory = options.OutDirectory,
			TemplateName = "Main",
			TemplateDirectoryName = "Templates",
			EnableConnectionStringValidation = true,
			ThrowOnEntityNotFound = true,
			TemplateEngine = new TemplateEngineModel
			{
				IsSingleOutput = options.IsSingleOutput,
				Name = "scriban",
				Type = options.Type
			}
		};
	}

	/// <summary>Writes the config into <paramref name="directory"/> and returns its full path.</summary>
	public static string Write(string directory, ConfigScaffoldOptions options, bool force)
	{
		string path = Path.Combine(directory, ConfigFileName);

		if (File.Exists(path) && !force)
		{
			throw new IOException($"'{path}' already exists. Use --force to overwrite.");
		}

		string json = JsonConvert.SerializeObject(BuildConfig(options), Formatting.Indented);
		File.WriteAllText(path, json);

		return path;
	}
}
