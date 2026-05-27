using System.Reflection;
using HotChocolate.Configuration;
using HotChocolate.Data.Grouping.Convention;
using HotChocolate.Resolvers;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using HotChocolate.Types.Descriptors.Configurations;
using static HotChocolate.Data.Grouping.Expressions.ExpressionUtilities;

namespace HotChocolate.Data.Grouping.Types;

internal sealed class EntityAggregateType<T> : ObjectType
{
    private const string _suffix = "Aggregate";

    protected override void OnBeforeCompleteName(
        ITypeCompletionContext context,
        TypeSystemConfiguration configuration)
    {
        configuration.Name = GroupingTypeNaming.ResolveEntityName(context, typeof(T)) + _suffix;
        base.OnBeforeCompleteName(context, configuration);
    }

    protected override ObjectTypeConfiguration CreateConfiguration(ITypeDiscoveryContext context)
    {
        var definition = base.CreateConfiguration(context);
        definition.RuntimeType = typeof(GroupingFields);

        var convention = context.DescriptorContext.GetGroupingConvention(context.Scope);

        foreach (var member in context.TypeInspector.GetMembers(typeof(T)))
        {
            if (member is not PropertyInfo property)
            {
                continue;
            }

            var classification = MemberClassifier.Classify(property, convention);
            var typeRef = (ExtendedTypeReference)context.TypeInspector.GetOutputReturnTypeRef(member);
            var elementSource = typeRef.Type.IsArrayOrList ? typeRef.Type.ElementType!.Source : typeRef.Type.Source;
            var nullableElementSource = IsNonNullType(elementSource)
                ? typeof(Nullable<>).MakeGenericType(elementSource)
                : elementSource;

            Type? schemaTypeClr;
            if (classification.IsElementScalar)
            {
                schemaTypeClr = convention.ResolveAggregateResultType(elementSource);
                if (schemaTypeClr is null)
                {
                    continue;
                }
            }
            else
            {
                if (!Execution.QueryableGrouping.GroupingHelpers.HasLeaf(
                    nullableElementSource,
                    convention,
                    t => convention.ResolveAggregateResultType(t) is not null))
                {
                    continue;
                }

                schemaTypeClr = typeof(EntityAggregateType<>).MakeGenericType(nullableElementSource);
            }

            definition.Fields.Add(new ObjectFieldConfiguration
            {
                Name = context.DescriptorContext.Naming.GetMemberName(property, MemberKind.ObjectField),
                Member = member,
                Type = context.TypeInspector.GetTypeRef(schemaTypeClr, TypeContext.Output),
                PureResolver = ctx => ResolveField(ctx, property),
            });
        }

        return definition;
    }

    protected override void OnRegisterDependencies(
        ITypeDiscoveryContext context,
        ObjectTypeConfiguration configuration)
    {
        base.OnRegisterDependencies(context, configuration);
        SetTypeIdentity(typeof(EntityAggregateType<>));

        configuration.Dependencies.Add(new TypeDependency(
            context.TypeInspector.GetTypeRef(typeof(T), TypeContext.Output),
            TypeDependencyFulfilled.Named));
    }

    private static object? ResolveField(IResolverContext ctx, PropertyInfo property) =>
        ctx.Parent<GroupingFields>().Entries.TryGetValue(property, out var value) ? value : null;
}
