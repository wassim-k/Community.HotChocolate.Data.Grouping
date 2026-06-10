using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HotChocolate.Data.Grouping.Fixtures;
using HotChocolate.Data.Grouping.Helpers;
using HotChocolate.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace HotChocolate.Data.Grouping;

public partial class GroupingTests(GroupingTestFixture fixture) : IClassFixture<GroupingTestFixture>
{
    [Fact]
    public async Task SchemaTest()
    {
        var executor = await fixture.ServiceProvider.GetRequestExecutorAsync();

        await Verify(executor.Schema.ToString())
            .UseDirectory("__snapshots__");
    }

    [Theory]
    [MemberData(nameof(MiddlewareTest_Data))]
    public async Task MiddlewareTest(string name, string query)
    {
        var executor = await fixture.ServiceProvider.GetRequestExecutorAsync();
        var debugCapture = fixture.ServiceProvider.GetRequiredService<ExpressionDebugCapture>();

        string? memoryExpression = null;
        string? dbExpression = null;
        string? dbExpressionSource = null;
        string? canonicalResult = null;
        string? canonicalResultSource = null;

        foreach (var source in new[] { "memory", "sql", "mongo" })
        {
            var field = $"{source}EmployeeGrouping";

            var executionResult = await executor.ExecuteAsync(
                OperationRequestBuilder.New()
                    .SetDocument(query.Replace("employeeGrouping", field, StringComparison.Ordinal))
                    .Build());

            if (executionResult is OperationResult { Errors: { Count: > 0 } errors })
            {
                var details = string.Join(" | ", errors.Select(e => $"{e.Message}: {e.Exception?.GetType().Name} {e.Exception?.Message}"));
                throw new Exception($"[{source}] GraphQL execution resulted in errors: {details}");
            }

            if (source == "memory")
            {
                memoryExpression = debugCapture.Expression;
            }
            else
            {
                var normalised = NormaliseDbExpressionRoot(debugCapture.Expression);
                if (dbExpression is null)
                {
                    dbExpression = normalised;
                    dbExpressionSource = source;
                }
                else if (!string.Equals(dbExpression, normalised, StringComparison.Ordinal))
                {
                    throw new Exception(
                        $"[{name}] DB expression diverged between providers.\n" +
                        $"--- {dbExpressionSource} ---\n{dbExpression}\n\n" +
                        $"--- {source} ---\n{normalised}");
                }
            }

            // Result: normalise decimal precision drift (memory keeps full precision,
            // SQLite truncates to 2dp) then assert all three providers produce identical JSON.
            if (executionResult is OperationResult result && (result.Errors is null || result.Errors.Count == 0) &&
                JsonNode.Parse(result.ToJson())?["data"]?[field] is JsonArray array)
            {
                var normalisedJson = JsonNormaliser.Normalise(array)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

                if (canonicalResult is null)
                {
                    canonicalResult = normalisedJson;
                    canonicalResultSource = source;
                }
                else if (!string.Equals(canonicalResult, normalisedJson, StringComparison.Ordinal))
                {
                    throw new Exception(
                        $"[{name}] Result diverged between providers (post-normalisation).\n" +
                        $"--- {canonicalResultSource} ---\n{canonicalResult}\n\n" +
                        $"--- {source} ---\n{normalisedJson}");
                }
            }
        }

        if (memoryExpression is not null)
        {
            await Verify(memoryExpression)
                .UseDirectory("__snapshots__")
                .UseFileName($"{name}.expression.memory");
        }

        if (dbExpression is not null)
        {
            await Verify(dbExpression)
                .UseDirectory("__snapshots__")
                .UseFileName($"{name}.expression.db");
        }

        if (canonicalResult is not null)
        {
            await Verify(canonicalResult)
                .UseDirectory("__snapshots__")
                .UseFileName($"{name}.result");
        }
    }

