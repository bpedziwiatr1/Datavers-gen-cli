using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using DataverseGen.Core.Generators.Scriban.Templates;
using DataverseGen.Core.Generators.Scriban.Templates.TemplateFileManager;
using DataverseGen.Core.Generators.Views;
using DataverseGen.Core.Metadata;
using DataverseGen.Core.Reporting;
using Scriban;
using Scriban.Parsing;

namespace DataverseGen.Core.Generators.Scriban;

public partial class EntitiesScribanGenerator : IEntityGenerator
{
	private readonly bool _legacyEntityFileNames;

	public EntitiesScribanGenerator(bool legacyEntityFileNames = false)
	{
		_legacyEntityFileNames = legacyEntityFileNames;
	}

	public Action Generate(GenerationSession session, CancellationToken cancellationToken)
	{
		bool isSingle = session.TemplateEngineModel.IsSingleOutput;

		switch (session.TemplateEngineModel.Type.ToLowerInvariant())
		{
			case "c#":
				return isSingle
					? RunSingleFile(session, "XrmServiceContext.cs", ScribanTemplates.dataversegen_single)
					: GenerateCSharpEntities(session, cancellationToken);

			case "ts":
				return isSingle
					? RunSingleFile(session, "dataverse.metadata.ts", ScribanTemplates.dataversegen_single_ts)
					: GenerateTypeScriptEntities(session, cancellationToken);

			default:
				throw new ArgumentOutOfRangeException("_templateEngineModel.Type",
					$@"type {session.TemplateEngineModel.Type} not supported use C# or TS");
		}
	}

	private static string RemoveEmptyLines(string lines)
	{
		return RemoveEmptyLinesRegexCompiled()
		   .Replace(lines, string.Empty)
		   .TrimEnd();
	}

	private Action GenerateCSharpEntities(GenerationSession session, CancellationToken cancellationToken)
	{
		MultipleFileTemplateCsharp templates = new(session.TemplateName, session.TemplateDirName, session.Reporter, session.BaseDirectory);
		templates.Init();

		WriteEntityFiles(session,
			cancellationToken,
			[
				new EntityOutput(templates.EntityTemplate, _legacyEntityFileNames ? "cs" : "entity.cs", false),
				new EntityOutput(templates.FieldsTemplate, "Fields.cs", false),
				new EntityOutput(templates.EnumsTemplate, "Enums.cs", false)
			]);

		return () => WriteXrmServiceContext(session, templates);
	}

	private Action GenerateTypeScriptEntities(GenerationSession session, CancellationToken cancellationToken)
	{
		MultipleFileTemplateTypescript templates = new(session.TemplateName, session.TemplateDirName, session.Reporter, session.BaseDirectory);
		templates.Init();

		WriteEntityFiles(session,
			cancellationToken,
			[
				new EntityOutput(templates.EntityTemplate, _legacyEntityFileNames ? "attributes.ts" : "entity.ts", true),
				new EntityOutput(templates.EnumsTemplate, "enums.ts", true),
				new EntityOutput(templates.RelationshipTemplate, "relationships.ts", true)
			]);

		return null;
	}

	private static void WriteEntityFiles(
		GenerationSession session,
		CancellationToken cancellationToken,
		EntityOutput[] outputs)
	{
		Parallel.ForEach(session.Context.Entities,
			session.CreateParallelOptions(cancellationToken),
			entity =>
			{
				session.Reporter.ItemStarted(ItemKind.Entity, ItemPhase.Generate, entity.LogicalName);
				Stopwatch entityTimer = Stopwatch.StartNew();
				long bytes = outputs.Sum(output => WriteEntityTemplateToFile(session, output, entity));
				ReportEntityGenerated(session, entity, outputs.Length, bytes, entityTimer.Elapsed);
			});
	}

	private static void WriteXrmServiceContext(GenerationSession session, MultipleFileTemplateCsharp templates)
	{
		session.Reporter.ItemStarted(ItemKind.Output, ItemPhase.Generate, "XrmServiceContext.cs");
		Stopwatch contextTimer = Stopwatch.StartNew();
		string xrmContextContent =
			RemoveEmptyLines(templates.XrmContextTemplate.Render(EntityViewBuilder.BuildContextView(session.Context)));

		long contextBytes = session.Files.WriteFile(Path.Combine(session.FullOutputPath, "XrmServiceContext.cs"), xrmContextContent);
		ReportOutputGenerated(session, "XrmServiceContext.cs", contextBytes, contextTimer.Elapsed);
	}

	private static Action RunSingleFile(GenerationSession session, string fileName, byte[] templateContent)
	{
		session.Reporter.ItemStarted(ItemKind.Output, ItemPhase.Generate, fileName);
		Stopwatch timer = Stopwatch.StartNew();
		Template template = TemplateParser.Parse(Encoding.UTF8.GetString(templateContent),
			null,
			new ParserOptions(),
			new LexerOptions { Mode = ScriptMode.Default });

		string content = RemoveEmptyLines(ScribanRenderer.Render(template, EntityViewBuilder.BuildContextView(session.Context)));
		long bytes = session.Files.WriteFile(Path.Combine(session.FullOutputPath, fileName), content);
		ReportOutputGenerated(session, fileName, bytes, timer.Elapsed);

		return null;
	}

	private static void ReportEntityGenerated(
		GenerationSession session,
		MappingEntity entity,
		int files,
		long bytes,
		TimeSpan elapsed)
	{
		session.Reporter.ItemCompleted(new ItemResult
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

	private static void ReportOutputGenerated(GenerationSession session, string fileName, long bytes, TimeSpan elapsed)
	{
		session.Reporter.ItemCompleted(new ItemResult
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

	private static long WriteEntityTemplateToFile(GenerationSession session, EntityOutput output, MappingEntity entity)
	{
		string entityContent =
			RemoveEmptyLines(ScribanRenderer.Render(output.Template, EntityViewBuilder.BuildEntityView(session.Context, entity)))
			   .Replace("\n", "\r\n");

		if (output.TrailingNewline)
		{
			entityContent = $"{entityContent}\r\n";
		}

		string entityPath = Path.Combine(session.FullOutputPath, $"{entity.HybridName.ToLower()}.{output.FileSuffix}");
		long bytes = session.Files.WriteFile(entityPath, entityContent);
		session.Reporter.Log(LogLevel.Debug, $@"Generated entity: {entity.LogicalName} -> {entityPath}");

		return bytes;
	}

	private sealed record EntityOutput(Template Template, string FileSuffix, bool TrailingNewline);

	[GeneratedRegex("^\\s*$\\n|\\r", RegexOptions.Multiline)]
	private static partial Regex RemoveEmptyLinesRegexCompiled();
}
