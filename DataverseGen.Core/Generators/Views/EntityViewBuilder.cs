using DataverseGen.Core.Metadata;

namespace DataverseGen.Core.Generators.Views;

internal static class EntityViewBuilder
{
	internal static EntityTemplateView BuildEntityView(Context context, MappingEntity entity)
	{
		return new EntityTemplateView
		{
			Namespace = context.Namespace,
			Info = context.Info,
			Entity = entity
		};
	}

	internal static Context BuildContextView(Context context)
	{
		return context;
	}
}
