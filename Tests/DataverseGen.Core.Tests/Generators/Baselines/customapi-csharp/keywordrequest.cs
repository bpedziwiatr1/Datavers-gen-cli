using System;
using Microsoft.Xrm.Sdk;

namespace MyApp.CustomApis;

public sealed class KeywordRequest : OrganizationRequest
{
	public const string RequestNameValue = "newkeyword";

	public KeywordRequest(string @private)
	{
		RequestName = RequestNameValue;
		Private = @private;
	}

	public string Private
	{
		get => Parameters.ContainsKey("private")
			? (string)Parameters["private"]
			: default;
		set => Parameters["private"] = value;
	}

}
