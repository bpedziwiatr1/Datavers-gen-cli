namespace DataverseGen.Core.ConnectionString.Validators;

internal static class ValidatorTokenHelper
{
	public static KeyValuePair<string, string> CheckIfTokenIsPresentWithValue(
		IDictionary<string, string> tokens,
		HashSet<string> searchTokens)
	{
		string foundToken = ConnectionStringConst.GetToken(tokens, searchTokens);

		return CheckIfTokenIsPresentWithValue(tokens, foundToken);
	}

	public static KeyValuePair<string, string> CheckIfTokenIsPresentWithValue(
		IDictionary<string, string> tokens,
		string searchToken)
	{
		string tokenName = CheckIfTokenKeyPresent(tokens, searchToken);
		CheckIfTokenValuePresent(tokens, searchToken);

		return new KeyValuePair<string, string>(tokenName, tokens[tokenName]);
	}

	public static bool TryCheckIfTokenIsPresentWithValue(
		IDictionary<string, string> tokens,
		HashSet<string> searchTokens,
		out KeyValuePair<string, string> foundToken)
	{
		string tokenName = searchTokens.FirstOrDefault(tokens.ContainsKey);

		return TryGetTokenWithValue(tokens, tokenName, out foundToken);
	}

	public static bool TryCheckIfTokenIsPresentWithValue(
		IDictionary<string, string> tokens,
		string searchToken,
		out KeyValuePair<string, string> foundToken)
	{
		return TryGetTokenWithValue(tokens, searchToken, out foundToken);
	}

	private static bool TryGetTokenWithValue(
		IDictionary<string, string> tokens,
		string tokenName,
		out KeyValuePair<string, string> foundToken)
	{
		foundToken = new KeyValuePair<string, string>();

		if (tokenName is null || !tokens.TryGetValue(tokenName, out string value) || string.IsNullOrWhiteSpace(value))
		{
			return false;
		}

		foundToken = new KeyValuePair<string, string>(tokenName, value);

		return true;
	}

	private static string CheckIfTokenKeyPresent(
		IDictionary<string, string> tokens,
		string searchToken)
	{
		return !tokens.ContainsKey(searchToken)
			? throw new ArgumentNullException($"Missing token:{searchToken} Token from connectionstring")
			: searchToken;
	}

	private static void CheckIfTokenValuePresent(
		IDictionary<string, string> tokens,
		string searchToken)
	{
		string value = tokens[searchToken];

		if (string.IsNullOrWhiteSpace(value))
		{
			throw new ArgumentNullException($"Token {searchToken}, value: {value} IsNullOrWhiteSpace");
		}
	}
}