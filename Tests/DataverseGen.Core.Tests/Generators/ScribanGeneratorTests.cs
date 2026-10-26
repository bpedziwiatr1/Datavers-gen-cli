using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DataverseGen.Core.Config;
using DataverseGen.Core.Generators.Scriban;
using DataverseGen.Core.Metadata;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;

namespace DataverseGen.Core.Tests.Generators;

/// <summary>
///  Characterization tests for the built-in Scriban templates. The generated output is compared with
///  baseline files in Generators/Baselines. To regenerate the baselines after an intended template change,
///  run the tests with the environment variable DATAVERSEGEN_UPDATE_BASELINES=1 and review the diff.
/// </summary>
[TestClass]
public class ScribanGeneratorTests
{
	private const string UpdateBaselinesVariable = "DATAVERSEGEN_UPDATE_BASELINES";

	[TestMethod]
	public void GenerateTemplate_CSharpSingle_MatchesBaseline()
	{
		AssertMatchesBaseline("csharp-single", "C#", true);
	}

	[TestMethod]
	public void GenerateTemplate_CSharpMultiple_MatchesBaseline()
	{
		AssertMatchesBaseline("csharp-multiple", "C#", false);
	}

	[TestMethod]
	public void GenerateTemplate_TypeScriptSingle_MatchesBaseline()
	{
		AssertMatchesBaseline("ts-single", "ts", true);
	}

	[TestMethod]
	public void GenerateTemplate_TypeScriptMultiple_MatchesBaseline()
	{
		AssertMatchesBaseline("ts-multiple", "ts", false);
	}

	[TestMethod]
	public void GenerateTemplate_CSharpMultiple_UsesEntitySuffixByDefault()
	{
		CollectionAssert.AreEquivalent(
			new[] { "new_project.entity.cs", "new_project.Fields.cs", "new_project.Enums.cs", "XrmServiceContext.cs" },
			Generate("C#", false).Keys.ToArray());
	}

	[TestMethod]
	public void GenerateTemplate_CSharpMultiple_LegacyFileNames_KeepsPlainEntityFile()
	{
		CollectionAssert.AreEquivalent(
			new[] { "new_project.cs", "new_project.Fields.cs", "new_project.Enums.cs", "XrmServiceContext.cs" },
			Generate("C#", false, true).Keys.ToArray());
	}

	[TestMethod]
	public void GenerateTemplate_TypeScriptMultiple_UsesEntitySuffixByDefault()
	{
		CollectionAssert.AreEquivalent(
			new[] { "new_project.entity.ts", "new_project.enums.ts", "new_project.relationships.ts" },
			Generate("ts", false).Keys.ToArray());
	}

	[TestMethod]
	public void GenerateTemplate_TypeScriptMultiple_LegacyFileNames_KeepsAttributesFile()
	{
		CollectionAssert.AreEquivalent(
			new[] { "new_project.attributes.ts", "new_project.enums.ts", "new_project.relationships.ts" },
			Generate("ts", false, true).Keys.ToArray());
	}

	[TestMethod]
	public void GenerateTemplate_ClearOutputDirectory_RemovesOnlyStaleGeneratedFiles()
	{
		Dictionary<string, bool> existsAfter = GenerateOverExistingFiles(true);

		Assert.IsFalse(existsAfter["old.entity.cs"], "stale generated file should be removed");
		Assert.IsFalse(existsAfter["nested/old.Enums.cs"], "stale generated file in a subfolder should be removed");
		Assert.IsTrue(existsAfter["mine.cs"], "hand-written file must be kept");
		Assert.IsTrue(existsAfter["notes.txt"], "files of other types must be kept");
		Assert.IsTrue(existsAfter["new_project.entity.cs"], "current file must be kept");
	}

	[TestMethod]
	public void GenerateTemplate_ClearOutputDirectoryDisabled_KeepsStaleFiles()
	{
		Dictionary<string, bool> existsAfter = GenerateOverExistingFiles(false);

		Assert.IsTrue(existsAfter["old.entity.cs"]);
		Assert.IsTrue(existsAfter["nested/old.Enums.cs"]);
		Assert.IsTrue(existsAfter["mine.cs"]);
	}

