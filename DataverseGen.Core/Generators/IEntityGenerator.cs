namespace DataverseGen.Core.Generators;

public interface IEntityGenerator
{
	/// <summary>Writes the entity files; returns the step that has to run after everything else, if any.</summary>
	Action Generate(GenerationSession session, CancellationToken cancellationToken);
}
