using DataverseGen.Core.CustomApi;
using DataverseGen.Core.Reporting;

namespace DataverseGen.Core.Tests.Generators;

[TestClass]
public class CustomApiGenerateReportingTests
{
	private static CustomApiModel[] Apis()
	{
		return
		[
			new CustomApiModel { UniqueName = "new_ping", RequestClassName = "PingRequest", ResponseClassName = "PingResponse", OperationName = "new_ping" },
			new CustomApiModel
			{
				UniqueName = "new_status",
				RequestClassName = "StatusRequest",
				ResponseClassName = "StatusResponse",
				OperationName = "new_status",
				ResponseProperties = [new CustomApiParameterModel { PropertyName = "Text", RequestPropertyName = "text", TypeScriptType = "string" }]
			}
		];
	}

	[TestMethod]
	public void Generate_TypeScript_ReportsGeneratePhaseWithFilesAndBytes()
	{
		string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		RecordingReporter reporter = new();

		try
		{
			CustomApiTestHost.Write(root, "ts", Apis(), reporter);

			Assert.HasCount(2, reporter.Items);
			Assert.IsTrue(reporter.Items.All(i => i.Phase == ItemPhase.Generate && i.Kind == ItemKind.CustomApi));
			Assert.AreEqual(1, reporter.Items[0].Files);
			Assert.AreEqual(2, reporter.Items[1].Files);
			Assert.AreEqual(Directory.GetFiles(root).Sum(f => new FileInfo(f).Length), reporter.Items.Sum(i => i.Bytes));
		}
		finally
		{
			if (Directory.Exists(root))
			{
				Directory.Delete(root, true);
			}
		}
	}

	[TestMethod]
	public void Generate_CSharp_ReportsGeneratePhaseWithFilesAndBytes()
	{
		string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		RecordingReporter reporter = new();

		try
		{
			CustomApiTestHost.Write(root, "C#", Apis(), reporter);

			Assert.AreEqual(new[] { 1, 2 }.Sum(), reporter.Items.Sum(i => i.Files));
			Assert.AreEqual(Directory.GetFiles(root).Sum(f => new FileInfo(f).Length), reporter.Items.Sum(i => i.Bytes));
		}
		finally
		{
			if (Directory.Exists(root))
			{
				Directory.Delete(root, true);
			}
		}
	}
}
