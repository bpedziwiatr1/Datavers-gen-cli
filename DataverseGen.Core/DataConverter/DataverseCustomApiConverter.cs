using System.Diagnostics;
using System.Globalization;
using System.Text;
using DataverseGen.Core.CustomApi;
using DataverseGen.Core.Extensions;
using DataverseGen.Core.Reporting;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseGen.Core.DataConverter;

public class DataverseCustomApiConverter
{
	private const string CustomApiTableName = "customapi";
	private const string CustomApiRequestParameterTableName = "customapirequestparameter";
	private const string CustomApiResponsePropertyTableName = "customapiresponseproperty";

	private readonly DataverseConnector _dataverseConnector;
	private readonly int _degreeOfParallelism;
	private readonly IGenerationReporter _reporter;
	private readonly string[] _selectedCustomApis;

	public DataverseCustomApiConverter(
		DataverseConnector dataverseConnector,
		string[] selectedCustomApis,
		IGenerationReporter reporter = null,
		int degreeOfParallelism = 1)
	{
		_dataverseConnector = dataverseConnector;
		_selectedCustomApis = selectedCustomApis ?? Array.Empty<string>();
		_reporter = reporter ?? NullReporter.Instance;
		_degreeOfParallelism = Math.Max(1, degreeOfParallelism);
		if (HasSelection(_selectedCustomApis))
		{
			_reporter.Log(LogLevel.Debug, $@"Selected custom APIs: {string.Join(", ", _selectedCustomApis)}");
		}
	}

	internal static bool HasSelection(IReadOnlyCollection<string> selectedCustomApis)
	{
		return selectedCustomApis != null && selectedCustomApis.Any(name => !string.IsNullOrWhiteSpace(name));
	}

	public CustomApiModel[] GetCustomApis()
	{
		return GetCustomApisAsync(CancellationToken.None).GetAwaiter().GetResult();
	}

	public async Task<CustomApiModel[]> GetCustomApisAsync(CancellationToken cancellationToken = default)
	{
		if (!HasSelection(_selectedCustomApis))
		{
			_reporter.Log(LogLevel.Warning, @"No custom APIs configured in 'CustomApis' - skipping custom API generation");

			return Array.Empty<CustomApiModel>();
		}

		Stopwatch stopper = Stopwatch.StartNew();
		_reporter.StageStarted(Stage.CustomApis,
			_selectedCustomApis.Count(name => !string.IsNullOrWhiteSpace(name)));

		// The three tables are independent, so they are queried concurrently (each on its own cloned client).
		using ServiceClientPool pool = _dataverseConnector.CreatePool(_degreeOfParallelism);
		using SemaphoreSlim gate = new(_degreeOfParallelism);
		Task<List<Entity>> customApiTask = RetrieveAsync(pool, gate, CustomApiTableName, "custom APIs", cancellationToken);
		Task<List<Entity>> requestParameterTask =
			RetrieveAsync(pool, gate, CustomApiRequestParameterTableName, CustomApiRequestParameterTableName, cancellationToken);
		Task<List<Entity>> responsePropertyTask =
			RetrieveAsync(pool, gate, CustomApiResponsePropertyTableName, CustomApiResponsePropertyTableName, cancellationToken);
		await Task.WhenAll(customApiTask, requestParameterTask, responsePropertyTask);

		List<Entity> customApiEntities = customApiTask.Result;
		List<Entity> requestParameterEntities = requestParameterTask.Result;
		List<Entity> responsePropertyEntities = responsePropertyTask.Result;
		List<Entity> selectedCustomApiEntities =
			FilterCustomApiEntities(customApiEntities, _selectedCustomApis, _reporter);
		foreach (Entity selectedCustomApi in selectedCustomApiEntities)
		{
			_reporter.Log(LogLevel.Debug,
				$@"Found custom API: {GetString(selectedCustomApi, "uniquename")}, id: {GetEntityId(selectedCustomApi)}");
		}

		HashSet<string> selectedCustomApiIds = new(
			selectedCustomApiEntities.Select(GetEntityId),
			StringComparer.InvariantCultureIgnoreCase);

		Dictionary<string, List<CustomApiParameterModel>> requestParametersByApiId =
			GroupParametersByApiId(requestParameterEntities, selectedCustomApiIds, false);
		Dictionary<string, List<CustomApiParameterModel>> responsePropertiesByApiId =
			GroupParametersByApiId(responsePropertyEntities, selectedCustomApiIds, true);

		List<CustomApiModel> result = selectedCustomApiEntities
		   .Select(entity => ParseCustomApi(entity,
				requestParametersByApiId.GetValueOrDefault(GetEntityId(entity)) ?? new List<CustomApiParameterModel>(),
				responsePropertiesByApiId.GetValueOrDefault(GetEntityId(entity)) ?? new List<CustomApiParameterModel>()))
		   .OrderBy(api => api.RequestClassName)
		   .ToList();

		// The three queries are shared by all APIs, so every API gets an equal share of the retrieval time.
		TimeSpan retrieveShare = result.Count > 0 ? stopper.Elapsed / result.Count : TimeSpan.Zero;

		foreach (CustomApiModel api in result)
		{
			_reporter.ItemCompleted(new ItemResult
			{
				Kind = ItemKind.CustomApi,
				Name = api.UniqueName,
				Status = ItemStatus.Ok,
				Detail = $"{api.RequestParameters.Length} parameters · {api.ResponseProperties.Length} response properties",
				Elapsed = retrieveShare
			});
		}

		stopper.Stop();
		_reporter.StageCompleted(Stage.CustomApis, stopper.Elapsed);

		return result.ToArray();
	}

