using HotChocolate.Configuration;
using HotChocolate.Data.Grouping.Aggregates;
using HotChocolate.Data.Grouping.Naming;
using HotChocolate.Resolvers;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors.Configurations;

namespace HotChocolate.Data.Grouping.Types;

internal sealed class GroupingType<T> : ObjectType
{
    private const string _suffix = "Grouping";

    /// <inheritdoc />
    protected override void OnBeforeCompleteName(
        ITypeCompletionContext context,
        TypeSystemConfiguration configuration)
    {
        configuration.Name = GroupingTypeNaming.ResolveEntityName(context, typeof(T)) + _suffix;
        base.OnBeforeCompleteName(context, configuration);
    }

    /// <inheritdoc />
    protected override ObjectTypeConfiguration CreateConfiguration(ITypeDiscoveryContext context)
    {
        var definition = base.CreateConfiguration(context);

        definition.RuntimeType = typeof(IGroupingResult);

        definition.Fields.Add(new ObjectFieldConfiguration
        {
            Name = GroupingFieldNames.Key,
            Type = context.TypeInspector.GetTypeRef(
                typeof(NonNullType<>).MakeGenericType(typeof(GroupingKeyType<>).MakeGenericType(typeof(T))),
                TypeContext.Output),
            PureResolver = ResolveKey,
        });

        var countField = new ObjectFieldConfiguration
        {
            Name = GroupingFieldNames.Count,
            Type = context.TypeInspector.GetTypeRef(typeof(NonNullType<IntType>), TypeContext.Output),
            PureResolver = ResolveCount,
        };

        if (context.DescriptorContext.GetFilterTypeRef(typeof(int), context.Scope) is { } countHavingType)
        {
            countField.Arguments.Add(new ArgumentConfiguration
            {
                Name = GroupingArgumentNames.Having,
                Type = countHavingType,
            });
        }

        definition.Fields.Add(countField);

        definition.Fields.Add(new ObjectFieldConfiguration
        {
            Name = GroupingFieldNames.Aggregate,
            Type = context.TypeInspector.GetTypeRef(
                typeof(NonNullType<>).MakeGenericType(typeof(EntityAggregateType<>).MakeGenericType(typeof(T))),
                TypeContext.Output),
            PureResolver = ResolveAggregate,
        });

        return definition;
    }

    /// <inheritdoc />
    protected override void OnRegisterDependencies(
        ITypeDiscoveryContext context,
        ObjectTypeConfiguration configuration)
    {
        base.OnRegisterDependencies(context, configuration);
        SetTypeIdentity(typeof(GroupingType<>));

        configuration.Dependencies.Add(new TypeDependency(
            context.TypeInspector.GetTypeRef(typeof(T), TypeContext.Output),
            TypeDependencyFulfilled.Named));
    }

    private static object ResolveKey(IResolverContext ctx) => ctx.Parent<IGroupingResult>().Key;

    private static object ResolveCount(IResolverContext ctx) => ctx.Parent<IGroupingResult>().Count;

    private static object ResolveAggregate(IResolverContext ctx) => ctx.Parent<IGroupingResult>().Aggregate;
}
