using System.Runtime.InteropServices;
using DataverseGen.Cli.Commands;
using Spectre.Console;
using Spectre.Console.Cli;
using static DataverseGen.Cli.ConsoleOutput;

namespace DataverseGen.Cli;

//*
// NOTE!!
// DO NOT USE 'Trim unused assemblies'! It'll break json deserialization!
//*//
internal static class Program
{
	private const string NoPauseArgument = "--no-pause";

	private static int Main(string[] args)
	{
		// Pause and resize only when started in its own console window (e.g. double-clicked exe),
		// not when run from an existing terminal (e.g. as a dotnet tool).
		bool ownConsoleWindow = IsOwnConsoleWindow();
		bool pauseOnExit = ownConsoleWindow
			&& !Console.IsInputRedirected
			&& !args.Contains(NoPauseArgument, StringComparer.OrdinalIgnoreCase);

		try
		{
			Console.OutputEncoding = System.Text.Encoding.UTF8;

			if (ownConsoleWindow)
			{
				TrySetWindowSize(140, 30);
			}

			CommandApp<GenerateCommand> app = new();
			app.Configure(config =>
			{
				config.SetApplicationName("dataversegen");
				config.AddCommand<GenerateCommand>("generate")
					.WithDescription("Generate code from Dataverse metadata (default command).");
				config.AddCommand<InitCommand>("init")
					.WithDescription("Create an example dataversegen.config.json in the current directory.");
				config.AddCommand<TemplatesCommand>("templates")
					.WithDescription("Copy the built-in Scriban templates to the current directory.");
			});

			return app.Run(NormalizeArguments(args));
		}
		finally
		{
			if (pauseOnExit)
			{
				WriteLine("Press any key to exit", Color.Grey);
				Console.ReadKey();
			}
		}
	}

	/// <summary>Drops <c>--no-pause</c> and maps a leading <c>--init|-i</c> / <c>--templates|-t</c> flag to its command.</summary>
	internal static string[] NormalizeArguments(string[] args)
	{
		string[] rest = args
			.Where(arg => !arg.Equals(NoPauseArgument, StringComparison.OrdinalIgnoreCase))
			.ToArray();

		if (rest.Length == 0)
		{
			return rest;
		}

		string? command = rest[0].ToLowerInvariant() switch
		{
			"--init" or "-i" => "init",
			"--templates" or "-t" => "templates",
			_ => null
		};

		return command is null ? rest : [command, .. rest.Skip(1)];
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern uint GetConsoleProcessList(uint[] processList, uint processCount);

	private static bool IsOwnConsoleWindow()
	{
		if (Console.IsOutputRedirected)
		{
			return false;
		}

		uint[] processList = new uint[2];

		return GetConsoleProcessList(processList, (uint)processList.Length) == 1;
	}

	private static void TrySetWindowSize(int width, int height)
	{
		try
		{
			Console.SetWindowSize(width, height);
		}
		catch (Exception e) when (e is IOException or ArgumentOutOfRangeException or PlatformNotSupportedException)
		{
			// Window size is cosmetic; ignore hosts that do not allow resizing.
		}
	}
}
