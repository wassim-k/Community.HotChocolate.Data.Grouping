---
title: Entity Configuration
---

# Entity Configuration

Field-level renames, ignores, and directives for the generated grouping types.

Each `[UseGrouping]` field produces three types per entity (`*Grouping`, `*GroupingKey`, `*Aggregate`). Rather than requiring a separate config class per generated type, the library:

- **inherits** field renames and ignores from the entity's `ObjectType<T>` by default — one declaration covers all three;
- offers `GroupingConfig<T>` as a single override surface for grouping-specific behaviour.

## Inheriting from `ObjectType<T>`

```csharp
public class WidgetType : ObjectType<Widget>
{
    protected override void Configure(IObjectTypeDescriptor<Widget> descriptor)
    {
        descriptor.Field(w => w.Label).Name("displayName");   // → displayName in key + aggregate
        descriptor.Ignore(w => w.InternalNotes);              // → hidden from all grouping types
    }
}
```

Renames and ignores propagate to every appearance of the field in the grouping types, including nested aggregates.

## Overriding with `GroupingConfig<T>`

For grouping-specific behaviour the entity's `ObjectType<T>` can't express:

```csharp
public class EmployeeGroupingConfig : GroupingConfig<Employee>
{
    protected override void Configure(IGroupingConfigDescriptor<Employee> descriptor)
    {
        descriptor.Field(e => e.InternalNotes).Ignore();                              // hide from grouping only
        descriptor.Field(e => e.Bonus).Name("performanceBonus");                      // rename in grouping only
        descriptor.Field(e => e.Salary).Directive(new AuthorizeDirective("payroll")); // attach a directive
    }
}
```

Register one call per config class:

```csharp
builder.Services
    .AddGraphQL()
    .AddGrouping()
    .AddGroupingConfig<EmployeeGroupingConfig>()
    .AddQueryType<Query>();
```

When both an `ObjectType<T>` and a `GroupingConfig<T>` exist, the `GroupingConfig<T>` wins for grouping types; the `ObjectType<T>` rename still applies to the entity.

### Bulk registration via source generator

For many `GroupingConfig<T>` subclasses, install the companion analyzer:

```xml
<PackageReference Include="Community.HotChocolate.Data.Grouping.Analyzers" Version="x.y.z" />
```

It emits an `AddGroupingConfigs()` extension that registers every config in the consumer's assembly:

```csharp
builder.Services
    .AddGraphQL()
    .AddGrouping()
    .AddGroupingConfigs()
    .AddQueryType<Query>();
```

The generator only scans the consumer's own assembly; configs in referenced libraries need explicit registration. With `[assembly: HotChocolate.Module("MyApp")]` the generated method is renamed to `AddMyAppGroupingConfigs()`.

The generator picks up non-abstract, non-generic subclasses (public or internal) with a public constructor — matching how HotChocolate activates types via `AddType<T>()`.

### Injecting services into a config

`GroupingConfig<T>` is activated through `ActivatorUtilities`, so it can take DI constructor parameters. Annotate the chosen ctor with `[ActivatorUtilitiesConstructor]` if there are multiple:

```csharp
public class EmployeeGroupingConfig : GroupingConfig<Employee>
{
    private readonly IFeatureGate _gate;

    [ActivatorUtilitiesConstructor]
    public EmployeeGroupingConfig(IFeatureGate gate) => _gate = gate;

    protected override void Configure(IGroupingConfigDescriptor<Employee> descriptor)
    {
        if (!_gate.IsEnabled(Feature.ExposeSalary))
        {
            descriptor.Field(e => e.Salary).Ignore();
        }
    }
}
```


## The `[UseGrouping]` Attribute

```csharp
[UseGrouping]
public IQueryable<Employee> GetEmployeeGrouping(MyDbContext db) => db.Employees;
```

Renames and ignores in the grouping schema are configured exclusively through `GroupingConfig<T>` (above).

### Scope

When two grouping conventions need to coexist:

```csharp
[UseGrouping(Scope = "reporting")]
public IQueryable<Employee> GetEmployeeGrouping(MyDbContext db) => db.Employees;

// or fluently:
descriptor.Field("employees").UseGrouping(scope: "reporting");
```

The scope gates which `GroupingConvention` (registered via `AddGrouping(scope: ...)`) is consulted.
