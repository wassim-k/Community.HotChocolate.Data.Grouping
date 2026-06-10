using System.Data.Common;
using HotChocolate;
using HotChocolate.Data;
using HotChocolate.Data.Grouping.Convention;
using HotChocolate.Data.Grouping.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoSandbox;

namespace HotChocolate.Data.Grouping.Fixtures;

public class GroupingTestFixture : IAsyncDisposable
{
    private static readonly Employee _alice = new()
    {
        Id = 1,
        Name = "Alice",
        Active = true,
        Salary = 100_000d,
        Bonus = 10_000m,
        DepartmentId = 1,
        Department = new Department { Id = 1, Name = "Engineering", Budget = 500_000m },
        CompanyId = 1,
        Company = new Company { Id = 1, Name = "Acme", NoOfEmployees = 50 },
        Projects =
        [
            new Project
            {
                Id = 1, Name = "Alpha", Budget = 10_000m, EmployeeId = 1,
                Tasks =
                [
                    new Task { Id = 101, Name = "T1A", EstimatedHours = 5m, ProjectId = 1 },
                    new Task { Id = 102, Name = "T1B", EstimatedHours = 8m, ProjectId = 1 },
                ],
            },
            new Project
            {
                Id = 2, Name = "Beta", Budget = 20_000m, EmployeeId = 1,
                Tasks =
                [
                    new Task { Id = 103, Name = "T2A", EstimatedHours = 3m, ProjectId = 2 },
                ],
            },
        ],
        Skills =
        [
            new Skill { Id = 1, Name = "C#", Level = 5, EmployeeId = 1 },
            new Skill { Id = 2, Name = "Python", Level = 3, EmployeeId = 1 },
        ],
    };

    private static readonly Employee _carol = new()
    {
        Id = 3,
        Name = "Carol",
        Active = true,
        Salary = 120_000d,
        Bonus = 20_000m,
        DepartmentId = 2,
        Department = new Department { Id = 2, Name = "Sales", Budget = 300_000m },
        CompanyId = 1,
        Company = new Company { Id = 1, Name = "Acme", NoOfEmployees = 50 },
        Projects =
        [
            new Project
            {
                Id = 4, Name = "Gamma", Budget = 30_000m, EmployeeId = 3,
                Tasks =
                [
                    new Task { Id = 104, Name = "T4A", EstimatedHours = 10m, ProjectId = 4 },
                    new Task { Id = 105, Name = "T4B", EstimatedHours = 7m, ProjectId = 4 },
                    new Task { Id = 106, Name = "T4C", EstimatedHours = 12m, ProjectId = 4 },
                ],
            },
            new Project { Id = 5, Name = "Delta", Budget = 25_000m, EmployeeId = 3, Tasks = [] },
            new Project { Id = 6, Name = "Beta", Budget = 5_000m, EmployeeId = 3, Tasks = [] },
        ],
    };

