---
title: Installation
---

import Tabs from '@theme/Tabs';
import TabItem from '@theme/TabItem';

# Installation

## Prerequisites

- **.NET 8.0** or later
- **HotChocolate 16+**

## Add the NuGet Package

<Tabs groupId="package-manager">
  <TabItem value="cli" label=".NET CLI" default>

```bash
dotnet add package Community.HotChocolate.Data.Grouping
```

  </TabItem>
  <TabItem value="ps" label="Package Manager">

```powershell
Install-Package Community.HotChocolate.Data.Grouping
```

  </TabItem>
</Tabs>

## Register the Convention

```csharp {5}
using HotChocolate.Data.Grouping;

builder.Services
    .AddGraphQL()
    .AddGrouping()
    .AddFiltering()
    .AddQueryType<Query>();
```


See [Convention](../configuration/convention) for the inline and subclass variants.

## Decorate a Query Field

Mark any field returning `IQueryable<T>` or `IEnumerable<T>` with `[UseGrouping]`:

```csharp
public class Query
{
    [UseGrouping]
    public IQueryable<Employee> GetEmployeeGrouping(MyDbContext db) => db.Employees;
}
```

The middleware wraps the return type as `[EntityGrouping!]!`, adds a `filterNullParent: Boolean = false` argument, and composes with `[UseFiltering]` when present (`where` runs before the GROUP BY).
