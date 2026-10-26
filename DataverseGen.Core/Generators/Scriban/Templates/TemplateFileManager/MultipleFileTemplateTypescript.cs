using System.Text;
using DataverseGen.Core.Reporting;
using Scriban;

namespace DataverseGen.Core.Generators.Scriban.Templates.TemplateFileManager;

public class MultipleFileTemplateTypescript : MultipleFileTemplateBase
{
	private string _relationshipsTemplateFilePath;

	public MultipleFileTemplateTypescript(string templateName, string templateDir, IGenerationReporter reporter = null, string baseDirectory = null) :
		base(templateName, templateDir, reporter, baseDirectory) { }

	internal Template RelationshipTemplate { get; private set; }

	protected override void CheckFiles()
	{
		base.CheckFiles();
		_relationshipsTemplateFilePath = Path.Combine(DirectoryPath, "entity-relationship.sbncs");

		if (!File.Exists(_relationshipsTemplateFilePath))
		{
			throw new ArgumentException(
				$"Template missing file for entity-relationship '{_relationshipsTemplateFilePath}'");
		}
	}

	protected override void SetMainTemplate()
	{
		EntityTemplate =
			TemplateParser.Parse(Encoding.UTF8.GetString(ScribanTemplates.Main_ts_entity), null);

		EnumsTemplate =
			TemplateParser.Parse(Encoding.UTF8.GetString(ScribanTemplates.Main_ts_enums), null);

		RelationshipTemplate =
			TemplateParser.Parse(Encoding.UTF8.GetString(ScribanTemplates.Main_ts_entity_relationship), null);
	}

	protected override void SetTemplateByFiles()
	{
		base.SetTemplateByFiles();
		RelationshipTemplate = TemplateParser.ParseFile(_relationshipsTemplateFilePath);
	}
}