using DataverseGen.Core.ConnectionString;

namespace DataverseGen.Core.Tests.ConnectionString;

[TestClass]
public class ConnectionStringValidatorTests
{
	[TestMethod]
	[DataRow("")]
	[DataRow("notavalidformat")]
	[DataRow("no-equals-sign")]
	public void Validate_InvalidFormat_ThrowsArgumentException(string connectionString)
	{
		Assert.ThrowsExactly<ArgumentException>(() =>
			new ConnectionStringValidator(connectionString).Validate());
	}

	[TestMethod]
	public void Validate_NoAuthToken_ThrowsException()
	{
		Assert.ThrowsExactly<Exception>(() =>
			new ConnectionStringValidator("SomeKey=value;Url=https://contoso.crm.dynamics.com;").Validate());
	}

	[TestMethod]
	public void Validate_UnknownAuthType_ThrowsNotImplementedException()
	{
		Assert.ThrowsExactly<NotImplementedException>(() =>
			new ConnectionStringValidator("AuthType=Basic;Url=https://contoso.crm.dynamics.com;").Validate());
	}

	[TestMethod]
	[DataRow("authtype=office365;username=jsmith@contoso.onmicrosoft.com;password=passcode;url=https://contoso.crm.dynamics.com;")]
	[DataRow("AUTHTYPE=ClientSecret;CLIENTID=abc;SECRET=xyz;URL=https://contoso.crm.dynamics.com;")]
	[DataRow("authenticationtype=AD;SERVICEURI=https://contoso:8080/Test;")]
	public void Validate_KeysAndAuthTypeInAnyCase_Success(string connectionString)
	{
		new ConnectionStringValidator(connectionString).Validate();
	}

	[TestMethod]
	public void Validate_KeysDifferingOnlyByCase_ThrowsArgumentException()
	{
		Assert.ThrowsExactly<ArgumentException>(() =>
			new ConnectionStringValidator("AuthType=AD;Url=https://contoso:8080/Test;url=https://other;").Validate());
	}

	[TestMethod]
	public void TryValidate_ValidConnectionString_ReturnsTrue()
	{
		bool result = new ConnectionStringValidator(
			"AuthType=AD;Url=https://contoso:8080/Test;").TryValidate();

		Assert.IsTrue(result);
	}

	[TestMethod]
	public void TryValidate_InvalidConnectionString_ReturnsFalse()
	{
		bool result = new ConnectionStringValidator("AuthType=AD;").TryValidate();

		Assert.IsFalse(result);
	}

	[TestMethod]
	public void TryValidate_MalformedFormat_ReturnsFalse()
	{
		bool result = new ConnectionStringValidator("notavalidformat").TryValidate();

		Assert.IsFalse(result);
	}
}
