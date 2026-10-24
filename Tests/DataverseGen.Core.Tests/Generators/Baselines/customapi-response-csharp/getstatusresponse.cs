using System;
using Microsoft.Xrm.Sdk;

namespace MyApp.CustomApis;

public sealed class GetStatusResponse : OrganizationResponse
{
	public static class ParameterNames
	{
		public const string StatusText = "statustext";
		public const string Count = "count";
		public const string Account = "account";
		public const string Accounts = "accounts";
		public const string Private = "private";
	}

	/// <summary>Status &amp; &lt;details&gt;</summary>
	public string StatusText => Results.TryGetValue(ParameterNames.StatusText, out object value) ? (string)value : default;

	/// <summary>Count</summary>
	public int Count => Results.TryGetValue(ParameterNames.Count, out object value) ? (int)value : default;

	/// <summary>Account</summary>
	public Entity Account => Results.TryGetValue(ParameterNames.Account, out object value) ? (Entity)value : default;

	/// <summary>Accounts</summary>
	public EntityCollection Accounts => Results.TryGetValue(ParameterNames.Accounts, out object value) ? (EntityCollection)value : default;

	/// <summary>Private</summary>
	public string Private => Results.TryGetValue(ParameterNames.Private, out object value) ? (string)value : default;

}
