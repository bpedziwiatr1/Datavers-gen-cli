using System.Diagnostics;
using CommunityToolkit.Diagnostics;
using DataverseGen.Core.Metadata;
using DataverseGen.Core.Reporting;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace DataverseGen.Core.DataConverter;

public class DataverseMetadataConverter
{
	private const string ActivityPartySchemaName = "activityparty";
	private readonly DataverseConnector _dataverseConnector;
	private readonly IGenerationReporter _reporter;
	private readonly int _degreeOfParallelism;
	private readonly string[] _selectedEntities;
	private readonly bool _throwOnEntityNotFound;

	public DataverseMetadataConverter(
		DataverseConnector dataverseConnector,
		bool throwOnEntityNotFound,
		string[] selectedEntities,
		IGenerationReporter reporter = null,
		int degreeOfParallelism = 1)
	{
		Guard.IsNotNull(selectedEntities);
		Guard.IsNotNull(dataverseConnector);
		_selectedEntities = selectedEntities;

		_throwOnEntityNotFound = throwOnEntityNotFound;
		_dataverseConnector = dataverseConnector;
		_reporter = reporter ?? NullReporter.Instance;
		_degreeOfParallelism = Math.Max(1, degreeOfParallelism);
		WriteConverterSetting();
	}

	public MappingEntity[] GetMappedEntities()
	{
		return GetMappedEntitiesAsync(CancellationToken.None).GetAwaiter().GetResult();
	}

	public async Task<MappingEntity[]> GetMappedEntitiesAsync(CancellationToken cancellationToken = default)
	{
		_dataverseConnector.EnsureConnected();

		Stopwatch stopper = Stopwatch.StartNew();
		_reporter.StageStarted(Stage.Entities, _selectedEntities.Length);

		IEnumerable<EntityMetadata> selectedEntities =
			await SelectedEntitiesMetaDataAsync(cancellationToken);

		List<MappingEntity> mappedEntities =
			selectedEntities
			   .Select(MappingEntity.Parse)
			   .OrderBy(e => e.DisplayName)
			   .ToList();

		_reporter.Log(LogLevel.Debug, @"Selected Entities Retrieved");
		ExcludeRelationshipsNotIncluded(mappedEntities);

		foreach (MappingEntity ent in mappedEntities)
		{
			foreach (MappingRelationship1N rel in ent.RelationshipsOneToMany)
			{
				rel.ToEntity =
					mappedEntities.Find(e => e.LogicalName.Equals(rel.Attribute.ToEntity));
			}

			foreach (MappingRelationshipN1 rel in ent.RelationshipsManyToOne)
			{
				rel.ToEntity =
					mappedEntities.Find(e => e.LogicalName.Equals(rel.Attribute.ToEntity));
			}

			foreach (MappingRelationshipMn rel in ent.RelationshipsManyToMany)
			{
				rel.ToEntity =
					mappedEntities.Find(e => e.LogicalName.Equals(rel.Attribute.ToEntity));
			}
		}

		MappingEntity[] result = mappedEntities.ToArray();
		stopper.Stop();
		_reporter.StageCompleted(Stage.Entities, stopper.Elapsed);

		return result;
	}

	private static void ExcludeRelationshipsNotIncluded(IReadOnlyCollection<MappingEntity> mappedEntities)
	{
		foreach (MappingEntity ent in mappedEntities)
		{
			ent.RelationshipsOneToMany =
				ent.RelationshipsOneToMany
				   .Where(r =>
						mappedEntities.Select(m => m.LogicalName)
						   .Contains(r.Type))
				   .ToArray();

			ent.RelationshipsManyToOne =
				ent.RelationshipsManyToOne
				   .Where(r =>
						mappedEntities.Select(m => m.LogicalName)
						   .Contains(r.Type))
				   .ToArray();

			ent.RelationshipsManyToMany =
				ent.RelationshipsManyToMany
				   .Where(r =>
						mappedEntities.Select(m => m.LogicalName)
						   .Contains(r.Type))
				   .ToArray();
		}
	}

