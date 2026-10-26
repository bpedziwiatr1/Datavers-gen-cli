using System.Text;
using DataverseGen.Core.Generators.Scriban.Templates;

namespace DataverseGen.Core.Scaffolding;

/// <summary>Copies the built-in Scriban template set to disk so it can be customized.</summary>
public static class TemplateScaffolder
{
	public static string[] GetTemplateFileNames(bool isTypeScript)
	{
		return GetTemplates(isTypeScript).Keys.ToArray();
	}

	/// <summary>Writes the template set into <paramref name="targetDirectory"/> and returns the written paths.</summary>
	public static string[] Write(string targetDirectory, bool isTypeScript, bool force)
	{
		Dictionary<string, byte[]> templates = GetTemplates(isTypeScript);

		if (!force)
		{
			string existing = templates.Keys
				.Select(name => Path.Combine(targetDirectory, name))
				.FirstOrDefault(File.Exists);

			if (existing is not null)
			{
				throw new IOException($"'{existing}' already exists. Use --force to overwrite.");
			}
		}

		Directory.CreateDirectory(targetDirectory);

		List<string> written = new();

		foreach (KeyValuePair<string, byte[]> template in templates)
		{
			string path = Path.Combine(targetDirectory, template.Key);
			File.WriteAllBytes(path, template.Value);
			written.Add(path);
		}

		return written.ToArray();
	}

	private static Dictionary<string, byte[]> GetTemplates(bool isTypeScript)
	{
		return isTypeScript
			? new Dictionary<string, byte[]>
			{
				["entity.sbncs"] = ScribanTemplates.Main_ts_entity,
				["enums.sbncs"] = ScribanTemplates.Main_ts_enums,
				["entity-relationship.sbncs"] = ScribanTemplates.Main_ts_entity_relationship,
				["customapi.sbncs"] = ScribanTemplates.Main_ts_customapi,
				["customapi.response.sbncs"] = ScribanTemplates.Main_ts_customapi_response
			}
			: new Dictionary<string, byte[]>
			{
				["entity.sbncs"] = ScribanTemplates.Main_entity,
				["enums.sbncs"] = ScribanTemplates.Main_enums,
				["fields.sbncs"] = ScribanTemplates.Main_fields,
				["xrmservicecontext.sbncs"] = ScribanTemplates.Main_xrmservicecontext,
				["customapi.sbncs"] = ScribanTemplates.Main_customapi,
				["customapi.response.sbncs"] = ScribanTemplates.Main_customapi_response
			};
	}
}
