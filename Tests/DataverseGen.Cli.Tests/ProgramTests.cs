namespace DataverseGen.Cli.Tests;

[TestClass]
public class ProgramTests
{
	[TestMethod]
	[DataRow("-i", "init")]
	[DataRow("--init", "init")]
	[DataRow("-t", "templates")]
	[DataRow("--templates", "templates")]
	[DataRow("--INIT", "init")]
	public void NormalizeArguments_MapsLeadingFlagToCommand(string flag, string command)
	{
		string[] result = Program.NormalizeArguments([flag, "--force"]);

		CollectionAssert.AreEqual(new[] { command, "--force" }, result);
	}

	[TestMethod]
	public void NormalizeArguments_StripsNoPauseAnywhere()
	{
		CollectionAssert.AreEqual(new[] { "-v", "--log" },
			Program.NormalizeArguments(["--no-pause", "-v", "--log"]));
		CollectionAssert.AreEqual(new[] { "init" }, Program.NormalizeArguments(["--init", "--NO-PAUSE"]));
	}

	[TestMethod]
	public void NormalizeArguments_LeavesOtherArgumentsUntouched()
	{
		CollectionAssert.AreEqual(new[] { "generate", "-c", "x.json" },
			Program.NormalizeArguments(["generate", "-c", "x.json"]));
		Assert.IsEmpty(Program.NormalizeArguments([]));
		Assert.IsEmpty(Program.NormalizeArguments(["--no-pause"]));
	}

	[TestMethod]
	public void NormalizeArguments_OnlyMapsTheFirstArgument()
	{
		CollectionAssert.AreEqual(new[] { "generate", "-i" }, Program.NormalizeArguments(["generate", "-i"]));
	}
}