	internal static List<Entity> FilterCustomApiEntities(
		IReadOnlyCollection<Entity> customApiEntities,
		IReadOnlyCollection<string> selectedCustomApis,
		IGenerationReporter reporter = null)
	{
		reporter ??= NullReporter.Instance;

		if (!HasSelection(selectedCustomApis))
		{
			return new List<Entity>();
		}

		List<string> selectedCustomApiNames = new();
		HashSet<string> selectedCustomApiNamesLookup = new(StringComparer.InvariantCultureIgnoreCase);
		foreach (string selectedCustomApi in selectedCustomApis)
		{
			if (string.IsNullOrWhiteSpace(selectedCustomApi))
			{
				continue;
			}

			string normalizedSelectedCustomApi = selectedCustomApi.Trim();
			if (selectedCustomApiNamesLookup.Add(normalizedSelectedCustomApi))
			{
				selectedCustomApiNames.Add(normalizedSelectedCustomApi);
			}
		}

		Dictionary<string, Entity> availableCustomApis = customApiEntities
		   .Where(entity => !string.IsNullOrWhiteSpace(GetString(entity, "uniquename")))
		   .GroupBy(entity => GetString(entity, "uniquename"), StringComparer.InvariantCultureIgnoreCase)
		   .ToDictionary(group => group.Key, group => group.First(), StringComparer.InvariantCultureIgnoreCase);

		List<Entity> filteredCustomApis = new();

		foreach (string selectedCustomApi in selectedCustomApiNames)
		{
			if (!availableCustomApis.TryGetValue(selectedCustomApi, out Entity customApiEntity))
			{
				reporter.ItemCompleted(new ItemResult
				{
					Kind = ItemKind.CustomApi,
					Name = selectedCustomApi,
					Status = ItemStatus.NotFound
				});
				continue;
			}

			filteredCustomApis.Add(customApiEntity);
		}

		return filteredCustomApis;
	}

	private static Dictionary<string, List<CustomApiParameterModel>> GroupParametersByApiId(
		IEnumerable<Entity> entities,
		ISet<string> selectedApiIds,
		bool isResponse)
	{
		Dictionary<string, List<CustomApiParameterModel>> grouped = new(StringComparer.InvariantCultureIgnoreCase);

		foreach (Entity entity in entities)
		{
			string apiId = GetLookupId(entity, "customapiid");

			if (string.IsNullOrWhiteSpace(apiId))
			{
				continue;
			}

			if (selectedApiIds is { Count: > 0 } && !selectedApiIds.Contains(apiId))
			{
				continue;
			}

			CustomApiParameterModel parameter = ParseParameter(entity, isResponse);

			if (!grouped.TryGetValue(apiId, out List<CustomApiParameterModel> parameters))
			{
				parameters = new List<CustomApiParameterModel>();
				grouped[apiId] = parameters;
			}

			parameters.Add(parameter);
		}

		return grouped;
	}

	private async Task<List<Entity>> RetrieveAsync(
		ServiceClientPool pool,
		SemaphoreSlim gate,
		string tableName,
		string description,
		CancellationToken cancellationToken)
	{
		await gate.WaitAsync(cancellationToken);

		try
		{
			QueryExpression query = new(tableName)
			{
				ColumnSet = new ColumnSet(true)
			};

			ServiceClient client = pool.Rent();

			try
			{
				Stopwatch requestTimer = Stopwatch.StartNew();
				EntityCollection response = await client.RetrieveMultipleAsync(query, cancellationToken);
				_reporter.RequestCompleted(Stage.CustomApis, requestTimer.Elapsed);
				_reporter.Log(LogLevel.Debug, $@"Found {description}: {response.Entities.Count}");

				return response.Entities.ToList();
			}
			finally
			{
				pool.Return(client);
			}
		}
		finally
		{
			gate.Release();
		}
	}

