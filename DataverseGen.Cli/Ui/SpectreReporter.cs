using DataverseGen.Core.Reporting;
using Spectre.Console;
using Spectre.Console.Rendering;
using LogLevel = DataverseGen.Core.Reporting.LogLevel;

namespace DataverseGen.Cli.Ui;

/// <summary>
///  Console sink. In an interactive terminal it renders one progress bar per stage; otherwise (redirected output,
///  CI) it degrades to plain lines. Debug lines and stack traces only with <c>--verbose</c>. Thread-safe.
/// </summary>
internal sealed class SpectreReporter : IGenerationReporter
{
	private readonly IAnsiConsole _console;
	private readonly object _lock = new();
	private readonly bool _live;
	private readonly Dictionary<Stage, ProgressTask> _tasks = new();
	private readonly Dictionary<Stage, List<string>> _inFlight = new();
	private readonly Theme _theme;
	private readonly bool _verbose;
	private ProgressContext? _progress;

	public SpectreReporter(IAnsiConsole console, Theme theme, bool verbose, bool live)
	{
		_console = console;
		_theme = theme;
		_verbose = verbose;
		_live = live;
	}

	/// <summary>Live rendering needs an interactive terminal; redirected output gets plain lines.</summary>
	public static bool CanRenderLive(IAnsiConsole console, bool outputRedirected)
	{
		return console.Profile.Capabilities.Interactive && !outputRedirected;
	}

	/// <summary>
	///  Runs <paramref name="action" /> inside the progress display. Credential prompts and connection-string
	///  validation must happen before this call: live displays and prompts cannot overlap.
	/// </summary>
	public void Run(Action action)
	{
		RunAsync(() =>
		{
			action();

			return Task.CompletedTask;
		}).GetAwaiter().GetResult();
	}

	/// <summary>Async variant of <see cref="Run" />; the same rules about prompts apply.</summary>
	public async Task RunAsync(Func<Task> action)
	{
		if (!_live)
		{
			await action();

			return;
		}

		await _console.Progress()
		   .AutoClear(false)
		   .HideCompleted(false)
		   .Columns(new SpinnerColumn(Spinner.Known.Dots) { Style = Theme.Of(Theme.Accent) },
				new TaskDescriptionColumn { Alignment = Justify.Left },
				new ProgressBarColumn { CompletedStyle = Theme.Of(Theme.Accent), FinishedStyle = Theme.Of(Theme.Success) },
				new CountColumn(),
				new ElapsedTimeColumn(),
				new CurrentItemColumn(GetCurrent))
		   .StartAsync(async context =>
			{
				lock (_lock)
				{
					_progress = context;
				}

				try
				{
					await action();
				}
				finally
				{
					lock (_lock)
					{
						_progress = null;
					}
				}
			});
	}

	public void Log(LogLevel level, string message, Exception? exception = null)
	{
		if (level == LogLevel.Debug && !_verbose)
		{
			return;
		}

		(string icon, Color color) = level switch
		{
			LogLevel.Debug => (" ", Theme.Muted),
			LogLevel.Success => (_theme.OkIcon, Theme.Success),
			LogLevel.Warning => (_theme.WarnIcon, Theme.Warning),
			LogLevel.Error => (_theme.FailIcon, Theme.Error),
			_ => (_theme.Arrow, Theme.Accent)
		};

		string text = message;

		if (exception is not null && _verbose)
		{
			text = $"{text}{Environment.NewLine}{exception.ToString()}";
		}

		WriteLine(icon, text, color);
	}

	public void StageStarted(Stage stage, int total)
	{
		ProgressContext? progress;

		lock (_lock)
		{
			progress = _progress;
		}

		if (progress is null)
		{
			WriteLine(_theme.Arrow, $"{GetLabel(stage)}{(total > 0 ? $" ({total})" : string.Empty)}", Theme.Accent);

			return;
		}

		// Spectre calls stay outside _lock: the renderer holds its own locks while it calls GetCurrent (which takes _lock).
		ProgressTask task = progress.AddTask(GetLabel(stage), true, Math.Max(total, 1));
		task.IsIndeterminate = stage == Stage.Connect;

		lock (_lock)
		{
			_tasks[stage] = task;
		}
	}

	public void ItemStarted(ItemKind kind, ItemPhase phase, string name)
	{
		lock (_lock)
		{
			Stage stage = GetStage(kind, phase);

			if (!_inFlight.TryGetValue(stage, out List<string>? names))
			{
				names = new List<string>();
				_inFlight[stage] = names;
			}

			names.Add(name);
		}
	}