	[TestMethod]
	public void GenerateTemplate_CSharp_EnumsHaveAttributesAndSummaries()
	{
		string enums = Generate("C#", false)["new_project.Enums.cs"];

		StringAssert.Contains(enums, "using System.Runtime.Serialization;");
		StringAssert.Contains(enums, "[DataContract]");
		StringAssert.Contains(enums, "[System.CodeDom.Compiler.GeneratedCode(\"DataverseGen-Cli\"");
		StringAssert.Contains(enums, "[EnumMember]");
		StringAssert.Contains(enums, "/// Category description");
		StringAssert.Contains(enums, "/// Project done for a customer");
	}

	[TestMethod]
	public void GenerateTemplate_CSharp_EnumMembersHaveDescriptionWithLabel()
	{
		string enums = Generate("C#", false)["new_project.Enums.cs"];

		StringAssert.Contains(enums, "using System.ComponentModel;");
		StringAssert.Contains(enums, "[Description(\"Internal\")]");
		StringAssert.Contains(enums, "[Description(\"Customer\")]");
	}

	[TestMethod]
	public void GenerateTemplate_TypeScriptMultiple_PreserveOptionSetLabels_RendersEnumKeys()
	{
		string enums = Generate("ts", false, preserveOptionSetLabels: true)["new_project.enums.ts"];

		StringAssert.Contains(enums, "Internal = 100000000,");
		StringAssert.Contains(enums, "Customer = 100000001,");
	}

	[TestMethod]
	public void GenerateTemplate_TypeScriptMultiple_PreserveOptionSetLabels_QuotesNonAsciiLabels()
	{
		string enums = Generate("ts", false, preserveOptionSetLabels: true, source: BuildProjectEntityWithInternationalLabels())["new_project.enums.ts"];

		StringAssert.Contains(enums, "\"Potwierdź Zamówienie\" = 1,");
		StringAssert.Contains(enums, "\"Zażółć gęślą jaźń\" = 2,");
		StringAssert.Contains(enums, "\"Größe & Maße\" = 3,");
		StringAssert.Contains(enums, "\"Überfällig\" = 4,");
		StringAssert.Contains(enums, "\"Café Crème\" = 5,");
		StringAssert.Contains(enums, "\"Привіт\" = 6,");
		StringAssert.Contains(enums, "\"注文 確認\" = 7,");
	}

	[TestMethod]
	public void GenerateTemplate_TypeScriptMultiple_PreserveOptionSetLabelsOff_KeepsCleanedKeys()
	{
		string enums = Generate("ts", false, source: BuildProjectEntityWithInternationalLabels())["new_project.enums.ts"];

		StringAssert.Contains(enums, "PotwierdzZamowienie = 1,");
		Assert.IsFalse(enums.Contains("\"Potwierdź Zamówienie\""));
	}

	[TestMethod]
	public void GenerateTemplate_TypeScriptSingle_PreserveOptionSetLabels_QuotesNonAsciiLabels()
	{
		string metadata = Generate("ts", true, preserveOptionSetLabels: true, source: BuildProjectEntityWithInternationalLabels())["dataverse.metadata.ts"];

		StringAssert.Contains(metadata, "\"Potwierdź Zamówienie\" = 1,");
		StringAssert.Contains(metadata, "\"Größe & Maße\" = 3,");
	}

	[TestMethod]
	public void GenerateTemplate_CSharp_DescriptionKeepsNonAsciiLabels()
	{
		string enums = Generate("C#", false, source: BuildProjectEntityWithInternationalLabels())["new_project.Enums.cs"];

		StringAssert.Contains(enums, "[Description(\"Potwierdź Zamówienie\")]");
		StringAssert.Contains(enums, "[Description(\"Zażółć gęślą jaźń\")]");
		StringAssert.Contains(enums, "[Description(\"Größe & Maße\")]");
		StringAssert.Contains(enums, "[Description(\"Überfällig\")]");
		StringAssert.Contains(enums, "[Description(\"注文 確認\")]");
	}

