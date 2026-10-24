using System.ComponentModel;
using DataverseGen.Core.Scaffolding;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DataverseGen.Cli.Commands;

internal sealed class TemplatesCommand : Command<TemplatesCommand.Settings>
{
	internal sealed class Settings : CommandSettings
	{
		[CommandOption("-t|--type <TYPE>")]
		[Description("Template set to copy: cs or ts.")]
		[DefaultValue("cs")]
		public string Type { get; init; } = "cs";

		[CommandOption("-n|--name <NAME>")]
		[Description("Template set name (folder under the templates directory). Defaults to MyTemplate / MyTsTemplate.")]
		public string? Name { get; init; }

		[CommandOption("-d|--dir <DIRECTORY>")]
		[Description("Templates directory name (TemplateDirectoryName in the config).")]
		[DefaultValue("Templates")]
		public string TemplatesDirectory { get; init; } = "Templates";

		[CommandOption("-f|--force")]
		[Description("Overwrite existing template files.")]
		public bool Force { get; init; }

		public override ValidationResult Validate()
		{
			return Type is "cs" or "ts"
				? ValidationResult.Success()
				: ValidationResult.Error("--type must be 'cs' or 'ts'.");
		}
	}

	public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
	{
		bool isTypeScript = settings.Type == "ts";
		string name = settings.Name ?? (isTypeScript ? "MyTsTemplate" : "MyTemplate");

		if (name.Equals("Main", StringComparison.OrdinalIgnoreCase))
		{
			AnsiConsole.MarkupLine("[red]\"Main\" is reserved for the built-in templates; choose another --name.[/]");

			return 1;
		}

		string target = Path.Combine(Directory.GetCurrentDirectory(), settings.TemplatesDirectory, name);

		try
		{
			string[] written = TemplateScaffolder.Write(target, isTypeScript, settings.Force);

			foreach (string path in written)
			{
				AnsiConsole.MarkupLineInterpolated($"[green]Created[/] {path}");
			}

			AnsiConsole.MarkupLineInterpolated(
				$"Set TemplateName to [bold]{name}[/] and TemplateDirectoryName to [bold]{settings.TemplatesDirectory}[/] in the config.");

			return 0;
		}
		catch (IOException e)
		{
			AnsiConsole.MarkupLineInterpolated($"[red]{e.Message}[/]");

			return 1;
		}
	}
}
