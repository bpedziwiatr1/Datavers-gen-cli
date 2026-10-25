using DataverseGen.Core.CustomApi;

namespace DataverseGen.Core.Generators.Views;

internal static class CustomApiTypeScriptViewBuilder
{
	internal static CustomApiTypeScriptResponseView BuildResponseView(CustomApiModel customApi)
	{
		return new CustomApiTypeScriptResponseView
		{
			ClassName = customApi.ResponseClassName,
			Properties = customApi.ResponseProperties
			   .Select(p => ToView(p, false))
			   .ToList()
		};
	}

	internal static CustomApiTypeScriptView BuildView(CustomApiModel customApi)
	{
		List<CustomApiTypeScriptParameterView> constructorParameters = customApi.RequestParameters
		   .Select(p => ToView(p, false))
		   .ToList();
		List<CustomApiTypeScriptParameterView> properties = [.. constructorParameters];
		CustomApiParameterModel boundParameter = GetBoundParameter(customApi);

		if (boundParameter != null)
		{
			CustomApiTypeScriptParameterView bound = ToView(boundParameter, true);
			constructorParameters.Add(bound);
			properties.Insert(0, bound);
		}

		return new CustomApiTypeScriptView
		{
			ClassName = customApi.RequestClassName,
			OperationName = CustomApiText.EscapeString(customApi.OperationName),
			BoundParameterName = GetBoundParameterName(boundParameter),
			OperationType = GetOperationType(customApi),
			ConstructorParameters = constructorParameters,
			Properties = properties
		};
	}

	private static CustomApiTypeScriptParameterView ToView(CustomApiParameterModel parameter, bool isBound)
	{
		return new CustomApiTypeScriptParameterView
		{
			IsBound = isBound,
			RequestPropertyName = parameter.RequestPropertyName,
			ConstructorParameterName = parameter.ConstructorParameterName,
			TypeScriptType = parameter.TypeScriptType,
			WebApiTypeName = parameter.WebApiTypeName,
			WebApiStructuralProperty = parameter.WebApiStructuralProperty
		};
	}

	private static CustomApiParameterModel GetBoundParameter(CustomApiModel customApi)
	{
		if (string.IsNullOrWhiteSpace(customApi.BoundEntityLogicalName))
		{
			return null;
		}

		return new CustomApiParameterModel
		{
			PropertyName = "entity",
			RequestPropertyName = "entity",
			ConstructorParameterName = "entity",
			TypeScriptType = "any",
			WebApiTypeName = $"mscrm.{customApi.BoundEntityLogicalName}",
			WebApiStructuralProperty = "WebApiRequestStructuralProperty.EntityType"
		};
	}

	private static string GetBoundParameterName(CustomApiParameterModel boundParameter)
	{
		return boundParameter == null
			? "null"
			: $"\"{CustomApiText.EscapeString(boundParameter.RequestPropertyName)}\"";
	}

	private static string GetOperationType(CustomApiModel customApi)
	{
		return customApi.IsFunction
			? "WebApiRequestOperationType.Function"
			: "WebApiRequestOperationType.Action";
	}
}
