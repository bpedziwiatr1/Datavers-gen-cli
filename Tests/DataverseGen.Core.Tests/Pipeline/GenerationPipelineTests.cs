using DataverseGen.Core.Config;
using DataverseGen.Core.Generators.Scriban;
using DataverseGen.Core.Metadata;
using DataverseGen.Core.Pipeline;
using DataverseGen.Core.Reporting;
using DataverseGen.Core.Tests.Reporting;

namespace DataverseGen.Core.Tests.Pipeline;

[TestClass]
public class GenerationPipelineTests
{
	[TestMethod]
	public async Task RetrieveAsync_Sequential_RunsEntitiesBeforeCustomApis()
	{
		List<string> order = new();

		(string entities, int apis) = await GenerationPipeline.RetrieveAsync(
			async _ =>
			{
				order.Add("entities-start");
				await Task.Delay(30);
				order.Add("entities-end");

				return "e";
			},
			_ =>
			{
				order.Add("apis");

				return Task.FromResult(7);
			},
			1,
			CancellationToken.None);

		Assert.AreEqual("e", entities);
		Assert.AreEqual(7, apis);
		CollectionAssert.AreEqual(new[] { "entities-start", "entities-end", "apis" }, order);
	}

	[TestMethod]
	public async Task RetrieveAsync_Parallel_RunsBothAndReturnsResults()
	{
		(string entities, int apis) = await GenerationPipeline.RetrieveAsync(
			_ => Task.FromResult("e"),
			_ => Task.FromResult(7),
			4,
			CancellationToken.None);

		Assert.AreEqual("e", entities);
		Assert.AreEqual(7, apis);
	}

	[TestMethod]
	public async Task RetrieveAsync_Parallel_FaultCancelsTheOtherRetrieval()
	{
		bool otherWasCancelled = false;

		await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => GenerationPipeline.RetrieveAsync<string, int>(
			_ => throw new InvalidOperationException("boom"),
			async token =>
			{
				try
				{
					await Task.Delay(Timeout.Infinite, token);
				}
				catch (OperationCanceledException)
				{
					otherWasCancelled = true;

					throw;
				}

				return 0;
			},
			4,
			CancellationToken.None));

		Assert.IsTrue(otherWasCancelled);
	}

	[TestMethod]
	public async Task RetrieveAsync_ExternalCancellation_Propagates()
	{
		using CancellationTokenSource cts = new();
		await cts.CancelAsync();

		await Assert.ThrowsAsync<OperationCanceledException>(() => GenerationPipeline.RetrieveAsync<string, int>(
			async token =>
			{
				await Task.Delay(Timeout.Infinite, token);

				return "e";
			},
			async token =>
			{
				await Task.Delay(Timeout.Infinite, token);

				return 0;
			},
			4,
			cts.Token));
	}

	[TestMethod]
	public void CreateGenerator_Scriban_DoesNotWarn()
	{
		RecordingReporter reporter = new();

		object generator = GenerationPipeline.CreateGenerator(Config("scriban"), new Context(), new GenerationOptions(), reporter, 1);

		Assert.IsInstanceOfType<ScribanGenerator>(generator);
		Assert.IsEmpty(reporter.Logs);
	}

	[TestMethod]
	public void CreateGenerator_UnknownEngine_WarnsAndFallsBackToScriban()
	{
		RecordingReporter reporter = new();

		object generator = GenerationPipeline.CreateGenerator(Config("t4"), new Context(), new GenerationOptions(), reporter, 1);

		Assert.IsInstanceOfType<ScribanGenerator>(generator);
		Assert.HasCount(1, reporter.Logs);
		Assert.AreEqual(LogLevel.Warning, reporter.Logs[0].Level);
	}

	[TestMethod]
	[DataRow(true, false, true)]
	[DataRow(true, true, false)]
	[DataRow(false, false, false)]
	[DataRow(false, true, false)]
	public void ShouldClearOutputDirectory_CombinesConfigAndNoClear(bool configValue, bool noClear, bool expected)
	{
		ConfigModel config = Config("scriban");
		config.ClearOutputDirectory = configValue;

		bool result = GenerationPipeline.ShouldClearOutputDirectory(config, new GenerationOptions { NoClear = noClear });

		Assert.AreEqual(expected, result);
		Assert.AreEqual(configValue, config.ClearOutputDirectory);
	}

	private static ConfigModel Config(string engineName)
	{
		return new ConfigModel
		{
			Namespace = "Test",
			OutDirectory = "out",
			TemplateName = "Main",
			TemplateDirectoryName = "Templates",
			TemplateEngine = new TemplateEngineModel { Name = engineName, Type = "C#" }
		};
	}
}
