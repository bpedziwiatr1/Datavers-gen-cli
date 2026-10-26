using System.Diagnostics;
using DataverseGen.Core.CustomApi;
using DataverseGen.Core.Generators.Scriban.Templates.TemplateFileManager;
using DataverseGen.Core.Generators.Views;
using DataverseGen.Core.Reporting;
using Scriban;

namespace DataverseGen.Core.Generators.Scriban;

public class CustomApiScribanGenerator : ICustomApiGenerator
{
	public void Generate(GenerationSession session, CancellationToken cancellationToken)
	{
		CustomApiLanguage language = GetLanguage(session);

		if (language == null || session.Context.CustomApis is not { Length: > 0 })
		{
			return;
		}

		Directory.CreateDirectory(session.CustomApiPath);

		Template template = CustomApiTemplates.Load(session.TemplateName,
			session.TemplateDirName,
			language.IsTypeScript,
			session.Reporter,
			session.BaseDirectory);
		Template responseTemplate = CustomApiTemplates.LoadResponse(session.TemplateName,
			session.TemplateDirName,
			language.IsTypeScript,
			session.Reporter,
			session.BaseDirectory);

		Parallel.ForEach(session.Context.CustomApis,
			session.CreateParallelOptions(cancellationToken),
			customApi => WriteCustomApi(session, language, template, responseTemplate, customApi));
	}

	private static CustomApiLanguage GetLanguage(GenerationSession session)
	{
		string ns = session.Context.Namespace;

		return session.TemplateEngineModel.Type.ToLowerInvariant() switch
		{
			"c#" => new CustomApiLanguage(false,
				"cs",
				api => CustomApiCSharpViewBuilder.BuildView(ns, api),
				api => CustomApiCSharpViewBuilder.BuildResponseView(ns, api)),
			"ts" => new CustomApiLanguage(true,
				"ts",
				CustomApiTypeScriptViewBuilder.BuildView,
				CustomApiTypeScriptViewBuilder.BuildResponseView),
			_ => null
		};
	}

	private static void WriteCustomApi(
		GenerationSession session,
		CustomApiLanguage language,
		Template template,
		Template responseTemplate,
		CustomApiModel customApi)
	{
		session.Reporter.ItemStarted(ItemKind.CustomApi, ItemPhase.Generate, customApi.UniqueName);
		Stopwatch timer = Stopwatch.StartNew();
		int files = 1;
		long bytes = WriteClass(session, language, customApi.RequestClassName, customApi.UniqueName, "custom API",
			CustomApiTemplates.Render(template, language.BuildRequestView(customApi)));

		if (customApi.ResponseProperties is { Length: > 0 })
		{
			files++;
			bytes += WriteClass(session, language, customApi.ResponseClassName, customApi.UniqueName, "custom API response",
				CustomApiTemplates.Render(responseTemplate, language.BuildResponseView(customApi)));
		}

		session.Reporter.ItemCompleted(new ItemResult
		{
			Kind = ItemKind.CustomApi,
			Phase = ItemPhase.Generate,
			Name = customApi.UniqueName,
			Status = ItemStatus.Ok,
			Elapsed = timer.Elapsed,
			Files = files,
			Bytes = bytes
		});
	}

	private static long WriteClass(
		GenerationSession session,
		CustomApiLanguage language,
		string className,
		string apiName,
		string description,
		string content)
	{
		string path = Path.Combine(session.CustomApiPath, $"{className.ToLowerInvariant()}.customapi.{language.Extension}");
		long bytes = session.Files.WriteFile(path, content);
		session.Reporter.Log(LogLevel.Debug, $@"Generated {description}: {apiName} -> {path}");

		return bytes;
	}

	private sealed record CustomApiLanguage(
		bool IsTypeScript,
		string Extension,
		Func<CustomApiModel, object> BuildRequestView,
		Func<CustomApiModel, object> BuildResponseView);
}
