using System.Diagnostics;
using CommunityToolkit.Diagnostics;
using DataverseGen.Core.Metadata;
using DataverseGen.Core.Reporting;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace DataverseGen.Core.DataConverter;

public class DataverseMetadataConverter
{
	private const string ActivityPartySchemaName = "activityparty";
	private readonly DataverseConnector _dataverseConnector;
	private readonly IGenerationReporter _reporter;
	private readonly string[] _selectedEntities;
	private readonly bool _throwOnEntityNotFound;

	public DataverseMetadataConverter(
		DataverseConnector dataverseConnector,
		bool throwOnEntityNotFound,
		string[] selectedEntities,
		IGenerationReporter reporter = null)
	{
		Guard.IsNotNull(selectedEntities);
		Guard.IsNotNull(dataverseConnector);
		_selectedEntities = selectedEntities;

		_throwOnEntityNotFound = throwOnEntityNotFound;
		_dataverseConnector = dataverseConnector;
		_reporter = reporter ?? NullReporter.Instance;
		WriteConverterSetting();
	}

	public MappingEntity[] GetMappedEntities()
	{
		_dataverseConnector.Connect();

		Stopwatch stopper = Stopwatch.StartNew();
		_reporter.StageStarted(Stage.Entities, _selectedEntities.Length);

		IEnumerable<EntityMetadata> selectedEntities =
			SelectedEntitiesMetaData();

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

	private EntityMetadata RetrieveEntityMetadata(string selectedEntity)
	{
		_reporter.ItemStarted(ItemKind.Entity, ItemPhase.Retrieve, selectedEntity);
		Stopwatch stopper = Stopwatch.StartNew();

		try
		{
			RetrieveEntityRequest req = new()
			{
				EntityFilters = EntityFilters.All,
				LogicalName = selectedEntity,
				RetrieveAsIfPublished = true
			};

			RetrieveEntityResponse response =
				(RetrieveEntityResponse)_dataverseConnector.OrganizationService.Execute(req);
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
	}

	private IEnumerable<EntityMetadata> SelectedEntitiesMetaData()
	{
		bool isActivityPartyIncluded = false;
		HashSet<string> retrievedEntitiesSchemaNames = new(_selectedEntities.Length + 10);

		foreach (string selectedEntity in _selectedEntities)
		{
			if (retrievedEntitiesSchemaNames.Contains(selectedEntity))
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

			EntityMetadata entityEntityMetadata =
				RetrieveEntityMetadata(selectedEntity);

			if (entityEntityMetadata == null)
			{
				continue;
			}

			retrievedEntitiesSchemaNames.Add(entityEntityMetadata.LogicalName);

			yield return entityEntityMetadata;

			if (isActivityPartyIncluded
				|| (entityEntityMetadata.IsActivity != true
					&& entityEntityMetadata.IsActivityParty != true))
			{
				continue;
			}

			isActivityPartyIncluded = true;

			if (retrievedEntitiesSchemaNames.Contains(ActivityPartySchemaName))
			{
				continue;
			}

			EntityMetadata activityPartyMetadata =
				RetrieveEntityMetadata(ActivityPartySchemaName);

			if (activityPartyMetadata == null)
			{
				continue;
			}

			retrievedEntitiesSchemaNames.Add(ActivityPartySchemaName);

			yield return activityPartyMetadata;
		}
	}

	private void WriteConverterSetting()
	{
		_reporter.Log(LogLevel.Debug, $@"Selected entities: {string.Join(", ", _selectedEntities)}");
		_reporter.Log(LogLevel.Debug, $"Throw error if entity not found={_throwOnEntityNotFound}");
	}
}