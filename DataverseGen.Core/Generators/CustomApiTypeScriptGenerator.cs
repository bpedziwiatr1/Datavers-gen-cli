using System.Text;
using DataverseGen.Core.CustomApi;
using DataverseGen.Core.Generators.Scriban.Templates.TemplateFileManager;
using Scriban;

namespace DataverseGen.Core.Generators;

public static class CustomApiTypeScriptGenerator
{
	public static void WriteCustomApis(string outPath, IEnumerable<CustomApiModel> customApis)
	{
		WriteCustomApis(outPath, customApis, CustomApiTemplates.LoadBuiltIn(true));
	}

	public static void WriteCustomApis(string outPath, IEnumerable<CustomApiModel> customApis, Template template)
	{
		if (customApis == null)
		{
			return;
		}

		string customApiDirectory = outPath;
		Directory.CreateDirectory(customApiDirectory);

		foreach (CustomApiModel customApi in customApis)
		{
			string filePath = Path.Combine(customApiDirectory, $"{customApi.RequestClassName.ToLowerInvariant()}.customapi.ts");
			File.WriteAllText(filePath, RenderRequestClass(customApi, template), Encoding.UTF8);
			Console.WriteLine($@"Generated custom API: {customApi.UniqueName} -> {filePath}");
		}
	}

	internal static string RenderRequestClass(CustomApiModel customApi)
	{
		return RenderRequestClass(customApi, CustomApiTemplates.LoadBuiltIn(true));
	}

	internal static string RenderRequestClass(CustomApiModel customApi, Template template)
	{
		return CustomApiTemplates.Render(template, BuildView(customApi));
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
			OperationName = EscapeString(customApi.OperationName),
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
			: $"\"{EscapeString(boundParameter.RequestPropertyName)}\"";
	}

	private static string GetOperationType(CustomApiModel customApi)
	{
		return customApi.IsFunction
			? "WebApiRequestOperationType.Function"
			: "WebApiRequestOperationType.Action";
	}

	private static string EscapeString(string value)
	{
		return string.IsNullOrEmpty(value)
			? string.Empty
			: value.Replace("\\", "\\\\").Replace("\"", "\\\"");
	}
}
