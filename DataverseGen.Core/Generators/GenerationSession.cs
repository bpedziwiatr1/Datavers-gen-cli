using DataverseGen.Core.Config;
using DataverseGen.Core.Metadata;
using DataverseGen.Core.Reporting;

namespace DataverseGen.Core.Generators;

/// <summary>State shared by the entity and custom API generators during one generation run.</summary>
public class GenerationSession
{
	public Context Context { get; init; }

	public TemplateEngineModel TemplateEngineModel { get; init; }

	public string TemplateName { get; init; }

	public string TemplateDirName { get; init; }

	public string FullOutputPath { get; init; }

	public string CustomApiPath { get; init; }

	public IGenerationReporter Reporter { get; init; }

	public int DegreeOfParallelism { get; init; }

	public GeneratedFileSink Files { get; init; }

	public ParallelOptions CreateParallelOptions(CancellationToken cancellationToken)
	{
		return new ParallelOptions
		{
			MaxDegreeOfParallelism = DegreeOfParallelism,
			CancellationToken = cancellationToken
		};
	}
}
