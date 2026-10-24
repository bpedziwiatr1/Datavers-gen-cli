using DataverseGen.Core.Config;
using DataverseGen.Core.CustomApi;
using DataverseGen.Core.Generators.Scriban;
using DataverseGen.Core.Metadata;

namespace DataverseGen.Core.Tests.Generators;

[TestClass]
public class CustomApiOutputDirectoryTests
{
	[TestMethod]
	public void GenerateTemplate_WithoutCustomApiOutPath_WritesToCustomApiFolderInEntitiesFolder()
	{
		Run(null, root => Assert.IsTrue(File.Exists(Path.Combine(root, "entities", "customapi", "pingrequest.cs"))));
	}

	[TestMethod]
	public void GenerateTemplate_WithCustomApiOutPath_WritesToDedicatedFolder()
	{
		Run("apis", root =>
		{
			Assert.IsTrue(File.Exists(Path.Combine(root, "apis", "pingrequest.cs")));
			Assert.IsFalse(Directory.Exists(Path.Combine(root, "entities", "customapi")));
		});
	}

	private static void Run(string? customApiOutPath, Action<string> assert)
	{
		string original = Directory.GetCurrentDirectory();
		string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);

		try
		{
			Directory.SetCurrentDirectory(root);
			Context context = new()
			{
				Namespace = "N",
				Entities = [],
				CustomApis = [new CustomApiModel { RequestClassName = "PingRequest", OperationName = "newping" }]
			};

			new ScribanGenerator("Main",
				"Templates",
				"entities",
				context,
				new TemplateEngineModel { Name = "scriban", Type = "C#", IsSingleOutput = false },
				customApiOutPath).GenerateTemplate();

			assert(root);
		}
		finally
		{
			Directory.SetCurrentDirectory(original);
			Directory.Delete(root, true);
		}
	}
}
