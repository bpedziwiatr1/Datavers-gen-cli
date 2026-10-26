using DataverseGen.Core.Config;
using DataverseGen.Core.Generators.Scriban.Templates.TemplateFileManager;
using DataverseGen.Core.Scaffolding;
using Newtonsoft.Json;

namespace DataverseGen.Core.Tests.Scaffolding;

[TestClass]
public class ScaffolderTests
{
	private string _dir = null!;

	[TestInitialize]
	public void Init()
	{
		_dir = Path.Combine(Path.GetTempPath(), "dvgen-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_dir);
	}

	[TestCleanup]
	public void Cleanup()
	{
		Directory.Delete(_dir, true);
	}

	[TestMethod]
	public void ConfigScaffolder_WritesDeserializableConfig()
	{
		string path = ConfigScaffolder.Write(_dir, new ConfigScaffoldOptions { Namespace = "A.B", Type = "ts" }, false);

		ConfigModel? model = JsonConvert.DeserializeObject<ConfigModel>(File.ReadAllText(path));

		Assert.IsNotNull(model);
		Assert.AreEqual("A.B", model.Namespace);
		Assert.AreEqual("ts", model.TemplateEngine.Type);
		ConfigValidation.Validate(model);
	}

	[TestMethod]
	public void ConfigScaffolder_ExistingFile_RequiresForce()
	{
		ConfigScaffolder.Write(_dir, new ConfigScaffoldOptions(), false);

		Assert.ThrowsExactly<IOException>(() => ConfigScaffolder.Write(_dir, new ConfigScaffoldOptions(), false));
		ConfigScaffolder.Write(_dir, new ConfigScaffoldOptions(), true);
	}

	[TestMethod]
	[DataRow(false)]
	[DataRow(true)]
	public void TemplateScaffolder_WritesAllFilesAndLoadsAsCustomSet(bool isTypeScript)
	{
		string templatesRoot = Path.Combine(_dir, "Templates");
		string target = Path.Combine(templatesRoot, "Custom");

		string[] written = TemplateScaffolder.Write(target, isTypeScript, false);

		CollectionAssert.AreEquivalent(TemplateScaffolder.GetTemplateFileNames(isTypeScript),
			written.Select(Path.GetFileName).ToArray());
		Assert.ThrowsExactly<IOException>(() => TemplateScaffolder.Write(target, isTypeScript, false));

		string previous = Directory.GetCurrentDirectory();
		Directory.SetCurrentDirectory(_dir);

		try
		{
			if (isTypeScript)
			{
				_ = new MultipleFileTemplateTypescript("Custom", "Templates");
			}
			else
			{
				_ = new MultipleFileTemplateCsharp("Custom", "Templates");
			}
		}
		finally
		{
			Directory.SetCurrentDirectory(previous);
		}
	}
}