	[TestMethod]
	public void GenerateTemplate_CSharpSingle_DescriptionKeepsNonAsciiLabels()
	{
		string context = Generate("C#", true, source: BuildProjectEntityWithInternationalLabels())["XrmServiceContext.cs"];

		StringAssert.Contains(context, "[Description(\"Potwierdź Zamówienie\")]");
		StringAssert.Contains(context, "[Description(\"Café Crème\")]");
	}

	private static EntityMetadata BuildProjectEntityWithInternationalLabels()
	{
		EntityMetadata entity = BuildProjectEntity();

		PicklistAttributeMetadata orderStatus = Attribute(new PicklistAttributeMetadata
			{
				OptionSet = BuildOptionSet(
					(1, "Potwierdź Zamówienie"),
					(2, "Zażółć gęślą jaźń"),
					(3, "Größe & Maße"),
					(4, "Überfällig"),
					(5, "Café Crème"),
					(6, "Привіт"),
					(7, "注文 確認"))
			},
			"new_orderstatus",
			"new_OrderStatus",
			"Order Status");

		AttributeMetadata[] attributes = entity.Attributes.Append(orderStatus).ToArray();
		SetSealed(entity, nameof(EntityMetadata.Attributes), attributes);

		return entity;
	}

	private static Dictionary<string, bool> GenerateOverExistingFiles(bool clearOutputDirectory)
	{
		string originalDirectory = Directory.GetCurrentDirectory();
		string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		string outDirectory = Path.Combine(tempRoot, "out");
		Directory.CreateDirectory(Path.Combine(outDirectory, "nested"));

		try
		{
			Directory.SetCurrentDirectory(tempRoot);
			const string generatedHeader = "// <auto-generated>\r\n// old\r\n// </auto-generated>\r\n";
			File.WriteAllText(Path.Combine(outDirectory, "old.entity.cs"), generatedHeader);
			File.WriteAllText(Path.Combine(outDirectory, "nested", "old.Enums.cs"), generatedHeader);
			File.WriteAllText(Path.Combine(outDirectory, "mine.cs"), "class Mine { }");
			File.WriteAllText(Path.Combine(outDirectory, "notes.txt"), generatedHeader);

			MappingEntity project = MappingEntity.Parse(BuildProjectEntity());
			LinkSelfReferencedRelationships(project);

			new ScribanGenerator("Main",
				"Templates",
				"out",
				new Context { Namespace = "Test.Dataverse", Entities = new[] { project } },
				new TemplateEngineModel { Name = "scriban", Type = "C#", IsSingleOutput = false },
				clearOutputDirectory: clearOutputDirectory).GenerateTemplate();

			return new[] { "old.entity.cs", "nested/old.Enums.cs", "mine.cs", "notes.txt", "new_project.entity.cs" }
			   .ToDictionary(f => f, f => File.Exists(Path.Combine(outDirectory, f)));
		}
		finally
		{
			Directory.SetCurrentDirectory(originalDirectory);
			Directory.Delete(tempRoot, true);
		}
	}

	private static void AssertMatchesBaseline(string scenario, string type, bool isSingleOutput)
	{
		Dictionary<string, string> actual = Generate(type, isSingleOutput);
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

		// Parallel generation must produce exactly the same files.
		Dictionary<string, string> parallel = Generate(type, isSingleOutput, degreeOfParallelism: 4);

		foreach (string fileName in expectedFiles)
		{
			Assert.AreEqual(actual[fileName], parallel[fileName], $"Parallel output of '{fileName}' differs from baseline.");
		}
	}

	private static Dictionary<string, string> Generate(
		string type,
		bool isSingleOutput,
		bool legacyEntityFileNames = false,
		int degreeOfParallelism = 1,
		bool preserveOptionSetLabels = false,
		EntityMetadata source = null)
	{
		string originalDirectory = Directory.GetCurrentDirectory();
		string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempRoot);

