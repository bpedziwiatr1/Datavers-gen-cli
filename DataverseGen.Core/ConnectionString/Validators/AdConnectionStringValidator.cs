namespace DataverseGen.Core.ConnectionString.Validators;

internal class AdConnectionStringValidator : BaseConnectionStringValidator,
	IConnectionStringValidator
{
	public AdConnectionStringValidator(IDictionary<string, string> connectionStringTokens) :
		base(connectionStringTokens) { }

	public bool Validate()
	{
		CheckIfUrlIsPresentAndValid();

		return true;
	}
}