using System.ComponentModel;
using DataverseGen.Core.Scaffolding;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DataverseGen.Cli.Commands;

internal sealed class InitCommand : Command<InitCommand.Settings>
{
	internal sealed class Settings : CommandSettings
	{
		[CommandOption("-n|--namespace <NAMESPACE>")]
		[Description("Namespace of the generated code.")]
		public string? Namespace { get; init; }

		[CommandOption("-t|--type <TYPE>")]
		[Description("Generator type: cs or ts.")]
		public string? Type { get; init; }

		[CommandOption("--single")]
		[Description("Generate a single output file instead of one file per entity.")]
		public bool Single { get; init; }

		[CommandOption("-o|--out <DIRECTORY>")]
		[Description("Output directory.")]
		public string? OutDirectory { get; init; }

		[CommandOption("-y|--yes")]
		[Description("Do not prompt; use defaults for options that are not provided.")]
		public bool Yes { get; init; }

		[CommandOption("-f|--force")]
		[Description("Overwrite an existing config file.")]
		public bool Force { get; init; }

		public override ValidationResult Validate()
		{
			return Type is null or "cs" or "ts"
				? ValidationResult.Success()
				: ValidationResult.Error("--type must be 'cs' or 'ts'.");
		}
	}

	public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
	{
		ConfigScaffoldOptions options = new();
		bool interactive = !settings.Yes && !Console.IsInputRedirected;

		options.Namespace = settings.Namespace
			?? (interactive
				? AnsiConsole.Prompt(new TextPrompt<string>("Namespace:").DefaultValue(options.Namespace))
				: options.Namespace);

		string type = settings.Type
			?? (interactive
				? AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Output language:").AddChoices("cs", "ts"))
				: "cs");

		options.Type = type == "ts" ? "ts" : "C#";

		options.IsSingleOutput = settings.Single
			|| (interactive && AnsiConsole.Confirm("Generate a single output file?", false));

		options.OutDirectory = settings.OutDirectory
			?? (interactive
				? AnsiConsole.Prompt(new TextPrompt<string>("Output directory:").DefaultValue(options.OutDirectory))
				: options.OutDirectory);

		try
		{
			string path = ConfigScaffolder.Write(Directory.GetCurrentDirectory(), options, settings.Force);
			AnsiConsole.MarkupLineInterpolated($"[green]Created[/] {path}");
			AnsiConsole.MarkupLine("Edit [bold]Entities[/] / [bold]CustomApis[/], then run [bold]dataversegen[/].");

			return 0;
		}
		catch (IOException e)
		{
			AnsiConsole.MarkupLineInterpolated($"[red]{e.Message}[/]");

			return 1;
		}
	}
}
