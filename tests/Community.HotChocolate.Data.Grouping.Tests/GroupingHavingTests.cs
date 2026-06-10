#pragma warning disable SA1201 // Elements should appear in the correct order

using System.Text.Json;
using System.Text.Json.Nodes;
using HotChocolate.Data.Grouping.Fixtures;
using HotChocolate.Data.Grouping.Helpers;
using HotChocolate.Execution;

namespace HotChocolate.Data.Grouping;

/// <summary>
/// End-to-end HAVING coverage. Each scenario fires the same query against all three
/// providers and snapshots only the (provider-independent) result JSON, since the
/// HAVING <c>Where</c> composes on the projected carrier and the resulting expression
/// trees naturally diverge in shape across LINQ / EF Core / Mongo.
/// </summary>
public class GroupingHavingTests(GroupingTestFixture fixture) : IClassFixture<GroupingTestFixture>
{
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task HavingTest(string name, string query, IReadOnlyDictionary<string, object?>? variables)
    {
        var executor = await fixture.ServiceProvider.GetRequestExecutorAsync();

        string? canonicalResult = null;
        string? canonicalResultSource = null;

        foreach (var source in new[] { "memory", "sql", "mongo" })
        {
            var field = $"{source}EmployeeGrouping";
            var requestBuilder = OperationRequestBuilder.New()
                .SetDocument(query.Replace("employeeGrouping", field, StringComparison.Ordinal));

            if (variables is not null)
            {
                requestBuilder.SetVariableValues(variables);
            }

            var executionResult = await executor.ExecuteAsync(requestBuilder.Build());

            if (executionResult is OperationResult { Errors: { Count: > 0 } errors })
            {
                var details = string.Join(" | ", errors.Select(e => $"{e.Message}: {e.Exception?.GetType().Name} {e.Exception?.Message}"));
                throw new Exception($"[{name}/{source}] GraphQL execution resulted in errors: {details}");
            }

            if (executionResult is not OperationResult result)
            {
                throw new Exception($"[{name}/{source}] unexpected result type: {executionResult.GetType().Name}");
            }

            if (JsonNode.Parse(result.ToJson())?["data"]?[field] is not JsonArray array)
            {
                throw new Exception($"[{name}/{source}] expected a grouping array at '{field}' but got: {result.ToJson()}");
            }

            var normalisedJson = JsonNormaliser.Normalise(array)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

            if (canonicalResult is null)
            {
                canonicalResult = normalisedJson;
                canonicalResultSource = source;
            }
            else if (!string.Equals(canonicalResult, normalisedJson, StringComparison.Ordinal))
            {
                throw new Exception(
                    $"[{name}] Result diverged between providers.\n" +
                    $"--- {canonicalResultSource} ---\n{canonicalResult}\n\n" +
                    $"--- {source} ---\n{normalisedJson}");
            }
        }

        await Verify(canonicalResult)
            .UseDirectory("__snapshots__")
            .UseFileName($"Having_{name}.result");
    }

