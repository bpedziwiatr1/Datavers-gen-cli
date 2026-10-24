namespace DataverseGen.Core.CustomApi;

/// <summary>
///  Template model of a single custom API TypeScript request class. Members are exposed to Scriban in snake_case.
/// </summary>
public class CustomApiTypeScriptView
{
	public string ClassName { get; set; }

	/// <summary>Operation name already escaped for use inside a string literal.</summary>
	public string OperationName { get; set; }

	/// <summary>Quoted request property name of the bound entity, or the literal <c>null</c>.</summary>
	public string BoundParameterName { get; set; }

	public string OperationType { get; set; }

	/// <summary>Constructor arguments: request parameters followed by the bound entity.</summary>
	public List<CustomApiTypeScriptParameterView> ConstructorParameters { get; set; } = [];

	/// <summary>Request properties: the bound entity followed by the request parameters.</summary>
	public List<CustomApiTypeScriptParameterView> Properties { get; set; } = [];
}

public class CustomApiTypeScriptParameterView
{
	public bool IsBound { get; set; }

	public string RequestPropertyName { get; set; }

	public string ConstructorParameterName { get; set; }

	public string TypeScriptType { get; set; }

	public string WebApiTypeName { get; set; }

	public string WebApiStructuralProperty { get; set; }
}
