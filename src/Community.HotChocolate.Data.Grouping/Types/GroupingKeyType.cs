using System.Reflection;
using HotChocolate.Configuration;
using HotChocolate.Data.Grouping.Convention;
using HotChocolate.Resolvers;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using HotChocolate.Types.Descriptors.Configurations;
using static HotChocolate.Data.Grouping.Expressions.ExpressionUtilities;

namespace HotChocolate.Data.Grouping.Types;

internal sealed class GroupingKeyType<T> : ObjectType
{
    private const string _suffix = "GroupingKey";

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

        definition.RuntimeType = typeof(GroupingFields);

        var convention = context.DescriptorContext.GetGroupingConvention(context.Scope);

        foreach (var member in context.TypeInspector.GetMembers(typeof(T)))
        {
            if (member is not PropertyInfo property)
            {
                continue;
            }

            var memberName = context.DescriptorContext.Naming.GetMemberName(
                property,
                MemberKind.ObjectField);

            var classification = MemberClassifier.Classify(property, convention);
            var typeRef = (ExtendedTypeReference)context.TypeInspector.GetOutputReturnTypeRef(member);
            var elementExtendedType = typeRef.Type.IsArrayOrList ? typeRef.Type.ElementType! : typeRef.Type;
            var elementType = IsNonNullType(elementExtendedType.Source)
                ? typeof(Nullable<>).MakeGenericType(elementExtendedType.Source)
                : elementExtendedType.Source;

            var outputType = classification.IsElementScalar
                ? elementType
                : typeof(GroupingKeyType<>).MakeGenericType(elementType);

            definition.Fields.Add(new ObjectFieldConfiguration
            {
                Name = memberName,
                Member = member,
                Type = context.TypeInspector.GetTypeRef(outputType, TypeContext.Output),
                PureResolver = ctx => ResolveField(ctx, property),
                ResolverType = typeof(GroupingFields),
            });
        }

        return definition;
    }

    /// <inheritdoc />
    protected override void OnRegisterDependencies(
        ITypeDiscoveryContext context,
        ObjectTypeConfiguration configuration)
    {
        base.OnRegisterDependencies(context, configuration);
        SetTypeIdentity(typeof(GroupingKeyType<>));

        configuration.Dependencies.Add(new TypeDependency(
            context.TypeInspector.GetTypeRef(typeof(T), TypeContext.Output),
            TypeDependencyFulfilled.Named));
    }

    private static object? ResolveField(IResolverContext ctx, PropertyInfo property)
    {
        var node = ctx.Parent<GroupingFields>();
        return node.Entries.TryGetValue(property, out var value) ? value : null;
    }
}
