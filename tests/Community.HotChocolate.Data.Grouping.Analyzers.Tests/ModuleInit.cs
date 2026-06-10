using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace HotChocolate.Data.Grouping.Analyzers.Tests;

public static class ModuleInit
{
    // Matches the version literal inside `[GeneratedCode("Community.HotChocolate.Data.Grouping.Analyzers", "x.y.z")]`.
    private static readonly Regex _generatedCodeVersion = new(
        @"(GeneratedCode\(""Community\.HotChocolate\.Data\.Grouping\.Analyzers"", "")[^""]+("")",
        RegexOptions.Compiled);

    [ModuleInitializer]
    public static void Init()
    {
        VerifySourceGenerators.Initialize();
        // The analyzer's assembly version flows from the NuGet package version, which
        // changes on every release. Scrub it from snapshots so they survive bumps.
        VerifierSettings.AddScrubber(builder =>
        {
            var scrubbed = _generatedCodeVersion.Replace(builder.ToString(), "$1{ScrubbedVersion}$2");
            builder.Clear();
            builder.Append(scrubbed);
        });
    }
}
