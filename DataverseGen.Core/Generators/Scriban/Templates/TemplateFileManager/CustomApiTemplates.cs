using System.Text;
using Scriban;
using Scriban.Runtime;

namespace DataverseGen.Core.Generators.Scriban.Templates.TemplateFileManager;

/// <summary>
///  Loads and renders the custom API request wrapper template. Custom templates are optional: when a template
///  directory does not contain <c>customapi.sbncs</c>, the built-in one is used.
/// </summary>
internal static class CustomApiTemplates
{
	private const string RestrictedTemplateMain = "Main";
	private const string TemplateFileName = "customapi.sbncs";

	public static Template Load(string templateName, string templateDirName, bool isTypeScript)
	{
		if (!string.Equals(templateName, RestrictedTemplateMain, StringComparison.InvariantCultureIgnoreCase))
		{
			string path = $"{Directory.GetCurrentDirectory()}\\{templateDirName}\\{templateName}\\{TemplateFileName}";

			if (File.Exists(path))
			{
				Console.WriteLine($@"Loading custom API template from path: {path}");

				return Parse(File.ReadAllText(path), path);
			}

			Console.WriteLine($@"Template '{templateName}' has no {TemplateFileName}, using the built-in custom API template");
		}

		return LoadBuiltIn(isTypeScript);
	}

	public static Template LoadBuiltIn(bool isTypeScript)
	{
		byte[] content = isTypeScript
			? ScribanTemplates.Main_ts_customapi
			: ScribanTemplates.Main_customapi;

		return Parse(Encoding.UTF8.GetString(content), null);
	}

	public static string Render(Template template, object view)
	{
		ScriptObject scriptObject = new();
		scriptObject.Import(view);

		TemplateContext context = new()
		{
			LoopLimit = 0,
			RecursiveLimit = 0
		};

		context.PushGlobal(scriptObject);

		return template.Render(context)
		   .Replace("\r\n", "\n")
		   .Replace("\n", "\r\n");
	}

	private static Template Parse(string text, string sourcePath)
	{
		Template template = Template.Parse(text, sourcePath);

		if (template.HasErrors)
		{
			throw new InvalidOperationException(
				$"Custom API template {sourcePath ?? "(built-in)"} is invalid: {string.Join("; ", template.Messages)}");
		}

		return template;
	}
}
