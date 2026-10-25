using DataverseGen.Core.Metadata;

namespace DataverseGen.Core.Generators.Views;

/// <summary>Model of the per-entity templates. Member names are the template contract (snake_case in Scriban).</summary>
public class EntityTemplateView
{
	public string Namespace { get; set; }

	public GeneratorInfo Info { get; set; }

	public MappingEntity Entity { get; set; }
}