    private static readonly Employee[] _employees =
    [
        _alice,
        new Employee
        {
            Id = 2,
            Name = "Bob",
            Active = true,
            Salary = 80_000d,
            Bonus = null,
            DepartmentId = 1,
            Department = new Department { Id = 1, Name = "Engineering", Budget = 500_000m },
            CompanyId = 1,
            Company = new Company { Id = 1, Name = "Acme", NoOfEmployees = 50 },
            ManagerId = 1,
            Manager = _alice,
            Projects =
            [
                new Project { Id = 3, Name = "Alpha", Budget = 15_000m, EmployeeId = 2, Tasks = [] },
            ],
            Skills =
            [
                new Skill { Id = 3, Name = "C#", Level = 4, EmployeeId = 2 },
            ],
        },
        _carol,
        new Employee
        {
            Id = 4,
            Name = "Dave",
            Active = true,
            Salary = 90_000d,
            Bonus = 5_000m,
            DepartmentId = 3,
            Department = new Department { Id = 3, Name = "Engineering", Budget = 700_000m },
            CompanyId = 2,
            Company = new Company { Id = 2, Name = "Globex", NoOfEmployees = 200 },
            ManagerId = 3,
            Manager = _carol,
            Projects =
            [
                new Project { Id = 7, Name = "Alpha", Budget = 12_000m, EmployeeId = 4, Tasks = [] },
            ],
        },
        new Employee
        {
            Id = 5,
            Name = "Eve",
            Active = true,
            Salary = 95_000d,
            Bonus = null,
            DepartmentId = 3,
            Department = new Department { Id = 3, Name = "Engineering", Budget = 700_000m },
            CompanyId = 2,
            Company = new Company { Id = 2, Name = "Globex", NoOfEmployees = 200 },
            ManagerId = 3,
            Manager = _carol,
            Projects = null,
        },
        new Employee
        {
            Id = 6,
            Name = "Frank",
            Salary = 75_000d,
            Bonus = 7_500m,
            DepartmentId = 4,
            Department = new Department { Id = 4, Name = "Sales", Budget = 400_000m },
            CompanyId = 2,
            Company = new Company { Id = 2, Name = "Globex", NoOfEmployees = 200 },
            Projects = [],
        },
        new Employee
        {
            Id = 7,
            Name = "Grace",
            Salary = 60_000d,
            Bonus = null,
            DepartmentId = null,
            Department = null,
            CompanyId = 1,
            Company = new Company { Id = 1, Name = "Acme", NoOfEmployees = 50 },
            Projects =
            [
                new Project { Id = 10, Name = "Alpha", Budget = 8_000m, EmployeeId = 7, Tasks = [] },
            ],
        },
        new Employee
        {
            Id = 8,
            Name = "Henry",
            Salary = 85_000d,
            Bonus = 3_000m,
            DepartmentId = 5,
            Department = new Department { Id = 5, Name = null, Budget = 200_000m },
            CompanyId = 2,
            Company = new Company { Id = 2, Name = "Globex", NoOfEmployees = 200 },
            ManagerId = 3,
            Manager = _carol,
            Projects =
            [
                new Project { Id = 11, Name = "Beta", Budget = 9_000m, EmployeeId = 8, Tasks = [] },
            ],
        },
    ];

    public GroupingTestFixture()
    {
        ServiceProvider = BuildServiceProvider();
    }

    public ServiceProvider ServiceProvider { get; }

    public async ValueTask DisposeAsync()
    {
        await ServiceProvider.DisposeAsync();
    }

    private ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services
            .AddSingleton<DbConnection>(sp => new SqliteConnection("Filename=:memory:"))
            .AddDbContext<MyDbContext>((sp, options) => options
                .UseLoggerFactory(LoggerFactory.Create(builder => builder.AddDebug()))
                .UseSqlite(sp.GetRequiredService<DbConnection>()));

        services
            .AddSingleton(sp => MongoRunner.Run())
            .AddSingleton<IMongoClient>(sp => new MongoClient(MongoClientSettings.FromConnectionString(sp.GetRequiredService<IMongoRunner>().ConnectionString)));

        services.AddSingleton<ExpressionDebugCapture>();

        services
            .AddGraphQL()
            .ConfigureSchemaServices((appServices, sp) => sp.AddSingleton(appServices.GetRequiredService<ExpressionDebugCapture>()))
            .AddFiltering()
            .AddSorting()
            .AddGrouping(d => d.AddDefaults().GroupingProvider<QueryableGroupingProviderDebug>())
            .AddQueryType<Query>();

        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MyDbContext>();
        dbContext.Database.OpenConnection();
        dbContext.Database.EnsureCreated();

        var mongoCollection = serviceProvider.GetRequiredService<IMongoClient>().GetDatabase("DB").GetCollection<Employee>("Employees");
        mongoCollection.InsertMany(_employees);