		try
		{
			Directory.SetCurrentDirectory(tempRoot);

			MappingEntity project = MappingEntity.Parse(source ?? BuildProjectEntity());
			LinkSelfReferencedRelationships(project);

			Context context = new()
			{
				Namespace = "Test.Dataverse",
				Entities = new[] { project }
			};
			context.Info.PreserveOptionSetLabels = preserveOptionSetLabels;

			ScribanGenerator generator = new("Main",
				"Templates",
				"out",
				context,
				new TemplateEngineModel
				{
					Name = "scriban",
					Type = type,
					IsSingleOutput = isSingleOutput
				},
				null,
				legacyEntityFileNames,
				degreeOfParallelism: degreeOfParallelism);

			generator.GenerateTemplate();

			string outDirectory = Path.Combine(tempRoot, "out");

			return Directory.GetFiles(outDirectory)
			   .ToDictionary(f => Path.GetFileName(f), f => Normalize(File.ReadAllText(f)));
		}
		finally
		{
			Directory.SetCurrentDirectory(originalDirectory);
			Directory.Delete(tempRoot, true);
		}
	}

	// GeneratorInfo embeds the host process file version, which changes with the test runner.
	private static string Normalize(string content)
	{
		content = Regex.Replace(content, @"(Runtime Version|File Version):[^\r\n]*", "$1: <version>");

		return Regex.Replace(content,
			"GeneratedCode\\(\"DataverseGen-Cli\",\"[^\"]*\"\\)",
			"GeneratedCode(\"DataverseGen-Cli\",\"<version>\")");
	}

	// DataverseMetadataConverter resolves ToEntity across the selected entities; all test relationships are self-referenced.
	internal static void LinkSelfReferencedRelationships(MappingEntity entity)
	{
		foreach (MappingRelationship1N relationship in entity.RelationshipsOneToMany)
		{
			relationship.ToEntity = entity;
		}

		foreach (MappingRelationshipN1 relationship in entity.RelationshipsManyToOne)
		{
			relationship.ToEntity = entity;
		}

		foreach (MappingRelationshipMn relationship in entity.RelationshipsManyToMany)
		{
			relationship.ToEntity = entity;
		}
	}

	private static string GetSourceDirectory([CallerFilePath] string path = "")
	{
		return Path.GetDirectoryName(path)!;
	}

	internal static EntityMetadata BuildProjectEntity()
	{
		EntityMetadata entity = new()
		{
			LogicalName = "new_project",
			SchemaName = "new_Project",
			Description = BuildLabel("Project tracked by the team")
		};

		UniqueIdentifierAttributeMetadata id = Attribute(new UniqueIdentifierAttributeMetadata(),
			"new_projectid",
			"new_ProjectId",
			"Project");

		SetSealed(id, nameof(AttributeMetadata.IsPrimaryId), true);

		StringAttributeMetadata name = Attribute(new StringAttributeMetadata { MaxLength = 100 },
			"new_name",
			"new_Name",
			"Name");

		name.RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.ApplicationRequired);

		MoneyAttributeMetadata budget = Attribute(new MoneyAttributeMetadata { MinValue = 0, MaxValue = 1000000 },
			"new_budget",
			"new_Budget",
			"Budget");

		PicklistAttributeMetadata category = Attribute(new PicklistAttributeMetadata
			{
				OptionSet = BuildOptionSet((100000000, "Internal"), (100000001, "Customer"))
			},
			"new_category",
			"new_Category",
			"Category");

		category.OptionSet.Options[1].Description = BuildLabel("Project done for a customer");

		BooleanAttributeMetadata isActive = Attribute(new BooleanAttributeMetadata
			{
				OptionSet = new BooleanOptionSetMetadata(new OptionMetadata(BuildLabel("Yes"), 1),
					new OptionMetadata(BuildLabel("No"), 0))
			},
			"new_isactive",
			"new_IsActive",
			"Is Active");

		StateAttributeMetadata state = Attribute(new StateAttributeMetadata
			{
				OptionSet = BuildOptionSet((0, "Active"), (1, "Inactive"))
			},
			"statecode",
			"statecode",
			"Status");

		StatusAttributeMetadata status = Attribute(new StatusAttributeMetadata
			{
				OptionSet = BuildOptionSet((1, "Active"), (2, "Inactive"))
			},
			"statuscode",
			"statuscode",
			"Status Reason");

		LookupAttributeMetadata parentProject = Attribute(new LookupAttributeMetadata { Targets = new[] { "new_project" } },
			"new_parentprojectid",
			"new_ParentProjectId",
			"Parent Project");

		DateTimeAttributeMetadata createdOn = Attribute(new DateTimeAttributeMetadata(),
			"createdon",
			"CreatedOn",
			"Created On");

		SetSealed(createdOn, nameof(AttributeMetadata.IsValidForCreate), false);
		SetSealed(createdOn, nameof(AttributeMetadata.IsValidForUpdate), false);

		SetSealed(entity, nameof(EntityMetadata.ObjectTypeCode), 10001);
		SetSealed(entity, nameof(EntityMetadata.IsIntersect), false);
		SetSealed(entity, nameof(EntityMetadata.PrimaryIdAttribute), "new_projectid");
		SetSealed(entity, nameof(EntityMetadata.PrimaryNameAttribute), "new_name");

		SetSealed(entity,
			nameof(EntityMetadata.Attributes),
			new AttributeMetadata[] { id, name, budget, category, isActive, state, status, parentProject, createdOn });

		SetSealed(entity,
			nameof(EntityMetadata.OneToManyRelationships),
			new[]
			{
				new OneToManyRelationshipMetadata
				{
					SchemaName = "new_project_parentproject",
					ReferencedEntity = "new_project",
					ReferencedAttribute = "new_projectid",
					ReferencingEntity = "new_project",
					ReferencingAttribute = "new_parentprojectid"
				}
			});

		SetSealed(entity,
			nameof(EntityMetadata.ManyToOneRelationships),
			new[]
			{
				new OneToManyRelationshipMetadata
				{
					SchemaName = "new_project_parentproject",
					ReferencedEntity = "new_project",
					ReferencedAttribute = "new_projectid",
					ReferencingEntity = "new_project",
					ReferencingAttribute = "new_parentprojectid"
				}
			});

		SetSealed(entity,
			nameof(EntityMetadata.ManyToManyRelationships),
			new[]
			{
				new ManyToManyRelationshipMetadata
				{
					SchemaName = "new_project_relatedproject",
					Entity1LogicalName = "new_project",
					Entity1IntersectAttribute = "new_projectidone",
					Entity2LogicalName = "new_project",
					Entity2IntersectAttribute = "new_projectidtwo",
					IntersectEntityName = "new_project_relatedproject"
				}
			});

		return entity;
	}

	private static T Attribute<T>(T attribute, string logicalName, string schemaName, string displayName)
		where T : AttributeMetadata
	{
		attribute.LogicalName = logicalName;
		attribute.SchemaName = schemaName;
		attribute.DisplayName = BuildLabel(displayName);
		attribute.Description = BuildLabel($"{displayName} description");
		SetSealed(attribute, nameof(AttributeMetadata.IsValidForCreate), true);
		SetSealed(attribute, nameof(AttributeMetadata.IsValidForRead), true);
		SetSealed(attribute, nameof(AttributeMetadata.IsValidForUpdate), true);

		return attribute;
	}

	private static OptionSetMetadata BuildOptionSet(params (int value, string label)[] options)
	{
		OptionSetMetadata optionSet = new();

		foreach ((int value, string label) in options)
		{
			optionSet.Options.Add(new OptionMetadata(BuildLabel(label), value));
		}

		return optionSet;
	}

	// Label(string, int) does NOT set UserLocalizedLabel — must be set explicitly
	private static Label BuildLabel(string text)
	{
		LocalizedLabel localizedLabel = new(text, 1033);

		return new Label(localizedLabel, new[] { localizedLabel });
	}

	// Several metadata properties have non-public setters in the Xrm SDK.
	private static void SetSealed(object target, string propertyName, object value)
	{
		PropertyInfo property = target.GetType().GetProperty(propertyName)
			?? throw new InvalidOperationException($"Property '{propertyName}' not found.");

		property.SetValue(target, value);
	}
}
