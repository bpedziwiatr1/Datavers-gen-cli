using System.Diagnostics;

namespace DataverseGen.Core.Reporting;

/// <summary>
///  Sink that aggregates durations, request counts and written files into a <see cref="PerformanceReport" />.
///  Thread-safe.
/// </summary>
public sealed class PerformanceCollector : IGenerationReporter
{
	private const int SlowestCount = 3;

	private readonly Dictionary<(ItemKind Kind, string Name), ItemState> _items = new();
	private readonly object _lock = new();
	private readonly List<string> _messages = new();
	private readonly List<KeyValuePair<Stage, TimeSpan>> _stages = new();
	private readonly Stopwatch _wallClock = Stopwatch.StartNew();
	private int _requestCount;
	private TimeSpan _requestTime;

	public void Log(LogLevel level, string message, Exception exception = null)
	{
		if (level is LogLevel.Warning or LogLevel.Error)
		{
			lock (_lock)
			{
				_messages.Add(message);
			}
		}
	}

	public void StageStarted(Stage stage, int total)
	{
	}

	public void ItemCompleted(ItemResult result)
	{
		lock (_lock)
		{
			(ItemKind Kind, string Name) key = (result.Kind, (result.Name ?? string.Empty).ToLowerInvariant());

			if (!_items.TryGetValue(key, out ItemState state))
			{
				state = new ItemState(result.Kind, result.Name, _items.Count);
				_items[key] = state;
			}

			state.Add(result);
		}
	}

	public void StageCompleted(Stage stage, TimeSpan elapsed)
	{
		lock (_lock)
		{
			_stages.Add(new KeyValuePair<Stage, TimeSpan>(stage, elapsed));
		}
	}

	public void RequestCompleted(Stage stage, TimeSpan elapsed)
	{
		lock (_lock)
		{
			_requestCount++;
			_requestTime += elapsed;
		}
	}

	/// <summary>Builds a snapshot; <paramref name="degreeOfParallelism" /> is the number of items processed concurrently (1 = sequential), <paramref name="wallClock" /> defaults to the time since the collector was created.</summary>
	public PerformanceReport Build(int degreeOfParallelism = 1, TimeSpan? wallClock = null)
	{
		int parallelism = Math.Max(1, degreeOfParallelism);

		lock (_lock)
		{
			ItemSummary[] items = _items.Values
			   .OrderBy(state => state.Order)
			   .Select(state => state.ToSummary())
			   .ToArray();

			TimeSpan sumOfItemTimes = items.Aggregate(TimeSpan.Zero, (sum, item) => sum + item.TotalElapsed);
			TimeSpan entitiesStage = StageTime(Stage.Entities);
			TimeSpan generateStage = StageTime(Stage.Generate);
			TimeSpan itemStages = entitiesStage + StageTime(Stage.CustomApis) + generateStage;
			int files = items.Sum(item => item.Files);

			return new PerformanceReport
			{
				WallClock = wallClock ?? _wallClock.Elapsed,
				Stages = _stages.ToArray(),
				Items = items,
				Messages = _messages.ToArray(),
				RequestCount = _requestCount,
				RequestTime = _requestTime,
				DegreeOfParallelism = parallelism,
				FilesWritten = files,
				BytesWritten = items.Sum(item => item.Bytes),
				EntitiesPerSecond = PerSecond(items.Count(item => item.Kind == ItemKind.Entity), entitiesStage),
				FilesPerSecond = PerSecond(files, generateStage),
				Slowest = items
				   .Where(item => item.Kind != ItemKind.Output)
				   .OrderByDescending(item => item.TotalElapsed)
				   .Take(SlowestCount)
				   .ToArray(),
				SumOfItemTimes = sumOfItemTimes,
				SpeedUp = parallelism <= 1 || itemStages <= TimeSpan.Zero
					? 1.0
					: sumOfItemTimes / itemStages,
				PeakWorkingSetBytes = GetPeakWorkingSet()
			};
		}
	}

	private TimeSpan StageTime(Stage stage)
	{
		return _stages
		   .Where(entry => entry.Key == stage)
		   .Aggregate(TimeSpan.Zero, (sum, entry) => sum + entry.Value);
	}

	private static double PerSecond(int count, TimeSpan duration)
	{
		return duration > TimeSpan.Zero ? count / duration.TotalSeconds : 0;
	}

	private static long GetPeakWorkingSet()
	{
		using Process process = Process.GetCurrentProcess();

		return process.PeakWorkingSet64;
	}

	private sealed class ItemState
	{
		private readonly ItemKind _kind;
		private readonly string _name;
		private long _bytes;
		private string _generateDetail;
		private TimeSpan _generateElapsed;
		private int _files;
		private string _retrieveDetail;
		private TimeSpan _retrieveElapsed;
		private ItemStatus _status;

		public ItemState(ItemKind kind, string name, int order)
		{
			_kind = kind;
			_name = name;
			Order = order;
		}

		public int Order { get; }

		public void Add(ItemResult result)
		{
			if (result.Status > _status)
			{
				_status = result.Status;
			}

			if (result.Phase == ItemPhase.Retrieve)
			{
				_retrieveElapsed += result.Elapsed;
				_retrieveDetail = string.IsNullOrEmpty(result.Detail) ? _retrieveDetail : result.Detail;
			}
			else
			{
				_generateElapsed += result.Elapsed;
				_generateDetail = string.IsNullOrEmpty(result.Detail) ? _generateDetail : result.Detail;
			}

			_files += result.Files;
			_bytes += result.Bytes;
		}

		public ItemSummary ToSummary()
		{
			return new ItemSummary
			{
				Kind = _kind,
				Name = _name,
				Status = _status,
				Detail = _retrieveDetail ?? _generateDetail,
				RetrieveElapsed = _retrieveElapsed,
				GenerateElapsed = _generateElapsed,
				Files = _files,
				Bytes = _bytes
			};
		}
	}
}
