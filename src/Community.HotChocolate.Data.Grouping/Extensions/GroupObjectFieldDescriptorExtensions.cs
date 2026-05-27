using System.Reflection;
using HotChocolate.Configuration;
using HotChocolate.Data.Grouping.Convention;
using HotChocolate.Data.Grouping.Execution;
using HotChocolate.Data.Grouping.Naming;
using HotChocolate.Data.Grouping.Types;
using HotChocolate.Language;
using HotChocolate.Resolvers;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors.Configurations;
using static HotChocolate.Data.Grouping.ThrowHelper;

namespace HotChocolate.Data.Grouping.Extensions;

/// <summary>Grouping extensions on <see cref="IObjectFieldDescriptor"/>.</summary>
public static class GroupObjectFieldDescriptorExtensions
{
    private static readonly MethodInfo _factoryTemplate =
        typeof(GroupObjectFieldDescriptorExtensions)
            .GetMethod(nameof(CreateMiddleware), BindingFlags.Static | BindingFlags.NonPublic)!;

    /// <summary>
    /// Registers the grouping middleware and arguments on the field.
    /// </summary>
    public static IObjectFieldDescriptor UseGrouping(
        this IObjectFieldDescriptor descriptor,
        string? scope = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var placeholder = new FieldMiddlewareConfiguration(_ => _ => default);
        descriptor.Extend().Configuration.MiddlewareConfigurations.Add(placeholder);

        descriptor.Argument(
            GroupingArgumentNames.FilterNullParent,
            a => a.Type<BooleanType>().DefaultValue(false));

        descriptor
            .Extend()
            .OnBeforeCreate(
                (context, definition) =>
                {
                    if (definition.ResultType is null
                        || !context.TypeInspector.TryCreateTypeInfo(
                            definition.ResultType,
                            out var typeInfo))
                    {
                        throw GroupObjectFieldDescriptorExtensions_CannotInfer(
                            definition.ResolverType ?? typeof(object));
                    }

                    var schemaType = typeof(NonNullType<>).MakeGenericType(
                        typeof(ListType<>).MakeGenericType(
                            typeof(NonNullType<>).MakeGenericType(
                                typeof(GroupingType<>).MakeGenericType(typeInfo.NamedType))));

                    definition.Type = context.TypeInspector.GetTypeRef(schemaType);

                    var entityType = typeInfo.NamedType;

                    definition.Tasks.Add(
                        new OnCompleteTypeSystemConfigurationTask<ObjectFieldConfiguration>(
                            (typeContext, d) =>
                                CompileMiddleware(typeContext, d, entityType, placeholder, scope),
                            definition,
                            ApplyConfigurationOn.BeforeCompletion));
                });

        return descriptor;
    }

    private static void CompileMiddleware(
        ITypeCompletionContext context,
        ObjectFieldConfiguration definition,
        Type entityType,
        FieldMiddlewareConfiguration placeholder,
        string? scope)
    {
        var convention = context.DescriptorContext.GetGroupingConvention(scope);

        var filterNullParentArg = definition.Arguments
            .FirstOrDefault(a => a.Name == GroupingArgumentNames.FilterNullParent);

        if (filterNullParentArg is not null)
        {
            filterNullParentArg.RuntimeDefaultValue = convention.DefaultFilterNullParent;
            filterNullParentArg.DefaultValue = new BooleanValueNode(convention.DefaultFilterNullParent);
        }

        var factory = _factoryTemplate.MakeGenericMethod(entityType);
        var middleware = (FieldMiddleware)factory.Invoke(null, [convention])!;

        var index = definition.MiddlewareConfigurations.IndexOf(placeholder);
        definition.MiddlewareConfigurations[index] = new FieldMiddlewareConfiguration(middleware);
    }

    private static FieldMiddleware CreateMiddleware<TEntity>(IGroupingConvention convention) =>
        next =>
        {
            var middleware = new GroupingMiddleware<TEntity>(next, convention);
            return async ctx => await middleware.InvokeAsync(ctx).ConfigureAwait(false);
        };
}
