using System.Linq.Expressions;
using AwesomeAssertions;
using HotChocolate.Data.Grouping.Execution.QueryableGrouping;
using HotChocolate.Data.Grouping.Fields;

namespace HotChocolate.Data.Grouping;

public class GroupingHelpersTests
{
    [Fact]
    public void CollectionPrefix_NullableNavThenNonNullableCollection_InMemoryGuardsWithEmpty()
    {
        var result = BuildPrefixAccess<Employee>(
            inMemory: true,
            (typeof(Employee), nameof(Employee.Manager), PathSegmentKind.ObjectNavigation),
            (typeof(Manager), nameof(Manager.Projects), PathSegmentKind.ObjectCollection));

        result.Type.Should().Be(typeof(IEnumerable<Project>));
        result.ToString().Should().Be("IIF((employee.Manager != null), employee.Manager.Projects, Empty())");
    }

    [Fact]
    public void CollectionPrefix_NullableNavThenNullableCollection_InMemoryGuardsBothHops()
    {
        var result = BuildPrefixAccess<Employee>(
            inMemory: true,
            (typeof(Employee), nameof(Employee.Manager), PathSegmentKind.ObjectNavigation),
            (typeof(Manager), nameof(Manager.ProjectsNullable), PathSegmentKind.ObjectCollection));

        result.Type.Should().Be(typeof(IEnumerable<Project>));
        result.ToString().Should().Be(
            "IIF((employee.Manager != null), (employee.Manager.ProjectsNullable ?? Empty()), Empty())");
    }

    [Fact]
    public void CollectionPrefix_NullableNavThenNonNullableCollection_DbModeAccessesRaw()
    {
        var result = BuildPrefixAccess<Employee>(
            inMemory: false,
            (typeof(Employee), nameof(Employee.Manager), PathSegmentKind.ObjectNavigation),
            (typeof(Manager), nameof(Manager.Projects), PathSegmentKind.ObjectCollection));

        result.ToString().Should().Be("employee.Manager.Projects");
    }

    private static Expression BuildPrefixAccess<TRoot>(
        bool inMemory,
        params (Type Owner, string Name, PathSegmentKind Kind)[] hops)
    {
        var root = Expression.Parameter(typeof(TRoot), typeof(TRoot).Name.ToLowerInvariant());
        var prefix = hops
            .Select(h => new PathSegment(h.Owner.GetProperty(h.Name)!, h.Kind))
            .ToArray();

        return GroupingHelpers.BuildCollectionPrefixAccess(root, prefix, inMemory, typeof(Project));
    }

    private record Project
    {
        public string Name { get; set; } = default!;
    }

    private record Manager
    {
        public List<Project> Projects { get; set; } = [];
        public List<Project>? ProjectsNullable { get; set; }
    }

    private record Employee
    {
        public Manager? Manager { get; set; }
    }
}
