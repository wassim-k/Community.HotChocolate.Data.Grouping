using System.Text.RegularExpressions;
using AgileObjects.ReadableExpressions;
using HotChocolate.Data.Grouping.Execution.QueryableGrouping;
using HotChocolate.Data.Grouping.Fields;

namespace HotChocolate.Data.Grouping.Helpers;

public sealed class QueryableGroupingProviderDebug(ExpressionDebugCapture capture) : QueryableGroupingProvider
{
    private static readonly Regex _innerGenericsPattern = new(@"<[^<>]*>", RegexOptions.Compiled);

    protected override IQueryable BuildQuery<T>(
        IQueryable<T> source,
        SelectionPlan plan,
        bool filterNullParent,
        bool inMemory,
        IReadOnlyList<HavingPredicate> having)
    {
        var projected = base.BuildQuery(source, plan, filterNullParent, inMemory, having);
        capture.Expression = StripGenerics(projected.Expression.ToReadableString());
        return projected;
    }

    private static string StripGenerics(string text)
    {
        string previous;
        do
        {
            previous = text;
            text = _innerGenericsPattern.Replace(text, string.Empty);
        }
        while (text != previous);
        return text;
    }
}
