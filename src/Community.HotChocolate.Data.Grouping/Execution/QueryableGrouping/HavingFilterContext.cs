using HotChocolate.Data.Filters;
using HotChocolate.Data.Filters.Expressions;
using HotChocolate.Internal;

namespace HotChocolate.Data.Grouping.Execution.QueryableGrouping;

/// <summary>
/// Subclass of HotChocolate.Data's <see cref="QueryableFilterContext"/> that overrides the operand parameter
/// type. HotChocolate's stock context types the visitor's lambda parameter as <c>initialType.EntityType</c> —
/// for an <c>*OperationFilterInputType</c> that's the filter class itself, not the slot's CLR type.
/// HAVING needs the parameter typed to the carrier slot (<c>int?</c> for Count, <c>double?</c> for
/// Avg(int), etc.) so operator handlers can build well-typed <c>slot op value</c> expressions.
/// Pops HotChocolate's default scope + runtime-type and pushes ones keyed on the slot type.
/// </summary>
internal sealed class HavingFilterContext : QueryableFilterContext
{
    public HavingFilterContext(IFilterInputType initialType, IExtendedType operandType, bool inMemory)
        : base(initialType, inMemory)
    {
        Scopes.Pop();
        Scopes.Push(new QueryableScope(operandType, "_s0", inMemory));
        RuntimeTypes.Pop();
        RuntimeTypes.Push(operandType);
    }
}
