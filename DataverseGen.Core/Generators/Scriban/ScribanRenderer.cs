using Scriban;
using Scriban.Runtime;

namespace DataverseGen.Core.Generators.Scriban;

internal static class ScribanRenderer
{
	internal static string Render(Template template, object view)
	{
		ScriptObject scriptObject = new();
		scriptObject.Import(view);

		TemplateContext context = new()
		{
			LoopLimit = 0,
			RecursiveLimit = 0
		};

		context.PushGlobal(scriptObject);

		return template.Render(context);
	}
}
