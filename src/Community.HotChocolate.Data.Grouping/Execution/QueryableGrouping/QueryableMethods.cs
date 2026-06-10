using System.Reflection;

namespace HotChocolate.Data.Grouping.Execution.QueryableGrouping;

/// <summary>
/// Cached <see cref="Queryable"/> method definitions shared by the provider and shape builders.
/// </summary>
internal static class QueryableMethods
{
    public static MethodInfo Where { get; } =
        FindBySelectorArity(nameof(Queryable.Where), parameterCount: 2, selectorArity: 2);

    public static MethodInfo Select { get; } =
        FindBySelectorArity(nameof(Queryable.Select), parameterCount: 2, selectorArity: 2);

    public static MethodInfo SelectMany { get; } =
        FindBySelectorArity(nameof(Queryable.SelectMany), parameterCount: 2, selectorArity: 2);

    public static MethodInfo SelectManyWithResult { get; } =
        FindBySelectorArity(nameof(Queryable.SelectMany), parameterCount: 3, selectorArity: 2);

    public static MethodInfo GroupBy { get; } = typeof(Queryable).GetMethods()
        .Single(m =>
            m.Name == nameof(Queryable.GroupBy)
            && m.GetParameters().Length == 2
            && m.GetGenericArguments().Length == 2);

    private static MethodInfo FindBySelectorArity(string name, int parameterCount, int selectorArity) =>
        typeof(Queryable).GetMethods().Single(m =>
            m.Name == name
            && m.GetParameters().Length == parameterCount
            && m.GetParameters()[1].ParameterType
                .GetGenericArguments()[0]
                .GetGenericArguments().Length == selectorArity);
}
