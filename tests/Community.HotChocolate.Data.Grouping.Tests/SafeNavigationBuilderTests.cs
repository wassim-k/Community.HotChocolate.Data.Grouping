using System.Linq.Expressions;
using System.Reflection;
using AwesomeAssertions;
using static HotChocolate.Data.Grouping.Execution.QueryableGrouping.SafeNavigationBuilder;

namespace HotChocolate.Data.Grouping;

public class SafeNavigationBuilderTests
{
    [Fact]
    public void NonNullableScalarChain_IdenticalForBothModes()
    {
        var (param, path) = Path<User>(
            (typeof(User), nameof(User.Department)),
            (typeof(Department), nameof(Department.Company)),
            (typeof(Company), nameof(Company.Name)));

        Build(param, path, inMemory: true).Should().Be("user.Department.Company.Name");
        Build(param, path, inMemory: false).Should().Be("user.Department.Company.Name");
    }

    [Fact]
    public void NullableScalarChain_GuardsInMemoryOnly()
    {
        var (param, path) = Path<User>(
            (typeof(User), nameof(User.DepartmentNullable)),
            (typeof(Department), nameof(Department.Company)),
            (typeof(Company), nameof(Company.Name)));

        Build(param, path, inMemory: true)
            .Should().Be("IIF((user.DepartmentNullable != null), user.DepartmentNullable.Company.Name, null)");
        Build(param, path, inMemory: false)
            .Should().Be("user.DepartmentNullable.Company.Name");
    }

    [Fact]
    public void NullableScalarChain_ConvertsValueTypeLeafToNullable()
    {
        var (param, path) = Path<User>(
            (typeof(User), nameof(User.DepartmentNullable)),
            (typeof(Department), nameof(Department.Id)));

        Build(param, path, inMemory: true)
            .Should().Be("IIF((user.DepartmentNullable != null), Convert(user.DepartmentNullable.Id, Nullable`1), null)");
        Build(param, path, inMemory: false)
            .Should().Be("user.DepartmentNullable.Id");
    }

    [Fact]
    public void CollectionHop_EmitsSelect()
    {
        var (param, path) = Path<Company>(
            (typeof(Company), nameof(Company.Departments)),
            (typeof(Department), nameof(Department.Name)));

        Build(param, path, inMemory: true).Should().Be("company.Departments.Select(item => item.Name)");
    }

    [Fact]
    public void NestedCollectionHops_EmitSelectMany_IdenticalForBothModes()
    {
        var (param, path) = Path<Company>(
            (typeof(Company), nameof(Company.Departments)),
            (typeof(Department), nameof(Department.Company)),
            (typeof(Company), nameof(Company.Departments)),
            (typeof(Department), nameof(Department.Employees)),
            (typeof(Employee), nameof(Employee.Name)));

        const string expected = "company.Departments.SelectMany(item => item.Company.Departments.SelectMany(item => item.Employees.Select(item => item.Name)))";
        Build(param, path, inMemory: true).Should().Be(expected);
        Build(param, path, inMemory: false).Should().Be(expected);
    }

    [Fact]
    public void NullableCollectionHop_GuardsInMemoryOnly()
    {
        var (param, path) = Path<User>(
            (typeof(User), nameof(User.DepartmentNullable)),
            (typeof(Department), nameof(Department.EmployeesNullable)),
            (typeof(Employee), nameof(Employee.Name)));

        Build(param, path, inMemory: true)
            .Should().Be("IIF((user.DepartmentNullable != null), IIF((user.DepartmentNullable.EmployeesNullable != null), user.DepartmentNullable.EmployeesNullable.Select(item => item.Name), Empty()), Empty())");
        Build(param, path, inMemory: false)
            .Should().Be("user.DepartmentNullable.EmployeesNullable.Select(item => item.Name)");
    }

    private static (ParameterExpression Param, PropertyInfo[] Path) Path<TRoot>(
        params (Type Owner, string Name)[] hops)
    {
        var param = Expression.Parameter(typeof(TRoot), typeof(TRoot).Name.ToLowerInvariant());
        var path = hops.Select(h => h.Owner.GetProperty(h.Name)!).ToArray();
        return (param, path);
    }

    private static string Build(ParameterExpression param, PropertyInfo[] path, bool inMemory) =>
        SafePropertyNavigation(param, path, inMemory).ToString();

    private record User
    {
        public Department Department { get; set; } = default!;
        public Department? DepartmentNullable { get; set; } = default!;
    }

    private record Department
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public Company Company { get; set; } = default!;
        public ICollection<Employee> Employees { get; set; } = default!;
        public ICollection<Employee>? EmployeesNullable { get; set; } = default!;
    }

    private record Company
    {
        public string Name { get; set; } = default!;
        public ICollection<Department> Departments { get; set; } = default!;
    }

    private record Employee
    {
        public string? Name { get; set; } = default!;
    }
}
