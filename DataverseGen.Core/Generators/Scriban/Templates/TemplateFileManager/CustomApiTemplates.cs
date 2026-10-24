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

	private const string ResponseTemplateFileName = "customapi.response.sbncs";

	public static Template Load(string templateName, string templateDirName, bool isTypeScript)
	{
		return Load(templateName, templateDirName, isTypeScript, false);
	}

	public static Template LoadResponse(string templateName, string templateDirName, bool isTypeScript)
	{
		return Load(templateName, templateDirName, isTypeScript, true);
	}

	public static Template LoadBuiltIn(bool isTypeScript)
	{
		return LoadBuiltIn(isTypeScript, false);
	}

	public static Template LoadBuiltInResponse(bool isTypeScript)
	{
		return LoadBuiltIn(isTypeScript, true);
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

	private static Template Load(string templateName, string templateDirName, bool isTypeScript, bool isResponse)
	{
		if (!string.Equals(templateName, RestrictedTemplateMain, StringComparison.InvariantCultureIgnoreCase))
		{
			string fileName = isResponse ? ResponseTemplateFileName : TemplateFileName;
			string path = $"{Directory.GetCurrentDirectory()}\\{templateDirName}\\{templateName}\\{fileName}";

			if (File.Exists(path))
			{
				Console.WriteLine($@"Loading custom API template from path: {path}");

				return Parse(File.ReadAllText(path), path);
			}

			Console.WriteLine($@"Template '{templateName}' has no {fileName}, using the built-in custom API template");
		}

		return LoadBuiltIn(isTypeScript, isResponse);
	}

	private static Template LoadBuiltIn(bool isTypeScript, bool isResponse)
	{
		byte[] content = (isTypeScript, isResponse) switch
		{
			(true, true) => ScribanTemplates.Main_ts_customapi_response,
			(true, false) => ScribanTemplates.Main_ts_customapi,
			(false, true) => ScribanTemplates.Main_customapi_response,
			_ => ScribanTemplates.Main_customapi
		};

		return Parse(Encoding.UTF8.GetString(content), null);
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
