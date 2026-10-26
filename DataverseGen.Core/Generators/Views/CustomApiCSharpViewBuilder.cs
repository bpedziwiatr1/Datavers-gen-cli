using System.Security;
using System.Text;
using DataverseGen.Core.CustomApi;
using DataverseGen.Core.Extensions;

namespace DataverseGen.Core.Generators.Views;

internal static class CustomApiCSharpViewBuilder
{
	private static readonly HashSet<string> ValueTypeNames = ["bool", "int", "long", "decimal", "double", "float", "DateTime", "Guid"];

	internal static CustomApiCSharpResponseView BuildResponseView(string namespaceName, CustomApiModel customApi)
	{
		return new CustomApiCSharpResponseView
		{
			Namespace = namespaceName,
			ClassName = customApi.ResponseClassName,
			Properties = customApi.ResponseProperties
			   .Select(p =>
				{
					CustomApiCSharpParameterView view = ToView(p);
					view.Summary = GetResponseSummary(p);
					return view;
				})
			   .ToList()
		};
	}

	private static string GetResponseSummary(CustomApiParameterModel parameter)
	{
		string description = string.Join(" ",
			(parameter.Description ?? string.Empty).Split((char[])null, StringSplitOptions.RemoveEmptyEntries));

		return description.Length == 0
			? SecurityElement.Escape(parameter.DisplayName ?? parameter.UniqueName ?? string.Empty)
			: SecurityElement.Escape(description);
	}

	internal static CustomApiCSharpView BuildView(string namespaceName, CustomApiModel customApi)
	{
		List<CustomApiCSharpParameterView> requestParameters = customApi.RequestParameters
		   .Select(ToView)
		   .ToList();
		List<CustomApiCSharpParameterView> properties = [.. requestParameters];
		List<CustomApiCSharpParameterView> constructorParameters = requestParameters
		   .Where(p => !p.IsOptional)
		   .ToList();
		CustomApiParameterModel boundParameter = GetBoundParameter(customApi);

		if (boundParameter != null)
		{
			CustomApiCSharpParameterView bound = ToView(boundParameter);
			constructorParameters.Add(bound);
			properties.Insert(0, bound);
		}

		constructorParameters.AddRange(requestParameters.Where(p => p.IsOptional));

		return new CustomApiCSharpView
		{
			Namespace = namespaceName,
			ClassName = customApi.RequestClassName,
			OperationName = CustomApiText.EscapeString(customApi.OperationName),
			ConstructorParameters = constructorParameters,
			Properties = properties
		};
	}

	private static CustomApiCSharpParameterView ToView(CustomApiParameterModel parameter)
	{
		return new CustomApiCSharpParameterView
		{
			Type = GetCSharpType(parameter),
			IsOptional = parameter.IsOptional,
			Summary = GetSummary(parameter),
			PropertyName = GetPropertyName(parameter),
			ParameterName = GetParameterName(parameter),
			RequestPropertyName = CustomApiText.EscapeString(parameter.RequestPropertyName)
		};
	}

	private static string GetSummary(CustomApiParameterModel parameter)
	{
		string status = parameter.IsOptional ? "Optional." : "Required.";
		string description = string.Join(" ",
			(parameter.Description ?? string.Empty).Split((char[])null, StringSplitOptions.RemoveEmptyEntries));

		return description.Length == 0
			? status
			: $"{status} {SecurityElement.Escape(description)}";
	}

	private static CustomApiParameterModel GetBoundParameter(CustomApiModel customApi)
	{
		if (string.IsNullOrWhiteSpace(customApi.BoundEntityLogicalName))
		{
			return null;
		}

		return new CustomApiParameterModel
		{
			DisplayName = "Entity",
			UniqueName = "entity",
			PropertyName = "entity",
			RequestPropertyName = "entity",
			ConstructorParameterName = "entity",
			TypeScriptType = "any",
			CSharpTypeName = "EntityReference",
			WebApiTypeName = $"mscrm.{customApi.BoundEntityLogicalName}",
			WebApiStructuralProperty = "WebApiRequestStructuralProperty.EntityType"
		};
	}

	private static string GetPropertyName(CustomApiParameterModel parameter)
	{
		string source = string.IsNullOrWhiteSpace(parameter.DisplayName)
			? parameter.UniqueName
			: parameter.DisplayName;

		string sanitized = MetadataNamingExtensions.GetProperVariableName(source);
		if (sanitized.StartsWith("_", StringComparison.InvariantCulture) &&
			sanitized.Length > 1 &&
			char.IsDigit(sanitized[1]))
		{
			return sanitized;
		}

		return MetadataNamingExtensions.ToPascalCase(sanitized);
	}

	private static string GetParameterName(CustomApiParameterModel parameter)
	{
		string propertyName = GetPropertyName(parameter);

		if (string.IsNullOrWhiteSpace(propertyName))
		{
			return "value";
		}

		string parameterName = char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
		return IsCSharpKeyword(parameterName)
			? $"@{parameterName}"
			: parameterName;
	}

	private static string GetCSharpType(CustomApiParameterModel parameter)
	{
		string typeName = string.IsNullOrWhiteSpace(parameter.CSharpTypeName)
			? "string"
			: parameter.CSharpTypeName;

		return parameter.IsOptional && ValueTypeNames.Contains(typeName)
			? $"{typeName}?"
			: typeName;
	}

	private static bool IsCSharpKeyword(string value)
	{
		return value.Equals("public", StringComparison.InvariantCultureIgnoreCase)
			|| value.Equals("private", StringComparison.InvariantCultureIgnoreCase)
			|| value.Equals("single", StringComparison.InvariantCultureIgnoreCase)
			|| value.Equals("new", StringComparison.InvariantCultureIgnoreCase)
			|| value.Equals("partial", StringComparison.InvariantCultureIgnoreCase)
			|| value.Equals("to", StringComparison.InvariantCultureIgnoreCase)
			|| value.Equals("error", StringComparison.InvariantCultureIgnoreCase)
			|| value.Equals("readonly", StringComparison.InvariantCultureIgnoreCase)
			|| value.Equals("case", StringComparison.InvariantCultureIgnoreCase)
			|| value.Equals("object", StringComparison.InvariantCultureIgnoreCase)
			|| value.Equals("global", StringComparison.InvariantCultureIgnoreCase)
			|| value.Equals("true", StringComparison.InvariantCultureIgnoreCase)
			|| value.Equals("false", StringComparison.InvariantCultureIgnoreCase);
	}
}
