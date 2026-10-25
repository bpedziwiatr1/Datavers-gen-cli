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
		string className = ToPascalCase(classNameSource);
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

		string tsType;
		string cSharpTypeName;
		string webApiTypeName;
		string structuralProperty;

		if (!isResponse ||
			!TryNormalizeCustomApiResponseType(typeLabel, out tsType, out cSharpTypeName, out webApiTypeName, out structuralProperty))
		{
			NormalizeCustomApiParameterType(typeLabel,
				logicalEntityName,
				out tsType,
				out cSharpTypeName,
				out webApiTypeName,
				out structuralProperty);
		}

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
			TypeScriptType = tsType,
			CSharpTypeName = cSharpTypeName,
			WebApiTypeName = webApiTypeName,
			WebApiStructuralProperty = structuralProperty
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

	/// <summary>
	///  Maps the exact Dataverse response property types that the request mapping gets wrong (it matches by
	///  substring, so <c>EntityCollection</c> and <c>EntityReference</c> end up as collection / entity reference).
	///  Returns false for types the request mapping already handles.
	/// </summary>
	private static bool TryNormalizeCustomApiResponseType(
		string typeLabel,
		out string typeScriptType,
		out string cSharpTypeName,
		out string webApiTypeName,
		out string structuralProperty)
	{
		structuralProperty = "WebApiRequestStructuralProperty.PrimitiveType";

		switch ((typeLabel ?? string.Empty).Trim().ToLowerInvariant())
		{
			case "entity":
				typeScriptType = "any";
				cSharpTypeName = "Entity";
				webApiTypeName = "mscrm.crmbaseentity";
				structuralProperty = "WebApiRequestStructuralProperty.EntityType";
				return true;
			case "entitycollection":
				typeScriptType = "any[]";
				cSharpTypeName = "EntityCollection";
				webApiTypeName = "Collection(mscrm.crmbaseentity)";
				structuralProperty = "WebApiRequestStructuralProperty.Collection";
				return true;
			case "entityreference":
				typeScriptType = "any";
				cSharpTypeName = "EntityReference";
				webApiTypeName = "mscrm.crmbaseentity";
				structuralProperty = "WebApiRequestStructuralProperty.EntityType";
				return true;
			case "stringarray":
				typeScriptType = "string[]";
				cSharpTypeName = "string[]";
				webApiTypeName = "Collection(Edm.String)";
				structuralProperty = "WebApiRequestStructuralProperty.Collection";
				return true;
			case "money":
				typeScriptType = "number";
				cSharpTypeName = "Money";
				webApiTypeName = "Edm.Decimal";
				return true;
			case "picklist":
			case "integer":
				typeScriptType = "number";
				cSharpTypeName = "int";
				webApiTypeName = "Edm.Int32";
				return true;
			case "float":
				typeScriptType = "number";
				cSharpTypeName = "double";
				webApiTypeName = "Edm.Double";
				return true;
			default:
				typeScriptType = null;
				cSharpTypeName = null;
				webApiTypeName = null;
				return false;
		}
	}

	private static void NormalizeCustomApiParameterType(
		string typeLabel,
		string logicalEntityName,
		out string typeScriptType,
		out string cSharpTypeName,
		out string webApiTypeName,
		out string structuralProperty)
	{
		string normalizedType = (typeLabel ?? string.Empty).Trim().ToLowerInvariant();

		if (string.IsNullOrWhiteSpace(normalizedType))
		{
			typeScriptType = "string";
			cSharpTypeName = "string";
			webApiTypeName = "Edm.String";
			structuralProperty = "WebApiRequestStructuralProperty.PrimitiveType";
			return;
		}

		if (normalizedType.Contains("collection"))
		{
			typeScriptType = "string[]";
			cSharpTypeName = string.IsNullOrWhiteSpace(logicalEntityName)
				? "string[]"
				: "EntityReference[]";
			webApiTypeName = string.IsNullOrWhiteSpace(logicalEntityName)
				? "Collection(Edm.String)"
				: $"Collection(mscrm.{logicalEntityName})";
			structuralProperty = "WebApiRequestStructuralProperty.Collection";
			return;
		}

		if (normalizedType.Contains("entity"))
		{
			typeScriptType = "any";
			cSharpTypeName = "EntityReference";
			webApiTypeName = string.IsNullOrWhiteSpace(logicalEntityName)
				? "mscrm.crmbaseentity"
				: $"mscrm.{logicalEntityName}";
			structuralProperty = "WebApiRequestStructuralProperty.EntityType";
			return;
		}

		if (normalizedType.Contains("bool"))
		{
			typeScriptType = "boolean";
			cSharpTypeName = "bool";
			webApiTypeName = "Edm.Boolean";
			structuralProperty = "WebApiRequestStructuralProperty.PrimitiveType";
			return;
		}

		if (normalizedType.Contains("int") || normalizedType.Contains("whole"))
		{
			typeScriptType = "number";
			cSharpTypeName = "int";
			webApiTypeName = "Edm.Int32";
			structuralProperty = "WebApiRequestStructuralProperty.PrimitiveType";
			return;
		}

		if (normalizedType.Contains("decimal") || normalizedType.Contains("double") || normalizedType.Contains("money") || normalizedType.Contains("float"))
		{
			typeScriptType = "number";
			cSharpTypeName = "decimal";
			webApiTypeName = "Edm.Decimal";
			structuralProperty = "WebApiRequestStructuralProperty.PrimitiveType";
			return;
		}

		if (normalizedType.Contains("date"))
		{
			typeScriptType = "Date";
			cSharpTypeName = "DateTime";
			webApiTypeName = "Edm.DateTimeOffset";
			structuralProperty = "WebApiRequestStructuralProperty.PrimitiveType";
			return;
		}

		if (normalizedType.Contains("guid") || normalizedType.Contains("unique"))
		{
			typeScriptType = "string";
			cSharpTypeName = "Guid";
			webApiTypeName = "Edm.Guid";
			structuralProperty = "WebApiRequestStructuralProperty.PrimitiveType";
			return;
		}

		typeScriptType = "string";
		cSharpTypeName = "string";
		webApiTypeName = "Edm.String";
		structuralProperty = "WebApiRequestStructuralProperty.PrimitiveType";
	}

	private static string ToPascalCase(string value, bool capitalizeFirstWord = true)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}

		char[] separators = [' ', '_', '-', '.', '/', '\\'];
		string[] parts = value.Split(separators, StringSplitOptions.RemoveEmptyEntries);
		StringBuilder builder = new();

		for (int i = 0; i < parts.Length; i++)
		{
			string part = parts[i].Trim();

			if (part.Length == 0)
			{
				continue;
			}

			if (i == 0 && !capitalizeFirstWord)
			{
				builder.Append(char.ToLowerInvariant(part[0]));
				if (part.Length > 1)
				{
					builder.Append(part[1..]);
				}
			}
			else
			{
				builder.Append(char.ToUpperInvariant(part[0]));
				if (part.Length > 1)
				{
					builder.Append(part[1..]);
				}
			}
		}

		return builder.ToString();
	}
}
