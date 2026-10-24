using System;
using Microsoft.Xrm.Sdk;

namespace MyApp.CustomApis;

public sealed class ProcessStatusRequest : OrganizationRequest
{
	public const string RequestNameValue = "new\"update\\status";

	public ProcessStatusRequest(bool flag, int count, string[] names, EntityReference entity)
	{
		RequestName = RequestNameValue;
		Flag = flag;
		Count = count;
		Names = names;
		Entity = entity;
	}

	public EntityReference Entity
	{
		get => Parameters.ContainsKey("entity")
			? (EntityReference)Parameters["entity"]
			: default;
		set => Parameters["entity"] = value;
	}

	public bool Flag
	{
		get => Parameters.ContainsKey("flag")
			? (bool)Parameters["flag"]
			: default;
		set => Parameters["flag"] = value;
	}

	public int Count
	{
		get => Parameters.ContainsKey("count")
			? (int)Parameters["count"]
			: default;
		set => Parameters["count"] = value;
	}

	public string[] Names
	{
		get => Parameters.ContainsKey("new_names")
			? (string[])Parameters["new_names"]
			: default;
		set => Parameters["new_names"] = value;
	}

}
