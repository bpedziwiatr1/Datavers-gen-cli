using DataverseGen.Core.Config;
using DataverseGen.Core.Metadata;
using DataverseGen.Core.Reporting;

namespace DataverseGen.Core.Generators.Scriban;

public class ScribanGenerator : BaseGenerator
{
	private readonly bool _legacyEntityFileNames;

	public ScribanGenerator(
		string templateName,
		string templateDirName,
		string outPath,
		Context context,
		TemplateEngineModel templateEngineModel,
		string customApiOutPath = null,
		bool legacyEntityFileNames = false,
		IGenerationReporter reporter = null,
		int degreeOfParallelism = 1,
		bool clearOutputDirectory = false)
		: base(templateName,
			templateDirName,
			outPath,
			context,
			templateEngineModel,
			customApiOutPath,
			reporter,
			degreeOfParallelism,
			clearOutputDirectory)
	{
		_legacyEntityFileNames = legacyEntityFileNames;
	}

	protected override IEntityGenerator CreateEntityGenerator()
	{
		return new EntitiesScribanGenerator(_legacyEntityFileNames);
	}

	protected override ICustomApiGenerator CreateCustomApiGenerator()
	{
		return new CustomApiScribanGenerator();
	}
}