        return serviceProvider;
    }

    public class Query
    {
        [UseGrouping]
        [UseFiltering]
        public IQueryable<Employee> GetMemoryEmployeeGrouping() => _employees.AsQueryable();

        [UseGrouping]
        [UseFiltering]
        public IQueryable<Employee> GetSqlEmployeeGrouping(MyDbContext dbContext) => dbContext.Employees;

        [UseGrouping]
        [UseFiltering]
        public IQueryable<Employee> GetMongoEmployeeGrouping(IMongoClient mongoClient) =>
            mongoClient.GetDatabase("DB").GetCollection<Employee>("Employees").AsQueryable();
    }

    public class MyDbContext(DbContextOptions<MyDbContext> options) : DbContext(options)
    {
        public DbSet<Employee> Employees { get; set; } = default!;

        public DbSet<Department> Departments { get; set; } = default!;

        public DbSet<Company> Companies { get; set; } = default!;

        public DbSet<Project> Projects { get; set; } = default!;

        public DbSet<Skill> Skills { get; set; } = default!;

        public DbSet<Task> Tasks { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Employee>()
                .HasOne(e => e.Department)
                .WithMany();

            modelBuilder.Entity<Employee>()
                .HasOne(e => e.Company)
                .WithMany();

            modelBuilder.Entity<Employee>()
                .HasMany(e => e.Projects)
                .WithOne()
                .HasForeignKey(p => p.EmployeeId);

            modelBuilder.Entity<Employee>()
                .HasMany(e => e.Skills)
                .WithOne()
                .HasForeignKey(s => s.EmployeeId);

            modelBuilder.Entity<Employee>()
                .HasOne(e => e.Manager)
                .WithMany()
                .HasForeignKey(e => e.ManagerId);

            modelBuilder.Entity<Project>()
                .HasMany(p => p.Tasks)
                .WithOne()
                .HasForeignKey(t => t.ProjectId);

            modelBuilder.Entity<Department>().HasData(
                _employees.Select(e => e.Department).OfType<Department>().DistinctBy(d => d.Id).ToList());
            modelBuilder.Entity<Company>().HasData(
                _employees.Select(e => e.Company).OfType<Company>().DistinctBy(c => c.Id).ToList());
            modelBuilder.Entity<Employee>().HasData(
                _employees.Select(e => e with { Department = null, Company = null, Projects = null, Skills = null, Manager = null }).ToList());
            modelBuilder.Entity<Project>().HasData(
                _employees.SelectMany(e => e.Projects ?? []).Select(p => p with { Tasks = null }).ToList());
            modelBuilder.Entity<Skill>().HasData(
                _employees.SelectMany(e => e.Skills ?? []).ToList());
            modelBuilder.Entity<Task>().HasData(
                _employees.SelectMany(e => e.Projects ?? []).SelectMany(p => p.Tasks ?? []).ToList());
        }
    }

    public record Employee
    {
        public int Id { get; set; }

        public string Name { get; set; } = default!;

        public bool Active { get; set; }

        public double Salary { get; set; }

        public decimal? Bonus { get; set; }

        public int? DepartmentId { get; set; }

        public virtual Department? Department { get; set; } = default!;

        public int? CompanyId { get; set; }

        public virtual Company? Company { get; set; } = default!;

        public virtual ICollection<Project>? Projects { get; set; }

        public virtual ICollection<Skill>? Skills { get; set; }

        public int? ManagerId { get; set; }

        public virtual Employee? Manager { get; set; }
    }

    public record Project
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public decimal Budget { get; set; }

        public int EmployeeId { get; set; }

        public virtual ICollection<Task>? Tasks { get; set; }
    }

    public record Skill
    {
        public int Id { get; set; }

        public string Name { get; set; } = default!;

        public int Level { get; set; }

        public int EmployeeId { get; set; }
    }

    public record Task
    {
        public int Id { get; set; }

        public string Name { get; set; } = default!;

        public decimal EstimatedHours { get; set; }

        public int ProjectId { get; set; }
    }

    public record Department
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public decimal Budget { get; set; }
    }

    public record Company
    {
        public int Id { get; set; }

        public string Name { get; set; } = default!;

        public int NoOfEmployees { get; set; }
    }
}
