using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using DataverseGen.Core.Config;
using DataverseGen.Core.Generators;
using DataverseGen.Core.Generators.Scriban.Templates;
using DataverseGen.Core.Generators.Scriban.Templates.TemplateFileManager;
using DataverseGen.Core.Metadata;
using DataverseGen.Core.Reporting;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;

namespace DataverseGen.Core.Generators.Scriban;

public partial class ScribanGenerator : BaseGenerator
{
	private readonly string _customApiOutPath;
	private readonly int _degreeOfParallelism;
	private readonly bool _legacyEntityFileNames;
	private readonly IGenerationReporter _reporter;
	private string _fullOutputPath;

	public ScribanGenerator(
		string templateName,
		string templateDirName,
		string outPath,
		Context context,
		TemplateEngineModel templateEngineModel,
		string customApiOutPath = null,
		bool legacyEntityFileNames = false,
		IGenerationReporter reporter = null,
		int degreeOfParallelism = 1)
		: base(templateName,
			templateDirName,
			outPath,
			context,
			templateEngineModel)
	{
		_customApiOutPath = customApiOutPath;
		_legacyEntityFileNames = legacyEntityFileNames;
		_reporter = reporter ?? NullReporter.Instance;
		_degreeOfParallelism = Math.Max(1, degreeOfParallelism);
	}

	public override void GenerateTemplate()
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
		_fullOutputPath = $"{Directory.GetCurrentDirectory()}\\{OutPath}\\";

		if (!Directory.Exists(_fullOutputPath))
		{
			Directory.CreateDirectory(_fullOutputPath);
		}

		using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		Task customApis = null;

		// Custom APIs are independent of the entities, so with parallelism they are written at the same time.
		if (_degreeOfParallelism > 1)
		{
			customApis = Task.Run(() => GenerateCustomApis(stop.Token), stop.Token);
		}

		Action writeLast;

		try
		{
			writeLast = RunEntityGenerator(stop.Token);
			customApis?.GetAwaiter().GetResult();
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

		if (customApis == null)
		{
			GenerateCustomApis(stop.Token);
		}

		cancellationToken.ThrowIfCancellationRequested();
		writeLast?.Invoke();

		stopper.Stop();
		_reporter.StageCompleted(Stage.Generate, stopper.Elapsed);
	}

	/// <summary>Writes the entity files; returns the step that has to run after everything else, if any.</summary>
	private Action RunEntityGenerator(CancellationToken cancellationToken)
	{
		switch (TemplateEngineModel.Type.ToLowerInvariant())
		{
			case "c#":
				return RunCSharpGenerator(cancellationToken);

			case "ts":
				return RunTypeScriptGenerator(cancellationToken);

			default:
				throw new ArgumentOutOfRangeException("_templateEngineModel.Type",
					$@"type {TemplateEngineModel.Type} not supported use C# or TS");
		}
	}

	private ParallelOptions CreateParallelOptions(CancellationToken cancellationToken)
	{
		return new ParallelOptions
		{
			MaxDegreeOfParallelism = _degreeOfParallelism,
			CancellationToken = cancellationToken
		};
	}

	private static string RemoveEmptyLines(string lines)
	{
		return RemoveEmptyLinesRegexCompiled()
		   .Replace(lines, string.Empty)
		   .TrimEnd();
	}

	private Action MultiOutputGeneratorCSharp(CancellationToken cancellationToken)
	{
		MultipleFileTemplateCsharp templates = new(TemplateName, TemplateDirName, _reporter);
		templates.Init();

		Parallel.ForEach(Context.Entities,
			CreateParallelOptions(cancellationToken),
			entity =>
			{
				_reporter.ItemStarted(ItemKind.Entity, ItemPhase.Generate, entity.LogicalName);
				Stopwatch entityTimer = Stopwatch.StartNew();
				long bytes = WriteEntityTemplateToFile(templates.EntityTemplate, entity, _legacyEntityFileNames ? "cs" : "entity.cs");
				bytes += WriteEntityTemplateToFile(templates.FieldsTemplate, entity, "Fields.cs");
				bytes += WriteEntityTemplateToFile(templates.EnumsTemplate, entity, "Enums.cs");
				ReportEntityGenerated(entity, 3, bytes, entityTimer.Elapsed);
			});

		return () => WriteXrmServiceContext(templates);
	}

