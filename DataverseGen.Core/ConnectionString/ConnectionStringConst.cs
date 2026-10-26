namespace DataverseGen.Core.ConnectionString;

internal static class ConnectionStringConst
{
	public const string Password = "Password";

	public static readonly HashSet<string> Auth = Aliases("AuthenticationType", "AuthType");

	public static readonly HashSet<string> ClientId = Aliases("ClientId", "AppId", "ApplicationId");

	public static readonly HashSet<string> ClientSecret = Aliases("ClientSecret", "Secret");

	public static readonly HashSet<string> IntegratedSecurity = Aliases("Integrated Security");

	public static readonly HashSet<string> RedirectUri = Aliases("RedirectUri", "ReplyUrl");

	public static readonly HashSet<string> TokenCacheStorePath = Aliases("TokenCacheStorePath");

	public static readonly HashSet<string> Url = Aliases("ServiceUri", "Service Uri", "Url", "Server");

	public static readonly HashSet<string> UserName = Aliases("UserName", "User Name", "UserId", "User Id");

	public static readonly HashSet<string> Thumbprint = Aliases("Thumbprint", "CertThumbprint");

	private static readonly Dictionary<string, ConnectionType> AuthTypes = new(StringComparer.OrdinalIgnoreCase)
	{
		["OAuth"] = ConnectionType.OAuth,
		["AD"] = ConnectionType.AD,
		["IFD"] = ConnectionType.IFD,
		["Certificate"] = ConnectionType.Certificate,
		["ClientSecret"] = ConnectionType.ClientSecret,
		["Office365"] = ConnectionType.Office365
	};

	private static HashSet<string> Aliases(params string[] names)
	{
		return new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
	}

	public static string GetToken(
		IDictionary<string, string> tokens,
		HashSet<string> tokenNames)
	{
		string firstValidToken = tokenNames.FirstOrDefault(tokens.ContainsKey);

		return firstValidToken
			?? throw new Exception(
				$"token not found {string.Join(", ", tokenNames)} in {string.Join(", ", tokens.Keys)}");
	}

	public static ConnectionType Map(string authType)
	{
		return authType is not null && AuthTypes.TryGetValue(authType, out ConnectionType connectionType)
			? connectionType
			: throw new NotImplementedException($"not implemented authType:{authType}");
	}
}
