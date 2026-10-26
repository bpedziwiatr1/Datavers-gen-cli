using System.Text;
using DataverseGen.Core.Reporting;
using Scriban;

namespace DataverseGen.Core.Generators.Scriban.Templates.TemplateFileManager;

public class MultipleFileTemplateCsharp : MultipleFileTemplateBase
{
	private string _fieldsTemplateFilePath;
	private string _xrmServiceContextTemplateFilePath;

	public MultipleFileTemplateCsharp(string templateName, string templateDirName, IGenerationReporter reporter = null, string baseDirectory = null) :
		base(templateName, templateDirName, reporter, baseDirectory) { }

	internal Template XrmContextTemplate { get; private set; }

	internal Template FieldsTemplate { get; private set; }

	protected override void CheckFiles()
	{
		base.CheckFiles();
		_xrmServiceContextTemplateFilePath = Path.Combine(DirectoryPath, "xrmservicecontext.sbncs");
		_fieldsTemplateFilePath = Path.Combine(DirectoryPath, "fields.sbncs");

		if (!File.Exists(_xrmServiceContextTemplateFilePath))
		{
			throw new ArgumentException(
				$"Template missing file for xrmServiceContext '{_xrmServiceContextTemplateFilePath}'");
		}

		if (!File.Exists(_fieldsTemplateFilePath))
		{
			throw new ArgumentException($"Template missing file for fields '{_fieldsTemplateFilePath}'");
		}
	}

	protected override void SetMainTemplate()
	{
		base.SetMainTemplate();
		XrmContextTemplate =
			TemplateParser.Parse(Encoding.UTF8.GetString(ScribanTemplates.Main_xrmservicecontext), null);

		FieldsTemplate =
			TemplateParser.Parse(Encoding.UTF8.GetString(ScribanTemplates.Main_fields), null);
	}

	protected override void SetTemplateByFiles()
	{
		base.SetTemplateByFiles();
		XrmContextTemplate = TemplateParser.ParseFile(_xrmServiceContextTemplateFilePath);
		FieldsTemplate = TemplateParser.ParseFile(_fieldsTemplateFilePath);
	}
}