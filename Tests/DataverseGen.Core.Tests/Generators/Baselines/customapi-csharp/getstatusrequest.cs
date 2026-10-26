using System;
using Microsoft.Xrm.Sdk;

namespace MyApp.CustomApis;

public sealed class GetStatusRequest : OrganizationRequest
{
	public const string RequestNameValue = "newgetstatus";

	public static class ParameterNames
	{
		public const string CustomerId = "customerid";
	}

	public GetStatusRequest(string customerId)
	{
		RequestName = RequestNameValue;
		CustomerId = customerId;
	}

	/// <summary>Required.</summary>
	public string CustomerId
	{
		get => Parameters.TryGetValue(ParameterNames.CustomerId, out object value) ? (string)value : default;
		set => Parameters[ParameterNames.CustomerId] = value;
	}

}
