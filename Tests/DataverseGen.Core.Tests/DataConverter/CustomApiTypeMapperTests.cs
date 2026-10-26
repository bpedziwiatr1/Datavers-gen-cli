using DataverseGen.Core.DataConverter;
using DataverseGen.Core.Extensions;

namespace DataverseGen.Core.Tests.DataConverter;

[TestClass]
public class CustomApiTypeMapperTests
{
	private const string Primitive = "WebApiRequestStructuralProperty.PrimitiveType";
	private const string EntityType = "WebApiRequestStructuralProperty.EntityType";
	private const string Collection = "WebApiRequestStructuralProperty.Collection";

	[TestMethod]
	[DataRow("Boolean", "bool", "boolean", "Edm.Boolean", Primitive)]
	[DataRow("DateTime", "DateTime", "Date", "Edm.DateTimeOffset", Primitive)]
	[DataRow("Date and Time", "DateTime", "Date", "Edm.DateTimeOffset", Primitive)]
	[DataRow("Decimal", "decimal", "number", "Edm.Decimal", Primitive)]
	[DataRow("Float", "double", "number", "Edm.Double", Primitive)]
	[DataRow("Integer", "int", "number", "Edm.Int32", Primitive)]
	[DataRow("Whole Number", "int", "number", "Edm.Int32", Primitive)]
	[DataRow("Picklist", "int", "number", "Edm.Int32", Primitive)]
	[DataRow("Money", "Money", "number", "Edm.Decimal", Primitive)]
	[DataRow("Guid", "Guid", "string", "Edm.Guid", Primitive)]
	[DataRow("String", "string", "string", "Edm.String", Primitive)]
	[DataRow("StringArray", "string[]", "string[]", "Collection(Edm.String)", Collection)]
	[DataRow("Collection of strings", "string[]", "string[]", "Collection(Edm.String)", Collection)]
	[DataRow("EntityReference", "EntityReference", "any", "mscrm.crmbaseentity", EntityType)]
	[DataRow("EntityCollection", "EntityCollection", "any[]", "Collection(mscrm.crmbaseentity)", Collection)]
	[DataRow("", "string", "string", "Edm.String", Primitive)]
	[DataRow(null, "string", "string", "Edm.String", Primitive)]
	[DataRow("Unknown Type", "string", "string", "Edm.String", Primitive)]
	[DataRow("7", "string", "string", "Edm.String", Primitive)]
	public void Resolve_MapsTypeIdenticallyForRequestAndResponse(
		string? label,
		string expectedCSharp,
		string expectedTypeScript,
		string expectedWebApi,
		string expectedStructural)
	{
		foreach (bool isResponse in new[] { false, true })
		{
			CustomApiTypeInfo info = CustomApiTypeMapper.Resolve(label!, string.Empty, isResponse);

			Assert.AreEqual(expectedCSharp, info.CSharpTypeName);
			Assert.AreEqual(expectedTypeScript, info.TypeScriptType);
			Assert.AreEqual(expectedWebApi, info.WebApiTypeName);
			Assert.AreEqual(expectedStructural, info.WebApiStructuralProperty);
		}
	}

	[TestMethod]
	[DataRow("Entity", false, "EntityReference")]
	[DataRow("Entity", true, "Entity")]
	public void Resolve_EntityDiffersByDirection(string label, bool isResponse, string expectedCSharp)
	{
		CustomApiTypeInfo info = CustomApiTypeMapper.Resolve(label, "account", isResponse);

		Assert.AreEqual(expectedCSharp, info.CSharpTypeName);
		Assert.AreEqual("any", info.TypeScriptType);
		Assert.AreEqual("mscrm.account", info.WebApiTypeName);
		Assert.AreEqual(EntityType, info.WebApiStructuralProperty);
	}

	[TestMethod]
	public void Resolve_EntityCollectionUsesLogicalEntityName()
	{
		CustomApiTypeInfo info = CustomApiTypeMapper.Resolve("EntityCollection", "account", false);

		Assert.AreEqual("EntityCollection", info.CSharpTypeName);
		Assert.AreEqual("Collection(mscrm.account)", info.WebApiTypeName);
		Assert.AreEqual(Collection, info.WebApiStructuralProperty);
	}

	[TestMethod]
	[DataRow("my custom_api", "MyCustomApi")]
	[DataRow("a-b.c/d\\e", "ABCDE")]
	[DataRow("already", "Already")]
	[DataRow("", "")]
	[DataRow(null, "")]
	public void ToPascalCase_CapitalizesEachPart(string? value, string expected)
	{
		Assert.AreEqual(expected, MetadataNamingExtensions.ToPascalCase(value!));
	}
}