	private async Task<EntityMetadata> RetrieveEntityMetadataAsync(
		ServiceClientPool pool,
		string selectedEntity,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		_reporter.ItemStarted(ItemKind.Entity, ItemPhase.Retrieve, selectedEntity);
		Stopwatch stopper = Stopwatch.StartNew();
		ServiceClient client = pool.Rent();

		try
		{
			RetrieveEntityRequest req = new()
			{
				EntityFilters = EntityFilters.All,
				LogicalName = selectedEntity,
				RetrieveAsIfPublished = true
			};

			RetrieveEntityResponse response =
				(RetrieveEntityResponse)await client.ExecuteAsync(req, cancellationToken);
			_reporter.RequestCompleted(Stage.Entities, stopper.Elapsed);

			_reporter.Log(LogLevel.Debug,
				$@"Found entity: {selectedEntity}, metadata-id: {response.EntityMetadata.MetadataId}");
			_reporter.ItemCompleted(new ItemResult
			{
				Kind = ItemKind.Entity,
				Name = selectedEntity,
				Status = ItemStatus.Ok,
				Detail = $"{response.EntityMetadata.Attributes?.Length ?? 0} fields",
				Elapsed = stopper.Elapsed
			});

			return response.EntityMetadata;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			_reporter.Log(LogLevel.Debug, $@"Retrieving entity {selectedEntity} failed", ex);
			_reporter.ItemCompleted(new ItemResult
			{
				Kind = ItemKind.Entity,
				Name = selectedEntity,
				Status = _throwOnEntityNotFound ? ItemStatus.Failed : ItemStatus.NotFound,
				Detail = ex.Message,
				Elapsed = stopper.Elapsed
			});

			if (_throwOnEntityNotFound)
			{
				throw;
			}

			return null;
		}
		finally
		{
			pool.Return(client);
		}
	}

	/// <summary>
	///  Retrieves the selected entities (up to the configured degree of parallelism at a time) and returns them in
	///  the order of the selection, so the generated output does not depend on which request finishes first.
	/// </summary>
	private async Task<List<EntityMetadata>> SelectedEntitiesMetaDataAsync(CancellationToken cancellationToken)
	{
		HashSet<string> seen = new(_selectedEntities.Length + 10);
		List<string> uniqueEntities = new(_selectedEntities.Length);

		foreach (string selectedEntity in _selectedEntities)
		{
			if (!seen.Add(selectedEntity))
			{
				_reporter.ItemCompleted(new ItemResult
				{
					Kind = ItemKind.Entity,
					Name = selectedEntity,
					Status = ItemStatus.Warning,
					Detail = "duplicate, ignored"
				});

				continue;
			}

			uniqueEntities.Add(selectedEntity);
		}

		using ServiceClientPool pool = _dataverseConnector.CreatePool(_degreeOfParallelism);
		EntityMetadata[] retrieved = new EntityMetadata[uniqueEntities.Count];

		await Parallel.ForEachAsync(Enumerable.Range(0, uniqueEntities.Count),
			new ParallelOptions
			{
				MaxDegreeOfParallelism = _degreeOfParallelism,
				CancellationToken = cancellationToken
			},
			async (index, token) =>
			{
				retrieved[index] = await RetrieveEntityMetadataAsync(pool, uniqueEntities[index], token);
			});

		List<EntityMetadata> result = retrieved.Where(metadata => metadata != null).ToList();

		// The activity party entity is needed by activities; it follows the first activity entity,
		// unless it was selected explicitly.
		int firstActivity = result.FindIndex(metadata => metadata.IsActivity == true || metadata.IsActivityParty == true);

		if (firstActivity < 0 || result.Any(metadata => metadata.LogicalName == ActivityPartySchemaName))
		{
			return result;
		}

		EntityMetadata activityPartyMetadata =
			await RetrieveEntityMetadataAsync(pool, ActivityPartySchemaName, cancellationToken);

		if (activityPartyMetadata != null)
		{
			result.Insert(firstActivity + 1, activityPartyMetadata);
		}

		return result;
	}

	private void WriteConverterSetting()
	{
		_reporter.Log(LogLevel.Debug, $@"Selected entities: {string.Join(", ", _selectedEntities)}");
		_reporter.Log(LogLevel.Debug, $"Throw error if entity not found={_throwOnEntityNotFound}");
	}
}