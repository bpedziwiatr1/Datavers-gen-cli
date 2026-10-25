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
		switch (session.TemplateEngineModel.Type.ToLowerInvariant())
		{
			case "c#":
				return session.TemplateEngineModel.IsSingleOutput
					? RunSingleFile(session, "XrmServiceContext.cs", ScribanTemplates.dataversegen_single)
					: MultiOutputGeneratorCSharp(session, cancellationToken);

			case "ts":
				return session.TemplateEngineModel.IsSingleOutput
					? RunSingleFile(session, "dataverse.metadata.ts", ScribanTemplates.dataversegen_single_ts)
					: MultiOutputGeneratorTypeScript(session, cancellationToken);

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

	private Action MultiOutputGeneratorCSharp(GenerationSession session, CancellationToken cancellationToken)
	{
		MultipleFileTemplateCsharp templates = new(session.TemplateName, session.TemplateDirName, session.Reporter);
		templates.Init();

		Parallel.ForEach(session.Context.Entities,
			session.CreateParallelOptions(cancellationToken),
			entity =>
			{
				session.Reporter.ItemStarted(ItemKind.Entity, ItemPhase.Generate, entity.LogicalName);
				Stopwatch entityTimer = Stopwatch.StartNew();
				long bytes = WriteEntityTemplateToFile(session, templates.EntityTemplate, entity, _legacyEntityFileNames ? "cs" : "entity.cs");
				bytes += WriteEntityTemplateToFile(session, templates.FieldsTemplate, entity, "Fields.cs");
				bytes += WriteEntityTemplateToFile(session, templates.EnumsTemplate, entity, "Enums.cs");
				ReportEntityGenerated(session, entity, 3, bytes, entityTimer.Elapsed);
			});

		return () => WriteXrmServiceContext(session, templates);
	}

	private static void WriteXrmServiceContext(GenerationSession session, MultipleFileTemplateCsharp templates)
	{
		session.Reporter.ItemStarted(ItemKind.Output, ItemPhase.Generate, "XrmServiceContext.cs");
		Stopwatch contextTimer = Stopwatch.StartNew();
		string xrmContextContent =
			RemoveEmptyLines(templates.XrmContextTemplate.Render(EntityViewBuilder.BuildContextView(session.Context)));

		long contextBytes = session.Files.WriteFile($"{session.FullOutputPath}/XrmServiceContext.cs", xrmContextContent);
		ReportOutputGenerated(session, "XrmServiceContext.cs", contextBytes, contextTimer.Elapsed);
	}

	private Action MultiOutputGeneratorTypeScript(GenerationSession session, CancellationToken cancellationToken)
	{
		MultipleFileTemplateTypescript templates = new(session.TemplateName, session.TemplateDirName, session.Reporter);
		templates.Init();

		Parallel.ForEach(session.Context.Entities,
			session.CreateParallelOptions(cancellationToken),
			entity =>
			{
				session.Reporter.ItemStarted(ItemKind.Entity, ItemPhase.Generate, entity.LogicalName);
				Stopwatch entityTimer = Stopwatch.StartNew();
				long bytes = WriteEntityTemplateToFile(session, templates.EntityTemplate, entity, _legacyEntityFileNames ? "attributes.ts" : "entity.ts");
				bytes += WriteEntityTemplateToFile(session, templates.EnumsTemplate, entity, "enums.ts");
				bytes += WriteEntityTemplateToFile(session, templates.RelationshipTemplate, entity, "relationships.ts");
				ReportEntityGenerated(session, entity, 3, bytes, entityTimer.Elapsed);
			});

		return null;
	}

	private static Action RunSingleFile(GenerationSession session, string fileName, byte[] templateContent)
	{
		session.Reporter.ItemStarted(ItemKind.Output, ItemPhase.Generate, fileName);
		Stopwatch timer = Stopwatch.StartNew();
		Template template = Template.Parse(Encoding.UTF8.GetString(templateContent),
			null,
			new ParserOptions(),
			new LexerOptions { Mode = ScriptMode.Default });

		string content = RemoveEmptyLines(ScribanRenderer.Render(template, EntityViewBuilder.BuildContextView(session.Context)));
		long bytes = session.Files.WriteFile($"{session.FullOutputPath}{fileName}", content);
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

	private static long WriteEntityTemplateToFile(
		GenerationSession session,
		Template template,
		MappingEntity entity,
		string outputFileSuffixWithExtension)
	{
		string entityContent =
			RemoveEmptyLines(ScribanRenderer.Render(template, EntityViewBuilder.BuildEntityView(session.Context, entity)))
			   .Replace("\n", "\r\n");

		if (outputFileSuffixWithExtension.Contains("ts"))
		{
			entityContent = $"{entityContent}\r\n";
		}

		string entityPath = $"{session.FullOutputPath}/{entity.HybridName.ToLower()}.{outputFileSuffixWithExtension}";
		long bytes = session.Files.WriteFile(entityPath, entityContent);
		session.Reporter.Log(LogLevel.Debug, $@"Generated entity: {entity.LogicalName} -> {entityPath}");

		return bytes;
	}

	[GeneratedRegex("^\\s*$\\n|\\r", RegexOptions.Multiline)]
	private static partial Regex RemoveEmptyLinesRegexCompiled();
}
