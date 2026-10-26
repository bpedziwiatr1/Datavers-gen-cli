namespace DataverseGen.Core.Generators;

/// <summary>Resolves output and template locations; relative paths are anchored at a base directory (default: CWD).</summary>
internal static class GeneratorPaths
{
	public static string ResolveOutput(string outPath, string baseDirectory = null)
	{
		return Path.GetFullPath(Path.Combine(baseDirectory ?? Directory.GetCurrentDirectory(), outPath ?? string.Empty))
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
		return Path.GetFullPath(Path.Combine(baseDirectory ?? Directory.GetCurrentDirectory(), templateDirName ?? string.Empty,
			templateName ?? string.Empty)) + Path.DirectorySeparatorChar;
	}
}
