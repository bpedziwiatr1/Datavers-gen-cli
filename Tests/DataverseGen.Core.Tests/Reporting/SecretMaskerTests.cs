using DataverseGen.Core.Reporting;

namespace DataverseGen.Core.Tests.Reporting;

[TestClass]
public class SecretMaskerTests
{
	[TestMethod]
	[DataRow("ClientSecret")]
	[DataRow("Password")]
	[DataRow("Secret")]
	[DataRow("Key")]
	[DataRow("Thumbprint")]
	public void MaskSecrets_MasksValueOfEachKey(string key)
	{
		string text = $"AuthType=ClientSecret;Url=https://contoso.crm.dynamics.com;{key}=s3cr3t-value;ClientId=abc";

		string masked = SecretMasker.MaskSecrets(text);

		Assert.DoesNotContain("s3cr3t-value", masked);
		Assert.Contains($"{key}=***", masked);
		Assert.Contains("Url=https://contoso.crm.dynamics.com", masked);
		Assert.Contains("ClientId=abc", masked);
	}

	[TestMethod]
	[DataRow("clientsecret=abc123")]
	[DataRow("CLIENTSECRET = abc123")]
	[DataRow("pAsSwOrD=abc123")]
	[DataRow("thumbprint=abc123")]
	public void MaskSecrets_IsCaseInsensitive(string text)
	{
		Assert.DoesNotContain("abc123", SecretMasker.MaskSecrets(text));
	}

	[TestMethod]
	public void MaskSecrets_MasksQuotedValuesContainingSeparators()
	{
		string masked = SecretMasker.MaskSecrets("Url=x;Password=\"a;b,c\";ClientId=abc");

		Assert.DoesNotContain("a;b,c", masked);
		Assert.Contains("ClientId=abc", masked);
	}

	[TestMethod]
	public void MaskSecrets_MasksSecretAtEndWithoutTrailingSemicolon()
	{
		Assert.DoesNotContain("tail-secret", SecretMasker.MaskSecrets("Url=x;ClientSecret=tail-secret"));
	}

	[TestMethod]
	public void MaskSecrets_LeavesTextWithoutSecretsUntouched()
	{
		Assert.AreEqual("Url=x;AuthType=AD;PrimaryKey=1", SecretMasker.MaskSecrets("Url=x;AuthType=AD;PrimaryKey=1"));
		Assert.IsNull(SecretMasker.MaskSecrets(null!));
		Assert.AreEqual(string.Empty, SecretMasker.MaskSecrets(string.Empty));
	}
}
