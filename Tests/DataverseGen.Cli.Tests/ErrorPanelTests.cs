using DataverseGen.Cli.Ui;
using Spectre.Console.Testing;

namespace DataverseGen.Cli.Tests;

[TestClass]
public class ErrorPanelTests
{
	private static TestConsole CreateConsole()
	{
		TestConsole console = new();
		console.Profile.Width = 120;

		return console;
	}

	[TestMethod]
	public void Render_ShowsMessageAndHintWithoutStackTrace()
	{
		TestConsole console = CreateConsole();
		UserFacingException error = new("Config not found: x.json", "Run `dataversegen init`.");

		ErrorPanel.Render(console, new Theme(true), error, false, null);

		Assert.Contains("Config not found: x.json", console.Output);
		Assert.Contains("Run `dataversegen init`.", console.Output);
		Assert.Contains("Re-run with -v", console.Output);
		Assert.DoesNotContain("   at ", console.Output);
	}

	[TestMethod]
	public void Render_VerboseIncludesStackTrace()
	{
		TestConsole console = CreateConsole();
		Exception error;

		try
		{
			throw new InvalidOperationException("kaboom");
		}
		catch (Exception e)
		{
			error = e;
		}

		ErrorPanel.Render(console, new Theme(true), error, true, null);

		Assert.Contains("InvalidOperationException", console.Output);
		Assert.Contains("   at ", console.Output);
		Assert.DoesNotContain("Re-run with -v", console.Output);
	}

	[TestMethod]
	public void Render_PointsToLogFileWhenOneIsWritten()
	{
		TestConsole console = CreateConsole();

		ErrorPanel.Render(console, new Theme(true), new Exception("x"), false, "run.log");

		Assert.Contains("run.log", console.Output);
	}

	[TestMethod]
	public void Render_MasksSecretsInMessageAndStackTrace()
	{
		TestConsole console = CreateConsole();

		ErrorPanel.Render(console, new Theme(true), new Exception("fail ClientSecret=abc123;Url=u"), true, null);

		Assert.DoesNotContain("abc123", console.Output);
		Assert.Contains("ClientSecret=***", console.Output);
	}

	[TestMethod]
	public void GetHint_MapsKnownFailures()
	{
		Assert.Contains("not valid JSON",
			ErrorPanel.GetHint(new Newtonsoft.Json.JsonReaderException("x"), true, null));
		Assert.Contains("ConnectionString",
			ErrorPanel.GetHint(new Exception("Connection did not connect with x"), true, null));
		Assert.Contains("configuration",
			ErrorPanel.GetHint(new InvalidOperationException("TemplateEngine must be configured."), true, null));
	}
}
