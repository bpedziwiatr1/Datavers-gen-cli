namespace DataverseGen.Core.Generators;

public interface ICustomApiGenerator
{
	void Generate(GenerationSession session, CancellationToken cancellationToken);
}
