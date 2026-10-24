using System.Runtime.CompilerServices;
using DataverseGen.Core.CustomApi;
using DataverseGen.Core.Generators;

namespace DataverseGen.Core.Tests.Generators;

/// <summary>
///  Characterization tests for the custom API request wrappers. The output is compared with baseline files in
///  Generators/Baselines. To regenerate them after an intended change, run the tests with
///  DATAVERSEGEN_UPDATE_BASELINES=1 and review the diff.
/// </summary>
[TestClass]
public class CustomApiBaselineTests
{
	private const string UpdateBaselinesVariable = "DATAVERSEGEN_UPDATE_BASELINES";
	private const string Namespace = "MyApp.CustomApis";

	[TestMethod]
	public void RenderRequestClass_CSharp_MatchesBaseline()
	{
		AssertMatchesBaseline("customapi-csharp",
			"cs",
			model => CustomApiCSharpGenerator.RenderRequestClass(Namespace, model));
	}

	[TestMethod]
	public void RenderRequestClass_TypeScript_MatchesBaseline()
	{
		AssertMatchesBaseline("customapi-ts", "ts", CustomApiTypeScriptGenerator.RenderRequestClass);
	}

	private static void AssertMatchesBaseline(string scenario, string extension, Func<CustomApiModel, string> render)
	{
		Dictionary<string, string> actual = BuildModels()
		   .ToDictionary(m => $"{m.RequestClassName.ToLowerInvariant()}.{extension}", render);
		string baselineDirectory = Path.Combine(GetSourceDirectory(), "Baselines", scenario);

		if (Environment.GetEnvironmentVariable(UpdateBaselinesVariable) == "1")
		{
			if (Directory.Exists(baselineDirectory))
			{
				Directory.Delete(baselineDirectory, true);
			}

			Directory.CreateDirectory(baselineDirectory);

			foreach ((string fileName, string content) in actual)
			{
				File.WriteAllText(Path.Combine(baselineDirectory, fileName), content);
			}

			Assert.Inconclusive($"Baselines for '{scenario}' were updated.");
		}

		Assert.IsTrue(Directory.Exists(baselineDirectory),
			$"Missing baselines in {baselineDirectory}. Run tests with {UpdateBaselinesVariable}=1.");

		string[] expectedFiles = Directory.GetFiles(baselineDirectory)
		   .Select(f => Path.GetFileName(f))
		   .OrderBy(f => f, StringComparer.Ordinal)
		   .ToArray();

		CollectionAssert.AreEqual(expectedFiles, actual.Keys.OrderBy(f => f, StringComparer.Ordinal).ToArray());

		foreach (string fileName in expectedFiles)
		{
			string expected = File.ReadAllText(Path.Combine(baselineDirectory, fileName));
			Assert.AreEqual(expected, actual[fileName], $"Generated file '{fileName}' differs from baseline.");
		}
	}

	private static string GetSourceDirectory([CallerFilePath] string path = "")
	{
		return Path.GetDirectoryName(path)!;
	}

	private static CustomApiModel[] BuildModels()
	{
		return
		[
			new CustomApiModel
			{
				RequestClassName = "PingRequest",
				OperationName = "newping"
			},
			new CustomApiModel
			{
				RequestClassName = "GetStatusRequest",
				OperationName = "newgetstatus",
				IsFunction = true,
				RequestParameters =
				[
					Parameter("Customer Id", "customerid", "string", "string", "Edm.String", "WebApiRequestStructuralProperty.PrimitiveType")
				]
			},
			new CustomApiModel
			{
				RequestClassName = "ProcessStatusRequest",
				OperationName = "new\"update\\status",
				BoundEntityLogicalName = "account",
				RequestParameters =
				[
					Parameter("Flag", "flag", "bool", "boolean", "Edm.Boolean", "WebApiRequestStructuralProperty.PrimitiveType"),
					Parameter("Count", "count", "int", "number", "Edm.Int32", "WebApiRequestStructuralProperty.PrimitiveType"),
					Parameter("Names", "new_names", "string[]", "string[]", "Collection(Edm.String)", "WebApiRequestStructuralProperty.Collection")
				]
			},
			new CustomApiModel
			{
				RequestClassName = "KeywordRequest",
				OperationName = "newkeyword",
				RequestParameters =
				[
					Parameter("Private", "private", "string", "string", "Edm.String", "WebApiRequestStructuralProperty.PrimitiveType")
				]
			}
		];
	}

	private static CustomApiParameterModel Parameter(
		string displayName,
		string uniqueName,
		string csharpType,
		string typeScriptType,
		string webApiTypeName,
		string structuralProperty)
	{
		return new CustomApiParameterModel
		{
			DisplayName = displayName,
			UniqueName = uniqueName,
			PropertyName = uniqueName,
			RequestPropertyName = uniqueName,
			ConstructorParameterName = uniqueName,
			CSharpTypeName = csharpType,
			TypeScriptType = typeScriptType,
			WebApiTypeName = webApiTypeName,
			WebApiStructuralProperty = structuralProperty
		};
	}
}
