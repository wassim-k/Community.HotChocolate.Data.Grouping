---
title: Quick Start
---

# Quick Start

A walkthrough with a simplified `Employee` model from the test suite. Queries and results mirror the verified `GroupingTests` snapshots.

## 1. Define the Entity

```csharp
public record Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public bool Active { get; set; }
    public double Salary { get; set; }
    public decimal? Bonus { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public int? CompanyId { get; set; }
    public Company? Company { get; set; }
    public ICollection<Project>? Projects { get; set; }
    public ICollection<Skill>? Skills { get; set; }
}
```

`Department`, `Company`, `Project`, and `Skill` are plain navigations — no grouping annotations needed.

## 2. Expose a Query Field

```csharp
public class Query
{
    [UseGrouping]
    [UseFiltering]
    public IQueryable<Employee> GetEmployeeGrouping(MyDbContext db) => db.Employees;
}
```

<details>
<summary>Generated schema</summary>

```graphql
type Query {
  employeeGrouping(
    filterNullParent: Boolean = false
    where: EmployeeFilterInput
  ): [EmployeeGrouping!]!
}

type EmployeeGrouping {
  key: EmployeeGroupingKey!
  count(having: IntOperationFilterInput): Int!
  aggregate: EmployeeAggregate!
}

type EmployeeGroupingKey {
  id: Int
  name: String
  active: Boolean
  salary: Float
  bonus: Decimal
  departmentId: Int
  department: DepartmentGroupingKey
  companyId: Int
  company: CompanyGroupingKey
  projects: ProjectGroupingKey
  skills: SkillGroupingKey
}

type EmployeeAggregate {
  id: IntAggregateResult
  name: StringAggregateResult
  active: BooleanAggregateResult
  salary: FloatAggregateResult
  bonus: DecimalAggregateResult
  departmentId: IntAggregateResult
  department: DepartmentAggregate
  companyId: IntAggregateResult
  company: CompanyAggregate
  projects: ProjectAggregate
  skills: SkillAggregate
}

type IntAggregateResult {
  avg(having: FloatOperationFilterInput): Float
  sum(having: LongOperationFilterInput): Long
  min(having: IntOperationFilterInput): Int
  max(having: IntOperationFilterInput): Int
}

type FloatAggregateResult {
  avg(having: FloatOperationFilterInput): Float
  sum(having: FloatOperationFilterInput): Float
  min(having: FloatOperationFilterInput): Float
  max(having: FloatOperationFilterInput): Float
}

type DecimalAggregateResult {
  avg(having: DecimalOperationFilterInput): Decimal
  sum(having: DecimalOperationFilterInput): Decimal
  min(having: DecimalOperationFilterInput): Decimal
  max(having: DecimalOperationFilterInput): Decimal
}

type StringAggregateResult {
  min(having: StringOperationFilterInput): String
  max(having: StringOperationFilterInput): String
}

type BooleanAggregateResult {
  min(having: BooleanOperationFilterInput): Boolean
  max(having: BooleanOperationFilterInput): Boolean
}

# `*OperationFilterInput` types (from HotChocolate.Data) and `*FilterInput` from `[UseFiltering]`
# omitted for brevity. HAVING needs `AddFiltering()` registered alongside `AddGrouping()`; if it
# isn't, the `having:` arguments above are silently omitted from the generated schema.
```

</details>

## 3. Write a Grouping Query

The dimensions come from the `key` selection set — there is no separate `groupBy` argument.

```graphql
query {
  employeeGrouping {
    key {
      company { name }
      department { name }
    }
    count
  }
}
```

```json
[
  { "key": { "company": { "name": "Acme" },   "department": { "name": "Engineering" } }, "count": 2 },
  { "key": { "company": { "name": "Globex" }, "department": { "name": "Engineering" } }, "count": 2 },
  { "key": { "company": { "name": "Acme" },   "department": { "name": "Sales" } },       "count": 1 },
  { "key": { "company": { "name": "Globex" }, "department": { "name": "Sales" } },       "count": 1 },
  { "key": { "company": { "name": "Acme" },   "department": { "name": null } },          "count": 1 },
  { "key": { "company": { "name": "Globex" }, "department": { "name": null } },          "count": 1 }
]
```

The `department.name: null` buckets contain employees whose `Department` row exists with a null `Name`. To exclude employees whose `Department` navigation is itself null, pass `filterNullParent: true`.

## 4. Add Aggregates

Aggregates are field-first: `salary { avg sum min max }`, not `avg { salary }`.

```graphql
query {
  employeeGrouping {
    key { company { name } }
    count
    aggregate {
      salary { avg sum min max }
      bonus  { avg }
      department { budget { max } }               # nested navigation
    }
  }
}
```

```json
[
  {
    "key": { "company": { "name": "Acme" } },
    "count": 4,
    "aggregate": {
      "salary": { "avg": 90000, "sum": 360000, "min": 60000, "max": 120000 },
      "bonus":  { "avg": 15000 },
      "department": { "budget": { "max": 500000 } }
    }
  },
  {
    "key": { "company": { "name": "Globex" } },
    "count": 4,
    "aggregate": {
      "salary": { "avg": 86250, "sum": 345000, "min": 75000, "max": 95000 },
      "bonus":  { "avg": 5166.67 },
      "department": { "budget": { "max": 700000 } }
    }
  }
]
```

## 5. Filter Buckets with `having`

```graphql
query {
  employeeGrouping {
    key { company { name } }
    count(having: { gt: 2 })
    aggregate {
      salary { sum(having: { gt: 100000 }) avg }
    }
  }
}
```

See [Aggregations → HAVING](../examples/aggregations#filtering-buckets-with-having) for the operator list.

## 6. Filter Null Parents

```graphql
query {
  employeeGrouping(filterNullParent: true) {
    key { department { name } }
  }
}
```

`filterNullParent: true` drops employees whose `Department` navigation is null **before** the GROUP BY. Employees whose `Department` exists with a null `Name` still appear in the `{ "name": null }` bucket.
