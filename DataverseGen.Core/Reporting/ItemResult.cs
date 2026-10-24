namespace DataverseGen.Core.Reporting;

public enum ItemKind
{
	Entity,
	CustomApi
}

public enum ItemStatus
{
	Ok,
	Warning,
	NotFound,
	Failed
}

/// <summary>Outcome of processing one entity or custom API.</summary>
public sealed class ItemResult
{
	public ItemKind Kind { get; init; }

	public string Name { get; init; }

	public ItemStatus Status { get; init; }

	public string Detail { get; init; }

	public TimeSpan Elapsed { get; init; }

	public int Files { get; init; }

	public long Bytes { get; init; }
}
