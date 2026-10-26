using DataverseGen.Core.Config;
using DataverseGen.Core.CustomApi;
using DataverseGen.Core.Generators;
using DataverseGen.Core.Generators.Scriban;
using DataverseGen.Core.Generators.Scriban.Templates.TemplateFileManager;
using DataverseGen.Core.Generators.Views;
using DataverseGen.Core.Metadata;
using DataverseGen.Core.Reporting;
using Scriban;

namespace DataverseGen.Core.Tests.Generators;

internal static class CustomApiTestHost
{
	internal const string Namespace = "MyApp.CustomApis";

	internal static void Write(
		string outPath,
		string type,
		CustomApiModel[]? customApis,
		IGenerationReporter? reporter = null)
	{
		IGenerationReporter activeReporter = reporter ?? NullReporter.Instance;
		GenerationSession session = new()
		{
			Context = new Context { Namespace = Namespace, CustomApis = customApis! },
			TemplateEngineModel = new TemplateEngineModel { Name = "scriban", Type = type },
			TemplateName = "Main",
			TemplateDirName = "Templates",
			FullOutputPath = outPath,
			CustomApiPath = outPath,
			Reporter = activeReporter,
			DegreeOfParallelism = 1,
			Files = new GeneratedFileSink(activeReporter)
		};

		new CustomApiScribanGenerator().Generate(session, CancellationToken.None);
	}

	internal static string RenderCSharpRequest(CustomApiModel api, Template? template = null, string ns = Namespace)
	{
		return CustomApiTemplates.Render(template ?? CustomApiTemplates.LoadBuiltIn(false),
			CustomApiCSharpViewBuilder.BuildView(ns, api));
	}

	internal static string RenderCSharpResponse(CustomApiModel api)
	{
		return CustomApiTemplates.Render(CustomApiTemplates.LoadBuiltInResponse(false),
			CustomApiCSharpViewBuilder.BuildResponseView(Namespace, api));
	}

	internal static string RenderTypeScriptRequest(CustomApiModel api, Template? template = null)
	{
		return CustomApiTemplates.Render(template ?? CustomApiTemplates.LoadBuiltIn(true),
			CustomApiTypeScriptViewBuilder.BuildView(api));
	}

	internal static string RenderTypeScriptResponse(CustomApiModel api)
	{
		return CustomApiTemplates.Render(CustomApiTemplates.LoadBuiltInResponse(true),
			CustomApiTypeScriptViewBuilder.BuildResponseView(api));
	}
}
