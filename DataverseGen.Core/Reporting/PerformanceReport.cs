using System.Globalization;
using System.Text;

namespace DataverseGen.Core.Reporting;

/// <summary>Everything known about one entity or custom API after all phases (retrieve + generate) were merged.</summary>
public sealed class ItemSummary
{
	public ItemKind Kind { get; init; }

	public string Name { get; init; }

	public ItemStatus Status { get; init; }

	public string Detail { get; init; }

	public TimeSpan RetrieveElapsed { get; init; }

	public TimeSpan GenerateElapsed { get; init; }

	public int Files { get; init; }

	public long Bytes { get; init; }

	public TimeSpan TotalElapsed => RetrieveElapsed + GenerateElapsed;
}

/// <summary>Immutable snapshot built by <see cref="PerformanceCollector" />.</summary>
public sealed class PerformanceReport
{
	public TimeSpan WallClock { get; init; }

	/// <summary>Stage durations in the order the stages completed.</summary>
	public IReadOnlyList<KeyValuePair<Stage, TimeSpan>> Stages { get; init; } = [];

	public IReadOnlyList<ItemSummary> Items { get; init; } = [];

	/// <summary>Warning and error log messages (already secret-masked).</summary>
	public IReadOnlyList<string> Messages { get; init; } = [];

	public int RequestCount { get; init; }

	public TimeSpan RequestTime { get; init; }

	public int DegreeOfParallelism { get; init; } = 1;

	public int FilesWritten { get; init; }

	public long BytesWritten { get; init; }

	public double EntitiesPerSecond { get; init; }

	public double FilesPerSecond { get; init; }

	public IReadOnlyList<ItemSummary> Slowest { get; init; } = [];

	/// <summary>Sum of all per-item durations (the time a sequential run would need).</summary>
	public TimeSpan SumOfItemTimes { get; init; }

	/// <summary>Sum of item times divided by the wall-clock time of the item stages; exactly 1.0 when sequential.</summary>
	public double SpeedUp { get; init; } = 1.0;

	public long PeakWorkingSetBytes { get; init; }

	/// <summary>Plain-text rendering used for the <c>--log</c> file.</summary>
	public string ToText()
	{
		CultureInfo c = CultureInfo.InvariantCulture;
		StringBuilder text = new();
		text.AppendLine("Performance report");
		text.AppendLine(c, $"  Wall clock:         {WallClock.TotalMilliseconds:F0} ms");

		foreach (KeyValuePair<Stage, TimeSpan> stage in Stages)
		{
			text.AppendLine(c, $"  Stage {stage.Key,-11}   {stage.Value.TotalMilliseconds:F0} ms");
		}

		text.AppendLine(c, $"  Dataverse requests: {RequestCount} ({RequestTime.TotalMilliseconds:F0} ms total)");
		text.AppendLine(c, $"  Parallelism:        {DegreeOfParallelism}");
		text.AppendLine(c, $"  Files written:      {FilesWritten} ({BytesWritten} bytes)");
		text.AppendLine(c, $"  Throughput:         {EntitiesPerSecond:F1} entities/s, {FilesPerSecond:F1} files/s");
		text.AppendLine(c, $"  Sum of item times:  {SumOfItemTimes.TotalMilliseconds:F0} ms (speed-up {SpeedUp:F1}x)");
		text.AppendLine(c, $"  Peak working set:   {PeakWorkingSetBytes / (1024.0 * 1024.0):F0} MB");

		if (Slowest.Count > 0)
		{
			text.AppendLine("  Slowest items:");

			foreach (ItemSummary item in Slowest)
			{
				text.AppendLine(c, $"    {item.Kind} {item.Name}: {item.TotalElapsed.TotalMilliseconds:F0} ms");
			}
		}

		return text.ToString().TrimEnd();
	}
}