	private void WriteXrmServiceContext(MultipleFileTemplateCsharp templates)
	{
		_reporter.ItemStarted(ItemKind.Output, ItemPhase.Generate, "XrmServiceContext.cs");
		Stopwatch contextTimer = Stopwatch.StartNew();
		string xrmContextContent =
			RemoveEmptyLines(templates.XrmContextTemplate.Render(Context));

		long contextBytes = WriteFile($"{_fullOutputPath}/XrmServiceContext.cs", xrmContextContent);
		ReportOutputGenerated("XrmServiceContext.cs", contextBytes, contextTimer.Elapsed);
	}

	private Action MultiOutputGeneratorTypeScript(CancellationToken cancellationToken)
	{
		MultipleFileTemplateTypescript templates = new(TemplateName, TemplateDirName, _reporter);
		templates.Init();

		Parallel.ForEach(Context.Entities,
			CreateParallelOptions(cancellationToken),
			entity =>
			{
				_reporter.ItemStarted(ItemKind.Entity, ItemPhase.Generate, entity.LogicalName);
				Stopwatch entityTimer = Stopwatch.StartNew();
				long bytes = WriteEntityTemplateToFile(templates.EntityTemplate, entity, _legacyEntityFileNames ? "attributes.ts" : "entity.ts");
				bytes += WriteEntityTemplateToFile(templates.EnumsTemplate, entity, "enums.ts");
				bytes += WriteEntityTemplateToFile(templates.RelationshipTemplate,
					entity,
					"relationships.ts");
				ReportEntityGenerated(entity, 3, bytes, entityTimer.Elapsed);
			});

		return null;
	}

	private Action RunCSharpGenerator(CancellationToken cancellationToken)
	{
		if (TemplateEngineModel.IsSingleOutput)
		{
			SingleFileCSharp();

			return null;
		}

		return MultiOutputGeneratorCSharp(cancellationToken);
	}

	private Action RunTypeScriptGenerator(CancellationToken cancellationToken)
	{
		if (TemplateEngineModel.IsSingleOutput)
		{
			SingleFileTypescript();

			return null;
		}

		return MultiOutputGeneratorTypeScript(cancellationToken);
	}

	private void SingleFileCSharp()
	{
		_reporter.ItemStarted(ItemKind.Output, ItemPhase.Generate, "XrmServiceContext.cs");
		Stopwatch timer = Stopwatch.StartNew();
		ParserOptions options = new();
		LexerOptions lexerOptions = new()
		{
			Mode = ScriptMode.Default
		};

		Template template = Template.Parse(Encoding.UTF8.GetString(ScribanTemplates.dataversegen_single),
			null,
			options,
			lexerOptions);

		ScriptObject dataScriptObject = new();
		dataScriptObject.Import(Context);

		TemplateContext context = new()
		{
			LoopLimit = 0,
			RecursiveLimit = 0
		};

		context.PushGlobal(dataScriptObject);

		string content = RemoveEmptyLines(template.Render(context));
		long bytes = WriteFile($"{_fullOutputPath}XrmServiceContext.cs", content);
		ReportOutputGenerated("XrmServiceContext.cs", bytes, timer.Elapsed);
	}

	private void SingleFileTypescript()
	{
		_reporter.ItemStarted(ItemKind.Output, ItemPhase.Generate, "dataverse.metadata.ts");
		Stopwatch timer = Stopwatch.StartNew();
		ParserOptions options = new();
		LexerOptions lexerOptions = new()
		{
			Mode = ScriptMode.Default
		};

		Template template = Template.Parse(Encoding.UTF8.GetString(ScribanTemplates.dataversegen_single_ts),
			null,
			options,
			lexerOptions);

		ScriptObject dataScriptObject = new();
		dataScriptObject.Import(Context);

		TemplateContext context = new()
		{
			LoopLimit = 0,
			RecursiveLimit = 0
		};

		context.PushGlobal(dataScriptObject);

		string content = RemoveEmptyLines(template.Render(context));
		long bytes = WriteFile($"{_fullOutputPath}dataverse.metadata.ts", content);
		ReportOutputGenerated("dataverse.metadata.ts", bytes, timer.Elapsed);
	}