    public static TheoryData<string, string, IReadOnlyDictionary<string, object?>?> Cases() => new()
    {
        {
            "CountGt",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key { company { name } }
                    count(having: { gt: 2 })
                }
            }
            """,
            null
        },
        {
            "SumGt",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key { company { name } }
                    aggregate { salary { sum(having: { gt: 100000 }) } }
                }
            }
            """,
            null
        },
        {
            "AvgLte",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key { company { name } }
                    aggregate { salary { avg(having: { lte: 80000 }) } }
                }
            }
            """,
            null
        },
        {
            "RangeAnd",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key { company { name } }
                    count(having: { gt: 1, lt: 5 })
                }
            }
            """,
            null
        },
        {
            // Bonus is decimal? — some employees have it, some don't. `eq: null` must
            // match buckets whose Avg(bonus) is null (i.e. the bucket had no non-null
            // bonus contributors). Verifies the null-literal handling in the compiler
            // produces consistent semantics across providers.
            "EqNull",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key { company { name } }
                    aggregate { bonus { avg(having: { eq: null }) } }
                }
            }
            """,
            null
        },
        {
            // `nin` against a list must NOT match null aggregate slots — matches SQL's
            // NOT IN semantics. Without the null guard, EF and in-memory diverge.
            "NinNullSafe",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key { company { name } }
                    aggregate { bonus { sum(having: { nin: [0, 1000, 5000] }) } }
                }
            }
            """,
            null
        },
        {
            // Multiple HAVING clauses on different aggregate fields are AND-combined at the bucket level
            // (each compiles into its own predicate; ApplyHaving folds them with AndAlso into one Where).
            // Acme: count=4 ✓, sum(salary)=360000 ✓; Globex: count=4 ✓, sum(salary)=345000 ✗.
            "MultiClauseAcrossAggregates",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key { company { name } }
                    count(having: { gt: 3 })
                    aggregate { salary { sum(having: { gt: 350000 }) } }
                }
            }
            """,
            null
        },
        {
            // HAVING on Min — Min/Max are the only ops available on comparable (non-numeric) result
            // types. Acme min(salary)=60000 (Grace) ✓; Globex min=75000 (Frank) ✗.
            "MinHaving",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key { company { name } }
                    aggregate { salary { min(having: { lt: 70000 }) } }
                }
            }
            """,
            null
        },
        {
            // HAVING on Max with the inclusive gte variant. Acme max=120000 ✓; Globex max=95000 ✗.
            "MaxHavingGte",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key { company { name } }
                    aggregate { salary { max(having: { gte: 100000 }) } }
                }
            }
            """,
            null
        },
        {
            // HAVING on a string aggregate slot using `startsWith`. Verifies HotChocolate.Data's
            // StringOperationFilterInputType handlers dispatch on a string slot at runtime.
            // Acme min(name)="Alice" ✓; Globex min(name)="Dave" ✗.
            "StringMinHavingStartsWith",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key { company { name } }
                    aggregate { name { min(having: { startsWith: "A" }) } }
                }
            }
            """,
            null
        },
        {
            // Positive `in: [...]` (sibling of NinNullSafe). Acme sum(bonus)=30000 ✓;
            // Globex sum(bonus)=15500 ✗.
            "InPositive",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key { company { name } }
                    aggregate { bonus { sum(having: { in: [30000, 99999] }) } }
                }
            }
            """,
            null
        },
        {
            // HAVING that prunes all buckets — result must be an empty list, no exception.
            // Guards the "no clause-passes" path through ApplyHaving + Materialise.
            "PrunesAllBuckets",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key { company { name } }
                    count(having: { gt: 999 })
                }
            }
            """,
            null
        },
        {
            // HAVING value supplied through a GraphQL variable rather than an inline literal. Exercises
            // the middleware's VariableRewriter path; result must match the inline CountGt case.
            "WithVariable",
            """
            query EmployeeGrouping($having: IntOperationFilterInput) {
                employeeGrouping {
                    key { company { name } }
                    count(having: $having)
                }
            }
            """,
            new Dictionary<string, object?> { ["having"] = new Dictionary<string, object?> { ["gt"] = 2 } }
        },
        {
            // A having variable that's omitted (optional, unset) must mean "no constraint" — every
            // bucket returned, not an error. Regression for the variable-rewrite null path.
            "NullVariableOmitted",
            """
            query EmployeeGrouping($having: IntOperationFilterInput) {
                employeeGrouping {
                    key { company { name } }
                    count(having: $having)
                }
            }
            """,
            null
        },
        {
            // Same, but the having variable is supplied explicitly as null.
            "NullVariableExplicit",
            """
            query EmployeeGrouping($having: IntOperationFilterInput) {
                employeeGrouping {
                    key { company { name } }
                    count(having: $having)
                }
            }
            """,
            new Dictionary<string, object?> { ["having"] = null }
        },
    };
}
