using System.Collections.Immutable;
using Basic.Reference.Assemblies;
using HotChocolate.Data.Grouping.Config;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace HotChocolate.Data.Grouping.Analyzers.Tests;

internal static class GeneratorTestHelper
{
    private static readonly ImmutableArray<MetadataReference> _references =
    [
#if NET8_0
        ..Net80.References.All,
#elif NET9_0
        ..Net90.References.All,
#elif NET10_0
        ..Net100.References.All,
#endif
        MetadataReference.CreateFromFile(typeof(GroupingConfig).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(ModuleAttribute).Assembly.Location),
    ];

    public static GeneratorDriver Run(string source)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "AnalyzerTest",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: _references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return CSharpGeneratorDriver
            .Create(new GroupingConfigGenerator())
            .RunGenerators(compilation);
    }
}