    [Theory]
    [InlineData(
        "sum over projects without projects in key",
        """
        query EmployeeGrouping {
            employeeGrouping {
                key { department { name } }
                aggregate { projects { budget { sum } } }
            }
        }
        """)]
    [InlineData(
        "count + sum over projects, projects not in key",
        """
        query EmployeeGrouping {
            employeeGrouping {
                key { department { name } }
                count
                aggregate { projects { budget { sum } } }
            }
        }
        """)]
    [InlineData(
        "key flattens projects, aggregate touches skills",
        """
        query EmployeeGrouping {
            employeeGrouping {
                key { projects { name } }
                aggregate { skills { level { max } } }
            }
        }
        """)]
    [InlineData(
        "key flattens projects, aggregate dives into unkeyed nested tasks",
        """
        query EmployeeGrouping {
            employeeGrouping {
                key { projects { name } }
                aggregate { projects { tasks { estimatedHours { sum } } } }
            }
        }
        """)]
    [InlineData(
        "no key, aggregate touches a collection",
        """
        query EmployeeGrouping {
            employeeGrouping {
                aggregate { projects { budget { sum } } }
            }
        }
        """)]
    public async Task MiddlewareTest_RejectsAggregateCollectionMissingFromKey(string scenario, string query)
    {
        var executor = await fixture.ServiceProvider.GetRequestExecutorAsync();

        foreach (var source in new[] { "memory", "sql", "mongo" })
        {
            var field = $"{source}EmployeeGrouping";
            var result = await executor.ExecuteAsync(
                OperationRequestBuilder.New()
                    .SetDocument(query.Replace("employeeGrouping", field, StringComparison.Ordinal))
                    .Build());

            if (result is not OperationResult op || op.Errors is null || op.Errors.Count == 0)
            {
                throw new Exception($"[{scenario}/{source}] expected error {GroupingErrorCodes.AggregateCollectionMissingFromKey}; got data.");
            }
            if (!op.Errors.Any(e => e.Code == GroupingErrorCodes.AggregateCollectionMissingFromKey))
            {
                var details = string.Join(" | ", op.Errors.Select(e => $"{e.Code}: {e.Message}"));
                throw new Exception($"[{scenario}/{source}] expected {GroupingErrorCodes.AggregateCollectionMissingFromKey}; got: {details}");
            }
        }
    }

