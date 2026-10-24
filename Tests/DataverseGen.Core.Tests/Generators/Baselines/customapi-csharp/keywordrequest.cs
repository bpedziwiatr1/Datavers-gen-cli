using System;
using Microsoft.Xrm.Sdk;

namespace MyApp.CustomApis;

public sealed class KeywordRequest : OrganizationRequest
{
	public const string RequestNameValue = "newkeyword";

	public static class ParameterNames
	{
		public const string Private = "private";
	}

	public KeywordRequest(string @private)
	{
		RequestName = RequestNameValue;
		Private = @private;
	}

	/// <summary>Required.</summary>
	public string Private
	{
		get => Parameters.TryGetValue(ParameterNames.Private, out object value) ? (string)value : default;
		set => Parameters[ParameterNames.Private] = value;
	}

}
