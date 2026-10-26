#nullable enable annotations
using Scriban;
using Scriban.Parsing;

namespace DataverseGen.Core.Generators.Scriban.Templates.TemplateFileManager;

internal static class TemplateParser
{
	public static Template Parse(string text, string sourcePath, ParserOptions? parserOptions = null, LexerOptions? lexerOptions = null)
	{
		Template template = Template.Parse(text, sourcePath, parserOptions, lexerOptions);

		if (template.HasErrors)
		{
			throw new InvalidOperationException(
				$"Template {sourcePath ?? "(built-in)"} is invalid: {string.Join("; ", template.Messages)}");
		}

		return template;
	}

	public static Template ParseFile(string path)
	{
		return Parse(File.ReadAllText(path), path);
	}
}