    public static TheoryData<string, string> MiddlewareTest_Data => new()
    {
        {
            "KeyOnly",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key {
                        department { name }
                    }
                }
            }
            """
        },
        {
            // No `key` selected — the entire source collapses into a single bucket and
            // aggregates run over all rows. SelectionPlan.KeyPaths is empty; the
            // middleware substitutes a single empty path so the provider builds a
            // constant key.
            "AggregateOnlyNoKey",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    count
                    aggregate {
                        salary { avg sum min max }
                    }
                }
            }
            """
        },
        {
            "KeyWithCount",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key {
                        company { name }
                        department { name }
                    }
                    count
                }
            }
            """
        },
        {
            "NestedAggregate",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key {
                        company { name }
                    }
                    count
                    aggregate {
                        department { budget { min } }
                        company { noOfEmployees { max } }
                    }
                }
            }
            """
        },
        {
            // Aggregates over a nullable numeric leaf (`Bonus: decimal?`). Some employees
            // have non-null bonuses, some null — exercises the null-skipping behaviour of
            // LINQ's Average/Sum/Min/Max on nullable inputs.
            "NullableNumericLeaf",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key {
                        company { name }
                    }
                    count
                    aggregate {
                        bonus { avg sum min max }
                    }
                }
            }
            """
        },
        {
            // Grouping by both `departmentId` and `department.name` distinguishes:
            //   (null,        null)     — employee has no department at all
            //   (id,          null)     — employee has a department whose Name is null
            //   (id,          "Engineering")
            //   (id,          "Sales")
            // i.e. a missing ancestor produces a different bucket than an explicit null leaf.
            "NullableParentVsLeaf",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key {
                        departmentId
                        department { name }
                    }
                    count
                }
            }
            """
        },
        {
            "NullableNavigationCollectionKey",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key {
                        manager { projects { name } }
                    }
                    count
                }
            }
            """
        },
        {
            // Group by a leaf reached through TWO chained collection navigations
            // (`Employee.Projects.Tasks.Name`). Each (employee, project, task) tuple is a
            // row after the two SelectMany hops, so each bucket holds the tasks sharing
            // the same name across all projects of all employees. Exercises the
            // multi-prefix flatten with a nested-collection prefix.
            "ChainedCollectionKey",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key {
                        projects { tasks { name } }
                    }
                    count
                }
            }
            """
        },
        {
            // Group by a leaf inside a collection navigation. Each (employee, project)
            // pair becomes a row after flattening, so each bucket holds the projects
            // sharing the same name. Employees with no projects are absent from results.
            "KeyByCollectionLeaf",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key {
                        projects { name }
                    }
                    count
                }
            }
            """
        },
        {
            // Mixed scalar + collection key: per-(project-name, company-name) buckets.
            "KeyByCollectionMixedScalar",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key {
                        projects { name }
                        company { name }
                    }
                    count
                }
            }
            """
        },
        {
            // Collection key paired with aggregates over the original entity. Salary is
            // counted once per (employee, project) flattened row — i.e. an employee with
            // 2 projects contributes twice to the average. This matches SQL JOIN semantics.
            "KeyByCollectionWithAggregates",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key {
                        projects { name }
                    }
                    count
                    aggregate {
                        salary { avg sum }
                    }
                }
            }
            """
        },
        {
            // All paths cross the same collection: key on projects.name, aggregates on
            // projects.budget. Every leaf is on the flattened Project side.
            "KeyByCollectionAggregateCollection",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key {
                        projects { name }
                    }
                    count
                    aggregate {
                        projects { budget { avg sum min max } }
                    }
                }
            }
            """
        },
        {
            // Mixed: collection key plus aggregates that span both sides — salary on the
            // Employee parent, budget on the Project element. Tests the tuple-wrapped row.
            "KeyByCollectionMixedAggregates",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key {
                        projects { name }
                    }
                    count
                    aggregate {
                        salary { avg }
                        projects { budget { sum } }
                    }
                }
            }
            """
        },
        {
            // filterNullParent in scalar mode: parent `[Department]` for path
            // `[Department, Name]` must be non-null before grouping. Grace has a null
            // Department and drops out entirely; Henry's Department is non-null
            // (its Name is null, but that's the leaf, not the parent) so he stays in
            // the `null` bucket.
            "ScalarParentNullExcluded",
            """
            query EmployeeGrouping {
                employeeGrouping(filterNullParent: true) {
                    key {
                        department { name }
                    }
                }
            }
            """
        },
        {
            // filterNullParent in pure-flatten mode: parent path `[Projects]` equals
            // the SelectMany prefix, so it rebases to nothing — the filter is a no-op
            // and the result matches KeyByCollectionLeaf. One case covers the rebase
            // collapse; the mixed and multi-collection variants exercise the same
            // code path and were dropped during dedup.
            "CollectionParentNullExcluded",
            """
            query EmployeeGrouping {
                employeeGrouping(filterNullParent: true) {
                    key {
                        projects { name }
                    }
                    count
                }
            }
            """
        },
        {
            // Two parallel collection navigations (Projects + Skills). Each employee
            // contributes |Projects| × |Skills| rows after the chained SelectMany.
            "KeyByMultipleCollections",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key {
                        projects { name }
                        skills { name }
                    }
                    count
                }
            }
            """
        },
        {
            "KeyWithAllAggregates",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key {
                        company { name }
                        department { name }
                    }
                    count
                    aggregate {
                        salary { avg sum }
                        company { noOfEmployees { min } }
                        department { budget { max } }
                    }
                }
            }
            """
        },
        {
            // Aggregate over a leaf reached through TWO chained collection navigations
            // (Projects.Tasks.EstimatedHours). The aggregate must rebase onto the deepest
            // unwound element slot of the multi-prefix carrier — depth-2 collection aggregation
            // was previously exercised only as a key, never as an aggregate target.
            "ChainedCollectionAggregate",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key {
                        projects { tasks { name } }
                    }
                    count
                    aggregate {
                        projects { tasks { estimatedHours { avg sum min max } } }
                    }
                }
            }
            """
        },
        {
            // Sum/Avg over an int leaf (Skills.Level) exercises the small-integral widening
            // (Sum int -> long?, Avg int -> double?) end-to-end across all three providers,
            // including the int->long Convert the providers must translate.
            "AggregateIntWidening",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key {
                        skills { name }
                    }
                    count
                    aggregate {
                        skills { level { avg sum min max } }
                    }
                }
            }
            """
        },
        {
            // The whole selection arrives via a fragment spread, an inline fragment, and a
            // nested fragment on the key type. Selection parsing walks the compiled operation,
            // so all three must resolve identically to spelling the fields out inline.
            "FragmentSelection",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    ...groupFields
                    ... on EmployeeGrouping {
                        count
                    }
                }
            }
            fragment groupFields on EmployeeGrouping {
                key {
                    ...keyFields
                }
                aggregate {
                    salary { avg }
                }
            }
            fragment keyFields on EmployeeGroupingKey {
                department { name }
            }
            """
        },
        {
            // The same `key` response field selected twice — GraphQL field merging requires
            // the union of both selection sets to contribute to the grouping dimensions.
            "DuplicateKeyFieldsMerge",
            """
            query EmployeeGrouping {
                employeeGrouping {
                    key {
                        company { name }
                    }
                    key {
                        department { name }
                    }
                    count
                }
            }
            """
        },
    };

    [GeneratedRegex(@"^(?:\[Microsoft\.EntityFrameworkCore\.Query\.EntityQueryRootExpression\]|MongoQuery)")]
    private static partial Regex DbRootPattern();

    private static string NormaliseDbExpressionRoot(string expression) =>
        DbRootPattern().Replace(expression, "[Source]", count: 1);

}
