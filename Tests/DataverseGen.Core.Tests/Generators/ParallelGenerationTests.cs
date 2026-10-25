using DataverseGen.Core.Config;
using DataverseGen.Core.CustomApi;
using DataverseGen.Core.Generators.Scriban;
using DataverseGen.Core.Metadata;
using DataverseGen.Core.Reporting;
using Microsoft.Xrm.Sdk.Metadata;

namespace DataverseGen.Core.Tests.Generators;

[TestClass]
public class ParallelGenerationTests
{
	private const int EntityCount = 24;

	[TestMethod]
	[DataRow("C#", false)]
	[DataRow("C#", true)]
	[DataRow("ts", false)]
	[DataRow("ts", true)]
	public void Generate_WithManyEntities_IsIdenticalForSequentialAndParallel(string type, bool isSingleOutput)
	{
		Dictionary<string, string> sequential = Generate(type, isSingleOutput, 1, out _);
		Dictionary<string, string> parallel = Generate(type, isSingleOutput, 8, out _);

		CollectionAssert.AreEquivalent(sequential.Keys.ToArray(), parallel.Keys.ToArray());

		foreach ((string fileName, string content) in sequential)
		{
			Assert.AreEqual(content, parallel[fileName], $"'{fileName}' differs between --parallel 1 and 8.");
		}
	}

	[TestMethod]
	public void Generate_Parallel_ReportsExactlyOneCompletionPerItem()
	{
		Generate("C#", false, 8, out RecordingReporter reporter);

		List<ItemResult> entities = reporter.Items.Where(i => i.Kind == ItemKind.Entity).ToList();
		List<ItemResult> customApis = reporter.Items.Where(i => i.Kind == ItemKind.CustomApi).ToList();

		Assert.HasCount(EntityCount, entities);
		Assert.HasCount(EntityCount, entities.Select(i => i.Name).Distinct());
		Assert.HasCount(1, customApis);
		Assert.HasCount(1, reporter.Items.Where(i => i.Kind == ItemKind.Output));
		Assert.HasCount(1, reporter.Completed);
	}

	[TestMethod]
	public void Generate_Parallel_WritesCustomApiFilesNextToEntities()
	{
		Dictionary<string, string> files = Generate("ts", false, 8, out _);

		Assert.IsTrue(files.ContainsKey("customapi/new_do.customapi.ts"), string.Join(", ", files.Keys));
	}

	[TestMethod]
	public async Task GenerateTemplateAsync_WhenCancelled_ThrowsAndDoesNotCompleteTheStage()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		RecordingReporter reporter = new();

		await WithTempDirectory(async () =>
		{
			ScribanGenerator generator = CreateGenerator("C#", false, 8, reporter);

			await Assert.ThrowsAsync<OperationCanceledException>(
				() => generator.GenerateTemplateAsync(cancellation.Token));
		});

		Assert.IsEmpty(reporter.Completed);
		Assert.IsEmpty(reporter.Items);
	}

	private static Dictionary<string, string> Generate(
		string type,
		bool isSingleOutput,
		int degreeOfParallelism,
		out RecordingReporter reporter)
	{
		RecordingReporter recorder = new();
		Dictionary<string, string> result = new();

		WithTempDirectory(() =>
		{
			CreateGenerator(type, isSingleOutput, degreeOfParallelism, recorder).GenerateTemplate();

			foreach (string file in Directory.GetFiles("out", "*", SearchOption.AllDirectories))
			{
				result[Path.GetRelativePath("out", file).Replace('\\', '/')] =
					Normalize(File.ReadAllText(file));
			}

			return Task.CompletedTask;
		}).GetAwaiter().GetResult();

		reporter = recorder;

		return result;
	}

	private static ScribanGenerator CreateGenerator(
		string type,
		bool isSingleOutput,
		int degreeOfParallelism,
		IGenerationReporter reporter)
	{
		MappingEntity[] entities = Enumerable.Range(0, EntityCount)
		   .Select(i =>
			{
				EntityMetadata metadata = ScribanGeneratorTests.BuildProjectEntity();
				metadata.LogicalName = $"new_project{i:00}";
				metadata.SchemaName = $"new_Project{i:00}";
				MappingEntity entity = MappingEntity.Parse(metadata);
				ScribanGeneratorTests.LinkSelfReferencedRelationships(entity);

				return entity;
			})
		   .ToArray();

		Context context = new()
		{
			Namespace = "Test.Dataverse",
			Entities = entities,
			CustomApis =
			[
				new CustomApiModel
				{
					UniqueName = "new_do",
					DisplayName = "Do",
					RequestClassName = "new_do",
					ResponseClassName = "new_doResponse",
					RequestParameters = [],
					ResponseProperties = []
				}
			]
		};

		return new ScribanGenerator("Main",
			"Templates",
			"out",
			context,
			new TemplateEngineModel { Name = "scriban", Type = type, IsSingleOutput = isSingleOutput },
			null,
			false,
			reporter,
			degreeOfParallelism);
	}

	private static async Task WithTempDirectory(Func<Task> action)
	{
		string original = Directory.GetCurrentDirectory();
		string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);

		try
		{
			Directory.SetCurrentDirectory(root);
			await action();
		}
		finally
		{
			Directory.SetCurrentDirectory(original);
			Directory.Delete(root, true);
		}
	}

	// GeneratorInfo embeds the host process file version, which is identical within one run but not worth comparing.
	private static string Normalize(string content)
	{
		return System.Text.RegularExpressions.Regex.Replace(content,
			@"(Runtime Version|File Version):[^\r\n]*",
			"$1: <version>");
	}
}
