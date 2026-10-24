using System.Text;
using DataverseGen.Core.CustomApi;
using DataverseGen.Core.Extensions;
using DataverseGen.Core.Generators.Scriban.Templates.TemplateFileManager;
using Scriban;

namespace DataverseGen.Core.Generators;

public static class CustomApiCSharpGenerator
{
	public static void WriteCustomApis(string outPath, string namespaceName, IEnumerable<CustomApiModel> customApis)
	{
		WriteCustomApis(outPath, namespaceName, customApis, CustomApiTemplates.LoadBuiltIn(false));
	}

	public static void WriteCustomApis(
		string outPath,
		string namespaceName,
		IEnumerable<CustomApiModel> customApis,
		Template template)
	{
		if (customApis == null)
		{
			return;
		}

		string customApiDirectory = outPath;
		Directory.CreateDirectory(customApiDirectory);

		foreach (CustomApiModel customApi in customApis)
		{
			string filePath = Path.Combine(customApiDirectory, $"{customApi.RequestClassName.ToLowerInvariant()}.customapi.cs");
			File.WriteAllText(filePath, RenderRequestClass(namespaceName, customApi, template), Encoding.UTF8);
			Console.WriteLine($@"Generated custom API: {customApi.UniqueName} -> {filePath}");
		}
	}

	internal static string RenderRequestClass(string namespaceName, CustomApiModel customApi)
	{
		return RenderRequestClass(namespaceName, customApi, CustomApiTemplates.LoadBuiltIn(false));
	}

	internal static string RenderRequestClass(string namespaceName, CustomApiModel customApi, Template template)
	{
		return CustomApiTemplates.Render(template, BuildView(namespaceName, customApi));
	}

	internal static CustomApiCSharpView BuildView(string namespaceName, CustomApiModel customApi)
	{
		List<CustomApiCSharpParameterView> constructorParameters = customApi.RequestParameters
		   .Select(ToView)
		   .ToList();
		List<CustomApiCSharpParameterView> properties = [.. constructorParameters];
		CustomApiParameterModel boundParameter = GetBoundParameter(customApi);

		if (boundParameter != null)
		{
			CustomApiCSharpParameterView bound = ToView(boundParameter);
			constructorParameters.Add(bound);
			properties.Insert(0, bound);
		}

		return new CustomApiCSharpView
		{
			Namespace = namespaceName,
			ClassName = customApi.RequestClassName,
			OperationName = EscapeString(customApi.OperationName),
			ConstructorParameters = constructorParameters,
			Properties = properties
		};
	}

	private static CustomApiCSharpParameterView ToView(CustomApiParameterModel parameter)
	{
		return new CustomApiCSharpParameterView
		{
			Type = GetCSharpType(parameter),
			PropertyName = GetPropertyName(parameter),
			ParameterName = GetParameterName(parameter),
			RequestPropertyName = EscapeString(parameter.RequestPropertyName)
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

		return ToPascalCase(sanitized);
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
		return string.IsNullOrWhiteSpace(parameter.CSharpTypeName)
			? "string"
			: parameter.CSharpTypeName;
	}

	private static string EscapeString(string value)
	{
		return string.IsNullOrEmpty(value)
			? string.Empty
			: value.Replace("\\", "\\\\").Replace("\"", "\\\"");
	}

	private static string ToPascalCase(string value)
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

			builder.Append(char.ToUpperInvariant(part[0]));
			if (part.Length > 1)
			{
				builder.Append(part[1..]);
			}
		}

		return builder.ToString();
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
