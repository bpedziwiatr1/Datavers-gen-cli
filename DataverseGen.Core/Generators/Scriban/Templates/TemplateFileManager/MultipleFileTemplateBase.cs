using System.Text;
using DataverseGen.Core.Reporting;
using Scriban;

namespace DataverseGen.Core.Generators.Scriban.Templates.TemplateFileManager;

public abstract class MultipleFileTemplateBase
{
	private const string RestrictedTemplateMain = "Main";
	private readonly string _templateDirName;
	private readonly string _templateName;
	private readonly string _baseDirectory;
	private string _entityTemplateFilePath;
	private string _enumsTemplateFilePath;
	protected string DirectoryPath;
	protected IGenerationReporter Reporter;

	protected MultipleFileTemplateBase(string templateName, string templateDirName, IGenerationReporter reporter = null, string baseDirectory = null)
	{
		_templateName = templateName;
		_templateDirName = templateDirName;
		_baseDirectory = baseDirectory;
		Reporter = reporter ?? NullReporter.Instance;
	}

	internal Template EntityTemplate { get; set; }

	internal Template EnumsTemplate { get; set; }

	public void Init()
	{
		if (string.Equals(_templateName, RestrictedTemplateMain, StringComparison.InvariantCultureIgnoreCase))
		{
			SetMainTemplate();

			return;
		}

		SetTemplateByFiles();
	}

	protected virtual void CheckFiles()
	{
		DirectoryPath = GeneratorPaths.ResolveTemplateDirectory(_templateDirName, _templateName, _baseDirectory);
		Reporter.Log(LogLevel.Debug, $@"Loading templates from path: {DirectoryPath}");

		if (!Directory.Exists(DirectoryPath))
		{
			throw new ArgumentException($"Template '{_templateName}' not found in directory {DirectoryPath}");
		}

		_entityTemplateFilePath = Path.Combine(DirectoryPath, "entity.sbncs");
		_enumsTemplateFilePath = Path.Combine(DirectoryPath, "enums.sbncs");

		if (!File.Exists(_entityTemplateFilePath))
		{
			throw new ArgumentException($"Template missing file for entity '{_entityTemplateFilePath}'");
		}

		if (!File.Exists(_enumsTemplateFilePath))
		{
			throw new ArgumentException($"Template missing file for enums '{_enumsTemplateFilePath}'");
		}
	}

	protected virtual void SetMainTemplate()
	{
		EntityTemplate =
			TemplateParser.Parse(Encoding.UTF8.GetString(ScribanTemplates.Main_entity), null);

		EnumsTemplate =
			TemplateParser.Parse(Encoding.UTF8.GetString(ScribanTemplates.Main_enums), null);
	}

	protected virtual void SetTemplateByFiles()
	{
		CheckFiles();
		EntityTemplate =
			TemplateParser.ParseFile(_entityTemplateFilePath);

		EnumsTemplate = TemplateParser.ParseFile(_enumsTemplateFilePath);
	}
}