using System.Diagnostics;
using System.Security;
using System.Text;
using DataverseGen.Core.CustomApi;
using DataverseGen.Core.Extensions;
using DataverseGen.Core.Generators.Scriban.Templates.TemplateFileManager;
using DataverseGen.Core.Reporting;
using Scriban;

namespace DataverseGen.Core.Generators;

public static class CustomApiCSharpGenerator
{
	private static readonly HashSet<string> ValueTypeNames = ["bool", "int", "long", "decimal", "double", "float", "DateTime", "Guid"];

	public static void WriteCustomApis(string outPath, string namespaceName, IEnumerable<CustomApiModel> customApis)
	{
		WriteCustomApis(outPath, namespaceName, customApis, CustomApiTemplates.LoadBuiltIn(false));
	}

	public static void WriteCustomApis(
		string outPath,
		string namespaceName,
		IEnumerable<CustomApiModel> customApis,
		Template template,
		Template responseTemplate = null,
		IGenerationReporter reporter = null,
		int degreeOfParallelism = 1,
		CancellationToken cancellationToken = default,
		Action<string> onFileWritten = null)
	{
		if (customApis == null)
		{
			return;
		}

		reporter ??= NullReporter.Instance;

		string customApiDirectory = outPath;
		Directory.CreateDirectory(customApiDirectory);

		CustomApiModel[] apis = customApis.ToArray();
		bool anyResponse = apis.Any(api => api.ResponseProperties is { Length: > 0 });
		responseTemplate ??= anyResponse ? CustomApiTemplates.LoadBuiltInResponse(false) : null;

		Parallel.ForEach(apis,
			new ParallelOptions
			{
				MaxDegreeOfParallelism = Math.Max(1, degreeOfParallelism),
				CancellationToken = cancellationToken
			},
			customApi =>
		{
			reporter.ItemStarted(ItemKind.CustomApi, ItemPhase.Generate, customApi.UniqueName);
			Stopwatch timer = Stopwatch.StartNew();
			int files = 1;
			string filePath = Path.Combine(customApiDirectory, $"{customApi.RequestClassName.ToLowerInvariant()}.customapi.cs");
			File.WriteAllText(filePath, RenderRequestClass(namespaceName, customApi, template), Encoding.UTF8);
			onFileWritten?.Invoke(filePath);
			onFileWritten?.Invoke(filePath);
			long bytes = new FileInfo(filePath).Length;
			reporter.Log(LogLevel.Debug, $@"Generated custom API: {customApi.UniqueName} -> {filePath}");

			if (customApi.ResponseProperties is { Length: > 0 })
			{
				string responsePath = Path.Combine(customApiDirectory, $"{customApi.ResponseClassName.ToLowerInvariant()}.customapi.cs");
				File.WriteAllText(responsePath, RenderResponseClass(namespaceName, customApi, responseTemplate), Encoding.UTF8);
				onFileWritten?.Invoke(responsePath);
				onFileWritten?.Invoke(responsePath);
				files++;
				bytes += new FileInfo(responsePath).Length;
				reporter.Log(LogLevel.Debug, $@"Generated custom API response: {customApi.UniqueName} -> {responsePath}");
			}

			reporter.ItemCompleted(new ItemResult
			{
				Kind = ItemKind.CustomApi,
				Phase = ItemPhase.Generate,
				Name = customApi.UniqueName,
				Status = ItemStatus.Ok,
				Elapsed = timer.Elapsed,
				Files = files,
				Bytes = bytes
			});
		});
	}

	internal static string RenderResponseClass(string namespaceName, CustomApiModel customApi)
	{
		return RenderResponseClass(namespaceName, customApi, CustomApiTemplates.LoadBuiltInResponse(false));
	}

	internal static string RenderResponseClass(string namespaceName, CustomApiModel customApi, Template template)
	{
		return CustomApiTemplates.Render(template, BuildResponseView(namespaceName, customApi));
	}

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
			IsOptional = parameter.IsOptional,
			Summary = GetSummary(parameter),
			PropertyName = GetPropertyName(parameter),
			ParameterName = GetParameterName(parameter),
			RequestPropertyName = EscapeString(parameter.RequestPropertyName)
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
		string typeName = string.IsNullOrWhiteSpace(parameter.CSharpTypeName)
			? "string"
			: parameter.CSharpTypeName;

		return parameter.IsOptional && ValueTypeNames.Contains(typeName)
			? $"{typeName}?"
			: typeName;
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
