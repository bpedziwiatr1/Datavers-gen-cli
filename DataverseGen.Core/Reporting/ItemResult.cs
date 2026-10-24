namespace DataverseGen.Core.Reporting;

public enum ItemKind
{
	Entity,
	CustomApi,

	/// <summary>Output that belongs to no single entity or custom API (e.g. a single-file result).</summary>
	Output
}

public enum ItemStatus
{
	Ok,
	Warning,
	NotFound,
	Failed
}

/// <summary>Which part of the pipeline an <see cref="ItemResult" /> describes.</summary>
public enum ItemPhase
{
	/// <summary>Metadata was retrieved from Dataverse.</summary>
	Retrieve,

	/// <summary>Code was generated and written to disk.</summary>
	Generate
}

/// <summary>Outcome of processing one entity or custom API in one <see cref="ItemPhase" />.</summary>
public sealed class ItemResult
{
	public ItemKind Kind { get; init; }

	public ItemPhase Phase { get; init; }

	public string Name { get; init; }

	public ItemStatus Status { get; init; }

	public string Detail { get; init; }

	public TimeSpan Elapsed { get; init; }

	public int Files { get; init; }

	public long Bytes { get; init; }
}
