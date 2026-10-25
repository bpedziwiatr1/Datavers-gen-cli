using System.Text.RegularExpressions;

namespace DataverseGen.Core.Reporting;

/// <summary>Masks secret values (<c>ClientSecret=...</c>, <c>Password=...</c>) in connection strings and free text.</summary>
public static partial class SecretMasker
{
	public const string Mask = "***";

	public static string MaskSecrets(string text)
	{
		return string.IsNullOrEmpty(text)
			? text
			: SecretRegex().Replace(text, match => $"{match.Groups["key"].Value}{match.Groups["sep"].Value}{Mask}");
	}

	[GeneratedRegex("(?<![\\w])(?<key>ClientSecret|Password|Secret|Key|Thumbprint)(?<sep>\\s*=\\s*)(\"[^\"]*\"|'[^']*'|[^;]*)",
		RegexOptions.IgnoreCase)]
	private static partial Regex SecretRegex();
}
