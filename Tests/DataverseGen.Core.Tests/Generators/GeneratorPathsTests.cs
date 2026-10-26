using DataverseGen.Core.Generators;

namespace DataverseGen.Core.Tests.Generators;

[TestClass]
public class GeneratorPathsTests
{
	private static readonly string Base = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "dvgen-base"));

	[TestMethod]
	public void ResolveOutput_RelativePath_IsAnchoredAtBaseDirectory()
	{
		string path = GeneratorPaths.ResolveOutput(Path.Combine("out", "entities"), Base);

		Assert.AreEqual(Path.Combine(Base, "out", "entities") + Path.DirectorySeparatorChar, path);
	}

	[TestMethod]
	public void ResolveOutput_AbsolutePath_IgnoresBaseDirectory()
	{
		string absolute = Path.Combine(Path.GetTempPath(), "dvgen-abs", "out");

		Assert.AreEqual(Path.GetFullPath(absolute) + Path.DirectorySeparatorChar, GeneratorPaths.ResolveOutput(absolute, Base));
	}

	[TestMethod]
	public void ResolveCustomApi_NoOverride_UsesCustomApiFolderInsideOutput()
	{
		string output = GeneratorPaths.ResolveOutput("out", Base);

		Assert.AreEqual(Path.Combine(output, "customapi"), GeneratorPaths.ResolveCustomApi(output, " ", Base));
	}

	[TestMethod]
	public void ResolveCustomApi_Override_IsResolvedSeparately()
	{
		string output = GeneratorPaths.ResolveOutput("out", Base);

		Assert.AreEqual(Path.Combine(Base, "apis") + Path.DirectorySeparatorChar, GeneratorPaths.ResolveCustomApi(output, "apis", Base));
	}

	[TestMethod]
	public void ResolveTemplateDirectory_CombinesDirectoryAndName()
	{
		Assert.AreEqual(Path.Combine(Base, "Templates", "Custom") + Path.DirectorySeparatorChar,
			GeneratorPaths.ResolveTemplateDirectory("Templates", "Custom", Base));
	}
}
