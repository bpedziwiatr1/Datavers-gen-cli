using System.Diagnostics;
using DataverseGen.Core.Config;
using DataverseGen.Core.Metadata;
using DataverseGen.Core.Reporting;

namespace DataverseGen.Core.Generators;

public abstract class BaseGenerator
{
	protected readonly Context Context;
	protected readonly string OutPath;
	protected readonly string TemplateDirName;
	protected readonly TemplateEngineModel TemplateEngineModel;
	protected readonly string TemplateName;
	private readonly string _customApiOutPath;
	private readonly int _degreeOfParallelism;
	private readonly bool _clearOutputDirectory;
	private readonly IGenerationReporter _reporter;
	private readonly string _baseDirectory;

	protected BaseGenerator(
		string templateName,
		string templateDirName,
		string outPath,
		Context context,
		TemplateEngineModel templateEngineModel,
		string customApiOutPath = null,
		IGenerationReporter reporter = null,
		int degreeOfParallelism = 1,
		bool clearOutputDirectory = false,
		string baseDirectory = null)
	{
		TemplateEngineModel = templateEngineModel;
		TemplateDirName = templateDirName;
		TemplateName = templateName;
		OutPath = outPath;
		Context = context;
		_customApiOutPath = customApiOutPath;
		_reporter = reporter ?? NullReporter.Instance;
		_degreeOfParallelism = Math.Max(1, degreeOfParallelism);
		_clearOutputDirectory = clearOutputDirectory;
		_baseDirectory = baseDirectory;
	}

	protected abstract IEntityGenerator CreateEntityGenerator();

	protected abstract ICustomApiGenerator CreateCustomApiGenerator();

	public void GenerateTemplate()
	{
		Generate(CancellationToken.None);
	}

	/// <summary>
	///  Same as <see cref="GenerateTemplate" />, off the calling thread and cancellable. Cancelling throws
	///  <see cref="OperationCanceledException" /> and the stage is not reported as completed.
	/// </summary>
	public Task GenerateTemplateAsync(CancellationToken cancellationToken = default)
	{
		return Task.Run(() => Generate(cancellationToken), cancellationToken);
	}

	private void Generate(CancellationToken cancellationToken)
	{
		Stopwatch stopper = Stopwatch.StartNew();
		_reporter.StageStarted(Stage.Generate, GetGenerateItemCount());
		_reporter.Log(LogLevel.Debug, $@"Scriban template generator, template '{TemplateName}'");
		GenerationSession session = CreateSession();

		if (!Directory.Exists(session.FullOutputPath))
		{
			Directory.CreateDirectory(session.FullOutputPath);
		}

		using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		Task customApis = null;

		// Custom APIs are independent of the entities, so with parallelism they are written at the same time.
		if (_degreeOfParallelism > 1)
		{
			customApis = Task.Run(() => GenerateCustomApis(session, stop.Token), stop.Token);
		}

		Action writeLast = RunEntitiesAndWaitForCustomApis(session, customApis, stop);

		if (customApis == null)
		{
			GenerateCustomApis(session, stop.Token);
		}

		cancellationToken.ThrowIfCancellationRequested();
		writeLast?.Invoke();

		if (_clearOutputDirectory)
		{
			string extension = TemplateEngineModel.Type.Equals("ts", StringComparison.OrdinalIgnoreCase) ? "*.ts" : "*.cs";
			session.Files.ClearStaleFiles(extension, session.FullOutputPath, session.CustomApiPath);
		}

		stopper.Stop();
		_reporter.StageCompleted(Stage.Generate, stopper.Elapsed);
	}

	private Action RunEntitiesAndWaitForCustomApis(
		GenerationSession session,
		Task customApis,
		CancellationTokenSource stop)
	{
		try
		{
			Action writeLast = CreateEntityGenerator().Generate(session, stop.Token);
			customApis?.GetAwaiter().GetResult();

			return writeLast;
		}
		catch
		{
			stop.Cancel();

			try
			{
				customApis?.GetAwaiter().GetResult();
			}
			catch (Exception)
			{
				// The first failure is the one that gets reported.
			}

			throw;
		}
	}

	private void GenerateCustomApis(GenerationSession session, CancellationToken cancellationToken)
	{
		if (Context.CustomApis == null || Context.CustomApis.Length == 0)
		{
			return;
		}

		CreateCustomApiGenerator().Generate(session, cancellationToken);
	}

	private GenerationSession CreateSession()
	{
		string fullOutputPath = GeneratorPaths.ResolveOutput(OutPath, _baseDirectory);

		return new GenerationSession
		{
			Context = Context,
			TemplateEngineModel = TemplateEngineModel,
			TemplateName = TemplateName,
			TemplateDirName = TemplateDirName,
			FullOutputPath = fullOutputPath,
			CustomApiPath = GeneratorPaths.ResolveCustomApi(fullOutputPath, _customApiOutPath, _baseDirectory),
			BaseDirectory = _baseDirectory,
			Reporter = _reporter,
			DegreeOfParallelism = _degreeOfParallelism,
			Files = new GeneratedFileSink(_reporter)
		};
	}

	private int GetGenerateItemCount()
	{
		int apiCount = Context.CustomApis?.Length ?? 0;

		return (TemplateEngineModel.IsSingleOutput ? 1 : Context.Entities?.Length ?? 0) + apiCount;
	}
}
