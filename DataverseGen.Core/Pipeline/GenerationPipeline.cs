using DataverseGen.Core.Config;
using DataverseGen.Core.CustomApi;
using DataverseGen.Core.DataConverter;
using DataverseGen.Core.Generators;
using DataverseGen.Core.Generators.Scriban;
using DataverseGen.Core.Metadata;
using DataverseGen.Core.Reporting;

namespace DataverseGen.Core.Pipeline;

/// <summary>Retrieves metadata from Dataverse and generates the code files; the host supplies config, connection and reporting.</summary>
public static class GenerationPipeline
{
	private const string ScribanEngine = "scriban";

	public static async Task<int> RunAsync(
		ConfigModel config,
		DataverseConnector connector,
		GenerationOptions options,
		IGenerationReporter reporter,
		CancellationToken cancellationToken)
	{
		// Connect once up front: both converters share the connection and clone it for their workers.
		connector.EnsureConnected();
		int parallelism = ResolveParallelism(connector, options.Parallelism, reporter);

		DataverseMetadataConverter dataConverter = new(connector,
			config.ThrowOnEntityNotFound,
			config.Entities,
			reporter,
			parallelism);
		DataverseCustomApiConverter customApiConverter = new(connector, config.CustomApis, reporter, parallelism);

		(MappingEntity[] entities, CustomApiModel[] customApis) = await RetrieveAsync(dataConverter.GetMappedEntitiesAsync,
			customApiConverter.GetCustomApisAsync,
			parallelism,
			cancellationToken);

		Context context = new()
		{
			Namespace = config.Namespace,
			Entities = entities,
			CustomApis = customApis
		};

		BaseGenerator generator = CreateGenerator(config, context, options, reporter, parallelism);

		await generator.GenerateTemplateAsync(cancellationToken);

		return parallelism;
	}

	internal static bool ShouldClearOutputDirectory(ConfigModel config, GenerationOptions options)
	{
		return config.ClearOutputDirectory && !options.NoClear;
	}

	internal static async Task<(TEntities Entities, TApis Apis)> RetrieveAsync<TEntities, TApis>(
		Func<CancellationToken, Task<TEntities>> getEntities,
		Func<CancellationToken, Task<TApis>> getCustomApis,
		int parallelism,
		CancellationToken cancellationToken)
	{
		if (parallelism <= 1)
		{
			TEntities sequentialEntities = await getEntities(cancellationToken);
			TApis sequentialApis = await getCustomApis(cancellationToken);

			return (sequentialEntities, sequentialApis);
		}

		// Entities and custom APIs are independent; the first failure cancels the other retrieval.
		using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		Task<TEntities> entitiesTask = CancelOnFault(() => getEntities(stop.Token), stop);
		Task<TApis> customApisTask = CancelOnFault(() => getCustomApis(stop.Token), stop);
		await Task.WhenAll(entitiesTask, customApisTask);

		return (entitiesTask.Result, customApisTask.Result);
	}

	internal static BaseGenerator CreateGenerator(
		ConfigModel config,
		Context context,
		GenerationOptions options,
		IGenerationReporter reporter,
		int parallelism)
	{
		if (config.TemplateEngine.Name != ScribanEngine)
		{
			reporter.Log(LogLevel.Warning, $@"Unsupported generator {config.TemplateEngine}, run scriban");
		}

		return new ScribanGenerator(config.TemplateName,
			config.TemplateDirectoryName,
			string.IsNullOrWhiteSpace(config.EntitiesOutDirectory) ? config.OutDirectory : config.EntitiesOutDirectory,
			context,
			config.TemplateEngine,
			config.CustomApiOutDirectory,
			config.LegacyEntityFileNames,
			reporter,
			parallelism,
			ShouldClearOutputDirectory(config, options));
	}

	private static int ResolveParallelism(DataverseConnector connector, int? requested, IGenerationReporter reporter)
	{
		int recommended = connector.RecommendedDegreesOfParallelism;
		int parallelism = DataverseConnector.ResolveDegreeOfParallelism(requested, recommended);
		reporter.Log(LogLevel.Info,
			requested is null
				? $"Parallelism {parallelism} (server recommends {recommended})"
				: $"Parallelism {parallelism}");

		return parallelism;
	}

	private static async Task<T> CancelOnFault<T>(Func<Task<T>> work, CancellationTokenSource stop)
	{
		try
		{
			return await work();
		}
		catch
		{
			await stop.CancelAsync();

			throw;
		}
	}
}
