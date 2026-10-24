using System;
using Microsoft.Xrm.Sdk;

namespace MyApp.CustomApis;

public sealed class GetStatusRequest : OrganizationRequest
{
	public const string RequestNameValue = "newgetstatus";

	public GetStatusRequest(string customerId)
	{
		RequestName = RequestNameValue;
		CustomerId = customerId;
	}

	public string CustomerId
	{
		get => Parameters.ContainsKey("customerid")
			? (string)Parameters["customerid"]
			: default;
		set => Parameters["customerid"] = value;
	}

}
