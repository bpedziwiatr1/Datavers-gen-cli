namespace DataverseGen.Core.Generators;

/// <summary>Resolves output and template locations; relative paths are anchored at a base directory (default: CWD).</summary>
internal static class GeneratorPaths
{
	public static string ResolveOutput(string outPath, string baseDirectory = null)
	{
		return Path.GetFullPath(Combine(baseDirectory, outPath))
		   .TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
	}

	public static string ResolveCustomApi(string fullOutputPath, string customApiOutPath, string baseDirectory = null)
	{
		return string.IsNullOrWhiteSpace(customApiOutPath)
			? Path.Combine(fullOutputPath, "customapi")
			: ResolveOutput(customApiOutPath, baseDirectory);
	}

	public static string ResolveTemplateDirectory(string templateDirName, string templateName, string baseDirectory = null)
	{
		return Path.GetFullPath(Path.Combine(Combine(baseDirectory, templateDirName), templateName ?? string.Empty))
			+ Path.DirectorySeparatorChar;
	}

	private static string Combine(string baseDirectory, string path)
	{
		string root = baseDirectory ?? Directory.GetCurrentDirectory();
		string relative = path ?? string.Empty;

		return Path.IsPathFullyQualified(relative)
			? relative
			: Path.Combine(root, relative.TrimStart('\\', '/'));
	}
}
