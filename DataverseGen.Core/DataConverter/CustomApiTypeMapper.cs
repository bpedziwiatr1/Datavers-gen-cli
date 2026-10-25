namespace DataverseGen.Core.DataConverter;

internal sealed record CustomApiTypeInfo(
	string CSharpTypeName,
	string TypeScriptType,
	string WebApiTypeName,
	string WebApiStructuralProperty);

internal static class CustomApiTypeMapper
{
	private const string Primitive = "WebApiRequestStructuralProperty.PrimitiveType";
	private const string EntityType = "WebApiRequestStructuralProperty.EntityType";
	private const string Collection = "WebApiRequestStructuralProperty.Collection";

	private static readonly CustomApiTypeInfo StringType = new("string", "string", "Edm.String", Primitive);

	internal static CustomApiTypeInfo Resolve(string typeLabel, string logicalEntityName, bool isResponse)
	{
		string key = NormalizeLabel(typeLabel);
		string entityName = string.IsNullOrWhiteSpace(logicalEntityName) ? "crmbaseentity" : logicalEntityName;

		return key switch
		{
			"boolean" => new("bool", "boolean", "Edm.Boolean", Primitive),
			"datetime" or "dateandtime" => new("DateTime", "Date", "Edm.DateTimeOffset", Primitive),
			"decimal" or "decimalnumber" => new("decimal", "number", "Edm.Decimal", Primitive),
			"float" => new("double", "number", "Edm.Double", Primitive),
			"integer" or "picklist" or "wholenumber" => new("int", "number", "Edm.Int32", Primitive),
			"money" => new("Money", "number", "Edm.Decimal", Primitive),
			"guid" or "uniqueidentifier" => new("Guid", "string", "Edm.Guid", Primitive),
			"stringarray" or "collectionofstrings" => new("string[]", "string[]", "Collection(Edm.String)", Collection),
			"entity" => new(isResponse ? "Entity" : "EntityReference", "any", $"mscrm.{entityName}", EntityType),
			"entityreference" => new("EntityReference", "any", $"mscrm.{entityName}", EntityType),
			"entitycollection" => new("EntityCollection", "any[]", $"Collection(mscrm.{entityName})", Collection),
			_ => StringType
		};
	}

	private static string NormalizeLabel(string typeLabel)
	{
		return (typeLabel ?? string.Empty)
		   .Replace(" ", string.Empty)
		   .Replace("_", string.Empty)
		   .Trim()
		   .ToLowerInvariant();
	}
}
