using System.Text;
using DataverseGen.Core.Reporting;
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

	public static Template Load(
		string templateName,
		string templateDirName,
		bool isTypeScript,
		IGenerationReporter reporter = null,
		string baseDirectory = null)
	{
		return Load(templateName, templateDirName, isTypeScript, false, reporter, baseDirectory);
	}

	public static Template LoadResponse(
		string templateName,
		string templateDirName,
		bool isTypeScript,
		IGenerationReporter reporter = null,
		string baseDirectory = null)
	{
		return Load(templateName, templateDirName, isTypeScript, true, reporter, baseDirectory);
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
		return ScribanRenderer.Render(template, view)
		   .Replace("\r\n", "\n")
		   .Replace("\n", "\r\n");
	}

	private static Template Load(
		string templateName,
		string templateDirName,
		bool isTypeScript,
		bool isResponse,
		IGenerationReporter reporter,
		string baseDirectory)
	{
		reporter ??= NullReporter.Instance;

		if (!string.Equals(templateName, RestrictedTemplateMain, StringComparison.InvariantCultureIgnoreCase))
		{
			string fileName = isResponse ? ResponseTemplateFileName : TemplateFileName;
			string path = Path.Combine(GeneratorPaths.ResolveTemplateDirectory(templateDirName, templateName, baseDirectory), fileName);

			if (File.Exists(path))
			{
				reporter.Log(LogLevel.Debug, $@"Loading custom API template from path: {path}");

				return TemplateParser.ParseFile(path);
			}

			reporter.Log(LogLevel.Debug,
				$@"Template '{templateName}' has no {fileName}, using the built-in custom API template");
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

		return TemplateParser.Parse(Encoding.UTF8.GetString(content), null);
	}

}