	private static CustomApiModel ParseCustomApi(
		Entity entity,
		IReadOnlyCollection<CustomApiParameterModel> requestParameters,
		IReadOnlyCollection<CustomApiParameterModel> responseProperties)
	{
		string id = GetEntityId(entity);
		string displayName = GetString(entity, "name");
		string uniqueName = GetString(entity, "uniquename");
		string classNameSource = string.IsNullOrWhiteSpace(displayName) ? uniqueName : displayName;
		string className = MetadataNamingExtensions.ToPascalCase(classNameSource);
		if (!className.EndsWith("Request", StringComparison.InvariantCultureIgnoreCase))
		{
			className += "Request";
		}

		string responseClassName = className[..^"Request".Length] + "Response";

		return new CustomApiModel
		{
			Id = id,
			UniqueName = uniqueName,
			DisplayName = displayName,
			Description = GetString(entity, "description"),
			OperationName = string.IsNullOrWhiteSpace(uniqueName) ? className : uniqueName,
			BindingType = GetFormattedOrRawValue(entity, "bindingtype"),
			BoundEntityLogicalName = GetString(entity, "boundentitylogicalname"),
			ExecutePrivilegeName = GetString(entity, "executeprivilegename"),
			IsFunction = GetBool(entity, "isfunction"),
			IsPrivate = GetBool(entity, "isprivate"),
			RequestClassName = className,
			ResponseClassName = responseClassName,
			RequestParameters = requestParameters
			   .OrderBy(p => p.Position)
			   .ThenBy(p => p.PropertyName)
			   .ToArray(),
			ResponseProperties = responseProperties
			   .OrderBy(p => p.Position)
			   .ThenBy(p => p.PropertyName)
			   .ToArray()
		};
	}

	internal static CustomApiParameterModel ParseParameter(Entity entity, bool isResponse)
	{
		string displayName = GetString(entity, "name");
		string uniqueName = GetString(entity, "uniquename");
		string sourceName = string.IsNullOrWhiteSpace(uniqueName) ? displayName : uniqueName;
		string propertyName = MetadataNamingExtensions.GetProperVariableName(sourceName);
		string typeLabel = GetFormattedOrRawValue(entity, "type");
		string logicalEntityName = GetString(entity, "logicalentityname");

		CustomApiTypeInfo typeInfo = CustomApiTypeMapper.Resolve(typeLabel, logicalEntityName, isResponse);

		return new CustomApiParameterModel
		{
			Id = GetEntityId(entity),
			CustomApiId = GetLookupId(entity, "customapiid"),
			UniqueName = uniqueName,
			DisplayName = displayName,
			Description = GetString(entity, "description"),
			Type = typeLabel,
			LogicalEntityName = logicalEntityName,
			IsOptional = GetBool(entity, "isoptional"),
			Position = GetInt(entity, "position"),
			PropertyName = propertyName,
			RequestPropertyName = MetadataNamingExtensions.GetProperVariableName(string.IsNullOrWhiteSpace(uniqueName) ? propertyName : uniqueName),
			ConstructorParameterName = MetadataNamingExtensions.GetProperVariableName(string.IsNullOrWhiteSpace(displayName) ? propertyName : displayName),
			TypeScriptType = typeInfo.TypeScriptType,
			CSharpTypeName = typeInfo.CSharpTypeName,
			WebApiTypeName = typeInfo.WebApiTypeName,
			WebApiStructuralProperty = typeInfo.WebApiStructuralProperty
		};
	}

	private static string GetEntityId(Entity entity)
	{
		return entity.Id.ToString("D");
	}

	private static string GetFormattedOrRawValue(Entity entity, string attributeName)
	{
		if (entity.FormattedValues.TryGetValue(attributeName, out string formattedValue) &&
			!string.IsNullOrWhiteSpace(formattedValue))
		{
			return formattedValue;
		}

		object rawValue = entity.Contains(attributeName) ? entity[attributeName] : null;
		return rawValue switch
		{
			OptionSetValue optionSetValue => optionSetValue.Value.ToString(CultureInfo.InvariantCulture),
			Money money => money.Value.ToString(CultureInfo.InvariantCulture),
			_ => rawValue?.ToString() ?? string.Empty
		};
	}

	private static string GetLookupId(Entity entity, string attributeName)
	{
		EntityReference reference = entity.GetAttributeValue<EntityReference>(attributeName);
		return reference?.Id.ToString("D") ?? string.Empty;
	}

	private static bool GetBool(Entity entity, string attributeName)
	{
		object value = entity.Contains(attributeName) ? entity[attributeName] : null;
		return value switch
		{
			bool b => b,
			OptionSetValue optionSetValue => optionSetValue.Value != 0,
			_ => false
		};
	}

	private static int GetInt(Entity entity, string attributeName)
	{
		object value = entity.Contains(attributeName) ? entity[attributeName] : null;
		return value switch
		{
			int i => i,
			OptionSetValue optionSetValue => optionSetValue.Value,
			decimal decimalValue => (int)decimalValue,
			double doubleValue => (int)doubleValue,
			_ => 0
		};
	}

	private static string GetString(Entity entity, string attributeName)
	{
		return entity.GetAttributeValue<string>(attributeName) ?? string.Empty;
	}
}
