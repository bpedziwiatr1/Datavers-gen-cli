namespace DataverseGen.Core.CustomApi;

/// <summary>
///  Template model of a single custom API C# request class. Members are exposed to Scriban in snake_case.
/// </summary>
public class CustomApiCSharpView
{
	public string Namespace { get; set; }

	public string ClassName { get; set; }

	/// <summary>Operation name already escaped for use inside a string literal.</summary>
	public string OperationName { get; set; }

	/// <summary>
	///  Constructor arguments: required request parameters, the bound entity reference, then optional request
	///  parameters (which default to null).
	/// </summary>
	public List<CustomApiCSharpParameterView> ConstructorParameters { get; set; } = [];

	/// <summary>Request properties: the bound entity reference followed by the request parameters.</summary>
	public List<CustomApiCSharpParameterView> Properties { get; set; } = [];
}

public class CustomApiCSharpParameterView
{
	/// <summary>C# type; optional value types are already nullable (e.g. <c>int?</c>).</summary>
	public string Type { get; set; }

	public bool IsOptional { get; set; }

	/// <summary>Text of the XML doc summary (Required./Optional. plus description), already XML-escaped on one line.</summary>
	public string Summary { get; set; }

	public string PropertyName { get; set; }

	public string ParameterName { get; set; }

	/// <summary>Key of the value in OrganizationRequest.Parameters, already escaped for a string literal.</summary>
	public string RequestPropertyName { get; set; }
}
