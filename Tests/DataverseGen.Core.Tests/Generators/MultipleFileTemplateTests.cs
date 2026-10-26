using DataverseGen.Core.Generators.Scriban.Templates.TemplateFileManager;

namespace DataverseGen.Core.Tests.Generators;

[TestClass]
public class MultipleFileTemplateTests
{
	[TestMethod]
	public void Init_InvalidEntityTemplate_ThrowsWithFileName()
	{
		string baseDirectory = Path.Combine(Path.GetTempPath(), "dvgen-" + Guid.NewGuid().ToString("N"));
		string templateDirectory = Path.Combine(baseDirectory, "Templates", "Broken");
		Directory.CreateDirectory(templateDirectory);

		try
		{
			File.WriteAllText(Path.Combine(templateDirectory, "entity.sbncs"), "{{ for p in }}");
			File.WriteAllText(Path.Combine(templateDirectory, "enums.sbncs"), "ok");
			File.WriteAllText(Path.Combine(templateDirectory, "fields.sbncs"), "ok");
			File.WriteAllText(Path.Combine(templateDirectory, "xrmservicecontext.sbncs"), "ok");

			MultipleFileTemplateCsharp templates = new("Broken", "Templates", null, baseDirectory);

			InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => templates.Init());
			StringAssert.Contains(exception.Message, "entity.sbncs");
		}
		finally
		{
			Directory.Delete(baseDirectory, true);
		}
	}
}
