namespace DataverseGen.Core.ConnectionString.Validators;

public class OAuthConnectionStringValidator : BaseConnectionStringValidator,
	IConnectionStringValidator
{
	public OAuthConnectionStringValidator(IDictionary<string, string> connectionStringTokens) :
		base(connectionStringTokens) { }

	public bool Validate()
	{
		CheckIfUrlIsPresentAndValid();
		CheckIfAppIdIsPresent();
		CheckIfRedirectUriIsPresent();
		CheckIfUserNameIsPresentWhenPasswordIsGiven();

		return true;
	}

	private void CheckIfUserNameIsPresentWhenPasswordIsGiven()
	{
		bool isPasswordTokenFound = ValidatorTokenHelper.TryCheckIfTokenIsPresentWithValue(ConnectionStringTokens,
			ConnectionStringConst.Password,
			out KeyValuePair<string, string> _);

		if (isPasswordTokenFound)
		{
			CheckIfUserNameIsPresent();
		}
	}

	private void CheckIfAppIdIsPresent()
	{
		ValidatorTokenHelper.CheckIfTokenIsPresentWithValue(ConnectionStringTokens,
			ConnectionStringConst.ClientId);
	}

	private void CheckIfRedirectUriIsPresent()
	{
		CheckIfTokenIsPresentWithValue(ConnectionStringConst.RedirectUri);
	}
}