	public void ItemCompleted(ItemResult result)
	{
		ProgressTask? task;
		bool live;

		lock (_lock)
		{
			Stage stage = GetStage(result.Kind, result.Phase);

			if (_inFlight.TryGetValue(stage, out List<string>? names))
			{
				names.Remove(result.Name ?? string.Empty);
			}

			_tasks.TryGetValue(stage, out task);
			live = _progress is not null;
		}

		if (task is not null && task.Value >= task.MaxValue)
		{
			task.MaxValue = task.Value + 1;
		}

		task?.Increment(1);

		bool problem = result.Status != ItemStatus.Ok;

		if (!problem && !_verbose && live)
		{
			return;
		}

		string name = result.Name ?? string.Empty;
		string detail = string.IsNullOrEmpty(result.Detail) ? string.Empty : $" {_theme.Dot} {result.Detail}";
		string kind = GetKindLabel(result.Kind);

		string text = result.Status switch
		{
			ItemStatus.Failed => $"{kind} failed: {name}{detail}",
			ItemStatus.NotFound => $"{kind} not found: {name}{detail}",
			ItemStatus.Warning => $"{kind} {name}{detail}",
			_ => result.Phase == ItemPhase.Generate
				? $"{name} {_theme.Arrow} {result.Files} {(result.Files == 1 ? "file" : "files")} ({Format.Duration(result.Elapsed)})"
				: $"{name}{detail} ({Format.Duration(result.Elapsed)})"
		};

		WriteLine(_theme.IconFor(result.Status), text, Theme.ColorFor(result.Status));
	}

	public void StageCompleted(Stage stage, TimeSpan elapsed)
	{
		ProgressTask? task;

		lock (_lock)
		{
			_tasks.TryGetValue(stage, out task);
		}

		if (task is not null)
		{
			task.IsIndeterminate = false;
			task.Value = task.MaxValue;
			task.StopTask();

			return;
		}

		WriteLine(_theme.OkIcon, $"{GetLabel(stage)} done in {Format.Duration(elapsed)}", Theme.Success);
	}

	private void WriteLine(string icon, string text, Color color)
	{
		// Not under _lock: the console serializes writes itself, and holding _lock here deadlocks with the live renderer.
		_console.Write(new Text($"{icon} {text}{Environment.NewLine}", Theme.Of(color)));
	}

	private static string GetLabel(Stage stage)
	{
		return stage switch
		{
			Stage.Connect => "Connect",
			Stage.Entities => "Entities",
			Stage.CustomApis => "Custom APIs",
			_ => "Generate"
		};
	}

	private static string GetKindLabel(ItemKind kind)
	{
		return kind switch
		{
			ItemKind.CustomApi => "Custom API",
			ItemKind.Output => "File",
			_ => "Entity"
		};
	}

	private static Stage GetStage(ItemKind kind, ItemPhase phase)
	{
		if (phase == ItemPhase.Generate)
		{
			return Stage.Generate;
		}

		return kind == ItemKind.CustomApi ? Stage.CustomApis : Stage.Entities;
	}

	/// <summary>Names of the items currently being processed in the task's stage (several when run in parallel).</summary>
	private string GetCurrent(ProgressTask task)
	{
		lock (_lock)
		{
			foreach (KeyValuePair<Stage, ProgressTask> pair in _tasks)
			{
				if (pair.Value.Id != task.Id || task.IsFinished)
				{
					continue;
				}

				if (!_inFlight.TryGetValue(pair.Key, out List<string>? names) || names.Count == 0)
				{
					return string.Empty;
				}

				string first = names[0];

				return names.Count == 1 ? first : $"{first} +{names.Count - 1}";
			}
		}

		return string.Empty;
	}

	/// <summary>Name of the item being processed, shown after the elapsed time.</summary>
	private sealed class CurrentItemColumn : ProgressColumn
	{
		private readonly Func<ProgressTask, string> _getCurrent;

		public CurrentItemColumn(Func<ProgressTask, string> getCurrent)
		{
			_getCurrent = getCurrent;
		}

		protected override bool NoWrap => true;

		public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
		{
			string current = _getCurrent(task);

			return new Text(current.Length == 0 ? string.Empty : Format.Truncate(current, 40),
				Theme.Of(Theme.Muted));
		}

		public override int? GetColumnWidth(RenderOptions options)
		{
			return 42;
		}
	}

	/// <summary>"done/total" counter next to the bar.</summary>
	private sealed class CountColumn : ProgressColumn
	{
		protected override bool NoWrap => true;

		public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
		{
			return new Text(task.IsIndeterminate ? string.Empty : $"{task.Value:0}/{task.MaxValue:0}",
				Theme.Of(Theme.Muted));
		}

		public override int? GetColumnWidth(RenderOptions options)
		{
			return 9;
		}
	}
}
