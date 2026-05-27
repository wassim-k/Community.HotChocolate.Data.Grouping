using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using VerifyXunit;
using Xunit;

namespace HotChocolate.Data.Grouping.Analyzers.Tests;

public class GroupingConfigGeneratorTests
{
    // === Emission tests (snapshot-verified) ===

    [Fact]
    public Task Single_public_config()
    {
        var driver = GeneratorTestHelper.Run("""
            using HotChocolate.Data.Grouping.Config;
            namespace App;
            public class Employee {}
            public class EmployeeGroupingConfig : GroupingConfig<Employee>
            {
                protected override void Configure(IGroupingConfigDescriptor<Employee> d) {}
            }
            """);

        return Verifier.Verify(driver).UseDirectory("Snapshots");
    }

    [Fact]
    public Task Multiple_configs_sorted_and_deduplicated()
    {
        var driver = GeneratorTestHelper.Run("""
            using HotChocolate.Data.Grouping.Config;
            namespace App;
            public class A {}
            public class B {}
            public class ZGroupingConfig : GroupingConfig<A>
            {
                protected override void Configure(IGroupingConfigDescriptor<A> d) {}
            }
            public class AGroupingConfig : GroupingConfig<B>
            {
                protected override void Configure(IGroupingConfigDescriptor<B> d) {}
            }
            """);

        return Verifier.Verify(driver).UseDirectory("Snapshots");
    }

    [Fact]
    public Task Internal_subclass_included()
    {
        var driver = GeneratorTestHelper.Run("""
            using HotChocolate.Data.Grouping.Config;
            namespace App;
            public class Entity {}
            internal class InternalConfig : GroupingConfig<Entity>
            {
                protected override void Configure(IGroupingConfigDescriptor<Entity> d) {}
            }
            """);

        return Verifier.Verify(driver).UseDirectory("Snapshots");
    }

    [Fact]
    public Task Subclass_with_dependency_injected_ctor_included()
    {
        var driver = GeneratorTestHelper.Run("""
            using HotChocolate.Data.Grouping.Config;
            namespace App;
            public interface IFeatureGate {}
            public class Entity {}
            public class InjectedConfig : GroupingConfig<Entity>
            {
                public InjectedConfig(IFeatureGate gate) {}
                protected override void Configure(IGroupingConfigDescriptor<Entity> d) {}
            }
            """);

        return Verifier.Verify(driver).UseDirectory("Snapshots");
    }

    [Fact]
    public Task Module_attribute_uses_name_as_method_suffix()
    {
        // HotChocolate.ModuleAttribute, when present at the assembly level, names the
        // generated method `Add{ModuleName}GroupingConfigs` instead of the default
        // `AddGroupingConfigs`.
        var driver = GeneratorTestHelper.Run("""
            using HotChocolate;
            using HotChocolate.Data.Grouping.Config;

            [assembly: Module("MyApp")]

            namespace App;
            public class Employee {}
            public class EmployeeGroupingConfig : GroupingConfig<Employee>
            {
                protected override void Configure(IGroupingConfigDescriptor<Employee> d) {}
            }
            """);

        return Verifier.Verify(driver).UseDirectory("Snapshots");
    }

    // === Exclusion tests (explicit empty-output assertion) ===
    //
    // Verify writes no snapshot file when the generator emits nothing, so silent passes
    // would be possible. These tests assert empty output explicitly so the regression
    // signal is "test fails" if any of these inputs ever start producing source.

    [Fact]
    public void Abstract_subclass_excluded() => AssertNoSourcesEmitted("""
        using HotChocolate.Data.Grouping.Config;
        namespace App;
        public class Entity {}
        public abstract class AbstractConfig : GroupingConfig<Entity>
        {
            protected override void Configure(IGroupingConfigDescriptor<Entity> d) {}
        }
        """);

    [Fact]
    public void Open_generic_subclass_excluded() => AssertNoSourcesEmitted("""
        using HotChocolate.Data.Grouping.Config;
        namespace App;
        public class GenericConfig<T> : GroupingConfig<T> where T : class, new()
        {
            protected override void Configure(IGroupingConfigDescriptor<T> d) {}
        }
        """);

    [Fact]
    public void No_subclasses_in_compilation() => AssertNoSourcesEmitted("""
        namespace App;
        public class Unrelated {}
        """);

    [Fact]
    public void Private_nested_subclass_excluded() => AssertNoSourcesEmitted("""
        using HotChocolate.Data.Grouping.Config;
        namespace App;
        public class Entity {}
        public class Outer
        {
            private class PrivateConfig : GroupingConfig<Entity>
            {
                protected override void Configure(IGroupingConfigDescriptor<Entity> d) {}
            }
        }
        """);

    [Fact]
    public void Subclass_with_only_non_public_ctor_excluded() => AssertNoSourcesEmitted("""
        using HotChocolate.Data.Grouping.Config;
        namespace App;
        public class Entity {}
        public class PrivateCtorConfig : GroupingConfig<Entity>
        {
            private PrivateCtorConfig() {}
            protected override void Configure(IGroupingConfigDescriptor<Entity> d) {}
        }
        """);

    private static void AssertNoSourcesEmitted(string source)
    {
        var driver = GeneratorTestHelper.Run(source);
        var sources = driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources);
        sources.Should().BeEmpty();
    }
}
