using System;
using Microsoft.Xrm.Sdk;

namespace MyApp.CustomApis;

public sealed class PingRequest : OrganizationRequest
{
	public const string RequestNameValue = "newping";

	public PingRequest()
	{
		RequestName = RequestNameValue;
	}

}
