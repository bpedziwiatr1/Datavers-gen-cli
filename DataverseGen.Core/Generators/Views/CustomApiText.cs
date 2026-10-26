namespace DataverseGen.Core.Generators.Views;

internal static class CustomApiText
{
	internal static string EscapeString(string value)
	{
		return string.IsNullOrEmpty(value)
			? string.Empty
			: value.Replace("\\", "\\\\").Replace("\"", "\\\"");
	}
}
