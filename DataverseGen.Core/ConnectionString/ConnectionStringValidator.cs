using System.Text.RegularExpressions;
using DataverseGen.Core.Reporting;

namespace DataverseGen.Core.ConnectionString;

public partial class ConnectionStringValidator
{
	private static readonly Regex ConnectionStringRegex = ConnectionStringRegexCompiled();
	private readonly string _connectionString;
	private readonly IGenerationReporter _reporter;

	public ConnectionStringValidator(string connectionString, IGenerationReporter reporter = null)
	{
		_connectionString = connectionString;
		_reporter = reporter ?? NullReporter.Instance;
	}

	private IEnumerable<KeyValuePair<string, string>> ParseConnectionStrings()
	{
		if (!ConnectionStringRegex.IsMatch(_connectionString))
		{
			throw new ArgumentException(@"ConnectionString not valid no match found for tokens!");
		}

		foreach (Match match in ConnectionStringRegex.Matches(_connectionString))
		{
			yield return new KeyValuePair<string, string>(match.Groups["key"].Value.Trim(),
				match.Groups["val"].Value.Trim());
		}
	}

	public bool TryValidate()
	{
		try
		{
			return InternalValidate();
		}
		catch (Exception ex)
		{
			_reporter.Log(LogLevel.Error, ex.Message, ex);

			return false;
		}
	}

	public void Validate()
	{
		InternalValidate();
	}

	private bool InternalValidate()
	{
		IDictionary<string, string> connectionStringTokens = ParseConnectionStrings()
		   .ToDictionary(k => k.Key, v => v.Value);

		IConnectionStringValidator validator = ConnectionStringValidatorFactory
		   .CreateValidator(connectionStringTokens);

		return validator.Validate();
	}

	[GeneratedRegex("(?<key>[^=;,]+)=(?<val>[^;,]+(,\\d+)?)")]
	private static partial Regex ConnectionStringRegexCompiled();
}