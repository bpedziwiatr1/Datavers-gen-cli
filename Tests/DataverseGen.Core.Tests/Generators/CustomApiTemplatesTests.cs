using DataverseGen.Core.CustomApi;
using DataverseGen.Core.Generators;
using DataverseGen.Core.Generators.Scriban.Templates.TemplateFileManager;
using Scriban;

namespace DataverseGen.Core.Tests.Generators;

[TestClass]
public class CustomApiTemplatesTests
{
	private static readonly CustomApiModel Api = new()
	{
		RequestClassName = "PingRequest",
		OperationName = "newping"
	};

	[TestMethod]
	public void Load_MainTemplate_UsesBuiltInTemplate()
	{
		WithTemplateDirectory(null, () =>
		{
			Template template = CustomApiTemplates.Load("Main", "Templates", false);

			StringAssert.Contains(CustomApiCSharpGenerator.RenderRequestClass("Ns", Api, template),
				"public sealed class PingRequest : OrganizationRequest");
		});
	}

	[TestMethod]
	public void Load_CustomTemplateWithFile_UsesFileForCSharp()
	{
		WithTemplateDirectory("// {{namespace}}.{{class_name}} {{operation_name}}", () =>
		{
			Template template = CustomApiTemplates.Load("Custom", "Templates", false);

			Assert.AreEqual("// Ns.PingRequest newping", CustomApiCSharpGenerator.RenderRequestClass("Ns", Api, template));
		});
	}

	[TestMethod]
	public void Load_CustomTemplateWithFile_UsesFileForTypeScript()
	{
		WithTemplateDirectory("// {{class_name}} {{operation_type}}", () =>
		{
			Template template = CustomApiTemplates.Load("Custom", "Templates", true);

			Assert.AreEqual("// PingRequest WebApiRequestOperationType.Action",
				CustomApiTypeScriptGenerator.RenderRequestClass(Api, template));
		});
	}

	[TestMethod]
	public void Load_CustomTemplateWithoutFile_FallsBackToBuiltInTemplate()
	{
		WithTemplateDirectory(null, () =>
		{
			Directory.CreateDirectory(Path.Combine("Templates", "Custom"));

			Template template = CustomApiTemplates.Load("Custom", "Templates", true);

			StringAssert.Contains(CustomApiTypeScriptGenerator.RenderRequestClass(Api, template),
				"export default class PingRequest implements IWebApiRequest");
		});
	}

	[TestMethod]
	public void Load_InvalidCustomTemplate_ThrowsWithParserMessage()
	{
		WithTemplateDirectory("{{ for p in }}", () =>
		{
			InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
				() => CustomApiTemplates.Load("Custom", "Templates", false));

			StringAssert.Contains(exception.Message, "customapi.sbncs");
		});
	}

	// Custom templates are resolved against the current directory, like the entity templates.
	private static void WithTemplateDirectory(string? customTemplate, Action test)
	{
		string originalDirectory = Directory.GetCurrentDirectory();
		string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempRoot);

		try
		{
			Directory.SetCurrentDirectory(tempRoot);

			if (customTemplate != null)
			{
				string templateDirectory = Path.Combine(tempRoot, "Templates", "Custom");
				Directory.CreateDirectory(templateDirectory);
				File.WriteAllText(Path.Combine(templateDirectory, "customapi.sbncs"), customTemplate);
			}

			test();
		}
		finally
		{
			Directory.SetCurrentDirectory(originalDirectory);
			Directory.Delete(tempRoot, true);
		}
	}
}