	private int GetGenerateItemCount()
	{
		int apiCount = Context.CustomApis?.Length ?? 0;

		return (TemplateEngineModel.IsSingleOutput ? 1 : Context.Entities?.Length ?? 0) + apiCount;
	}

	/// <summary>Writes <paramref name="content" /> and returns the number of bytes on disk.</summary>
	private static long WriteFile(string path, string content)
	{
		File.WriteAllText(path, content, Encoding.UTF8);

		return new FileInfo(path).Length;
	}

	private void ReportEntityGenerated(MappingEntity entity, int files, long bytes, TimeSpan elapsed)
	{
		_reporter.ItemCompleted(new ItemResult
		{
			Kind = ItemKind.Entity,
			Phase = ItemPhase.Generate,
			Name = entity.LogicalName,
			Status = ItemStatus.Ok,
			Elapsed = elapsed,
			Files = files,
			Bytes = bytes
		});
	}

	private void ReportOutputGenerated(string fileName, long bytes, TimeSpan elapsed)
	{
		_reporter.ItemCompleted(new ItemResult
		{
			Kind = ItemKind.Output,
			Phase = ItemPhase.Generate,
			Name = fileName,
			Status = ItemStatus.Ok,
			Elapsed = elapsed,
			Files = 1,
			Bytes = bytes
		});
	}

	private long WriteEntityTemplateToFile(
		Template template,
		MappingEntity entity,
		string outputFileSuffixWithExtension)
	{
		ScriptObject dataScriptObject = new();
		dataScriptObject.Import(new
		{
			Context.Namespace,
			Context.Info,
			Entity = entity
		});

		TemplateContext context = new()
		{
			LoopLimit = 0,
			RecursiveLimit = 0
		};

		context.PushGlobal(dataScriptObject);
		string entityContent =
			RemoveEmptyLines(template.Render(context))
			   .Replace("\n", "\r\n");

		if (outputFileSuffixWithExtension.Contains("ts"))
		{
			entityContent = $"{entityContent}\r\n";
		}

		string entityPath = $"{_fullOutputPath}/{entity.HybridName.ToLower()}.{outputFileSuffixWithExtension}";
		long bytes = WriteFile(entityPath, entityContent);
		_reporter.Log(LogLevel.Debug, $@"Generated entity: {entity.LogicalName} -> {entityPath}");

		return bytes;
	}

	private void GenerateCustomApis(CancellationToken cancellationToken)
	{
		if (Context.CustomApis == null || Context.CustomApis.Length == 0)
		{
			return;
		}

		string customApiPath = string.IsNullOrWhiteSpace(_customApiOutPath)
			? $"{_fullOutputPath}customapi"
			: $"{Directory.GetCurrentDirectory()}\\{_customApiOutPath}\\";

		switch (TemplateEngineModel.Type.ToLowerInvariant())
		{
			case "c#":
				CustomApiCSharpGenerator.WriteCustomApis(customApiPath,
				Context.Namespace,
				Context.CustomApis,
				CustomApiTemplates.Load(TemplateName, TemplateDirName, false, _reporter),
				CustomApiTemplates.LoadResponse(TemplateName, TemplateDirName, false, _reporter),
				_reporter,
				_degreeOfParallelism,
				cancellationToken);
				break;

			case "ts":
				CustomApiTypeScriptGenerator.WriteCustomApis(customApiPath,
				Context.CustomApis,
				CustomApiTemplates.Load(TemplateName, TemplateDirName, true, _reporter),
				CustomApiTemplates.LoadResponse(TemplateName, TemplateDirName, true, _reporter),
				_reporter,
				_degreeOfParallelism,
				cancellationToken);
				break;
		}
	}

	[GeneratedRegex("^\\s*$\\n|\\r", RegexOptions.Multiline)]
	private static partial Regex RemoveEmptyLinesRegexCompiled();
}
