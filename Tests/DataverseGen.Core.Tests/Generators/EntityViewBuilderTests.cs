using DataverseGen.Core.Generators.Views;
using DataverseGen.Core.Metadata;

namespace DataverseGen.Core.Tests.Generators;

[TestClass]
public class EntityViewBuilderTests
{
	[TestMethod]
	public void BuildEntityView_CopiesNamespaceInfoAndEntity()
	{
		Context context = new() { Namespace = "Test.Ns" };
		MappingEntity entity = new();

		EntityTemplateView view = EntityViewBuilder.BuildEntityView(context, entity);

		Assert.AreEqual("Test.Ns", view.Namespace);
		Assert.AreSame(context.Info, view.Info);
		Assert.AreSame(entity, view.Entity);
	}

	[TestMethod]
	public void BuildContextView_ReturnsContextUnchanged()
	{
		Context context = new() { Namespace = "Test.Ns" };

		Assert.AreSame(context, EntityViewBuilder.BuildContextView(context));
	}
}
