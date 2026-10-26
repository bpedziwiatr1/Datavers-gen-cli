namespace DataverseGen.Core.Pipeline;

/// <summary>Run-time overrides that are not part of the config file.</summary>
public sealed class GenerationOptions
{
	/// <summary>Requested degree of parallelism; <c>null</c> = the server's recommendation.</summary>
	public int? Parallelism { get; init; }

	/// <summary>Keep previously generated files that were not written in this run (overrides ClearOutputDirectory).</summary>
	public bool NoClear { get; init; }
}
