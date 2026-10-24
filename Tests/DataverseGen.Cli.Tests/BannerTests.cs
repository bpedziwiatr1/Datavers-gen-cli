using DataverseGen.Cli.Ui;
using Spectre.Console.Testing;

namespace DataverseGen.Cli.Tests;

[TestClass]
public class BannerTests
{
	private static TestConsole CreateConsole()
	{
		TestConsole console = new();
		console.Profile.Width = 120;

		return console;
	}

	[TestMethod]
	public void Render_ShowsNameVersionRuntimeCwdConfigAndMode()
	{
		TestConsole console = CreateConsole();

		Banner.Render(console,
			new Theme(true),
			new BannerInfo("1.2.3", ".NET 10.0.0", @"C:\work", "my.config.json", "verbose on · parallelism 1"));

		Assert.Contains("› DataverseGen", console.Output);
		Assert.Contains("1.2.3", console.Output);
		Assert.Contains(".NET 10.0.0", console.Output);
		Assert.Contains(@"C:\work", console.Output);
		Assert.Contains("my.config.json", console.Output);
		Assert.Contains("verbose on", console.Output);
	}

	[TestMethod]
	public void Render_OmitsConfigAndModeWhenNotGiven()
	{
		TestConsole console = CreateConsole();

		Banner.Render(console, new Theme(true), new BannerInfo("1.0.0", ".NET 10", "cwd"));

		Assert.DoesNotContain("config", console.Output);
		Assert.DoesNotContain("mode", console.Output);
	}

	[TestMethod]
	public void Render_FallsBackToAsciiWithoutUnicode()
	{
		TestConsole console = CreateConsole();

		Banner.Render(console, new Theme(false), new BannerInfo("1.0.0", ".NET 10", "cwd"));

		Assert.Contains("> DataverseGen", console.Output);
		Assert.DoesNotContain("›", console.Output);
	}

	[TestMethod]
	public void Render_DoesNotInterpretMarkupInPaths()
	{
		TestConsole console = CreateConsole();

		Banner.Render(console, new Theme(true), new BannerInfo("1.0.0", ".NET 10", "C:\\[red]dir"));

		Assert.Contains("[red]dir", console.Output);
	}

	[TestMethod]
	public void Render_ShowsAsciiLogoOnWideConsole()
	{
		TestConsole console = CreateConsole();

		Banner.Render(console, new Theme(true), new BannerInfo("1.0.0", ".NET 10", "cwd"));

		Assert.Contains(@"/_____/\__,_/\__/\__,_/ |___/\___/_/  /____/\___/   \____/\___/_/ /_/", console.Output);
	}

	[TestMethod]
	public void Render_SkipsLogoOnNarrowConsole()
	{
		TestConsole console = CreateConsole();
		console.Profile.Width = 60;

		Banner.Render(console, new Theme(true), new BannerInfo("1.0.0", ".NET 10", "cwd"));

		Assert.DoesNotContain("/_____/", console.Output);
		Assert.Contains("DataverseGen", console.Output);
	}

	[TestMethod]
	public void GetVersion_DropsBuildMetadata()
	{
		Assert.DoesNotContain("+", Banner.GetVersion());
	}
}
