using System;
using Microsoft.Xrm.Sdk;

namespace MyApp.CustomApis;

public sealed class ProcessStatusRequest : OrganizationRequest
{
	public const string RequestNameValue = "new\"update\\status";

	public static class ParameterNames
	{
		public const string Entity = "entity";
		public const string Flag = "flag";
		public const string Count = "count";
		public const string Names = "new_names";
	}

	public ProcessStatusRequest(bool flag, string[] names, EntityReference entity, int? count = null)
	{
		RequestName = RequestNameValue;
		Flag = flag;
		Names = names;
		Entity = entity;
		if (count != null)
		{
			Count = count;
		}
	}

	/// <summary>Required.</summary>
	public EntityReference Entity
	{
		get => Parameters.TryGetValue(ParameterNames.Entity, out object value) ? (EntityReference)value : default;
		set => Parameters[ParameterNames.Entity] = value;
	}

	/// <summary>Required.</summary>
	public bool Flag
	{
		get => Parameters.TryGetValue(ParameterNames.Flag, out object value) ? (bool)value : default;
		set => Parameters[ParameterNames.Flag] = value;
	}

	/// <summary>Optional. Number of items &amp; &lt;more&gt;</summary>
	public int? Count
	{
		get => Parameters.TryGetValue(ParameterNames.Count, out object value) ? (int?)value : default;
		set => Parameters[ParameterNames.Count] = value;
	}

	/// <summary>Required.</summary>
	public string[] Names
	{
		get => Parameters.TryGetValue(ParameterNames.Names, out object value) ? (string[])value : default;
		set => Parameters[ParameterNames.Names] = value;
	}

}
