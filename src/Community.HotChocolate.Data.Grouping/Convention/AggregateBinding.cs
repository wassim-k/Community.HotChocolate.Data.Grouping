using System.ComponentModel;

namespace HotChocolate.Data.Grouping.Convention;

/// <summary>
/// CLR-runtime-type → <c>*AggregateResultType</c> mapping. Public only because it appears in
/// <see cref="GroupingConventionConfiguration.AggregateBindings"/>; consumers register bindings
/// through <see cref="IGroupingConventionDescriptor.BindRuntimeType{TRuntime, TResult}"/>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed record AggregateBinding(Type ResultType);
