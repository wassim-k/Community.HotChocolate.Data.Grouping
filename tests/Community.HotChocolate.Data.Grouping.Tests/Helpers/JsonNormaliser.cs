using System.Text.Json;
using System.Text.Json.Nodes;

namespace HotChocolate.Data.Grouping.Helpers;

/// <summary>
/// Canonicalises result JSON so snapshots are stable across providers: object keys are preserved
/// but arrays are order-independent (sorted by their serialised form) and numbers are rounded to two
/// decimals as <see cref="double"/>, smoothing over LINQ / EF Core / Mongo numeric-shape differences.
/// </summary>
internal static class JsonNormaliser
{
    public static JsonNode? Normalise(JsonNode? node) => node switch
    {
        JsonObject obj => obj.Aggregate(new JsonObject(), (acc, kvp) =>
        {
            acc[kvp.Key] = Normalise(kvp.Value);
            return acc;
        }),
        JsonArray arr => new JsonArray([.. arr.Select(Normalise).OrderBy(i => i?.ToJsonString())]),
        JsonValue v when v.GetValueKind() == JsonValueKind.Number && v.TryGetValue<decimal>(out var d)
            => JsonValue.Create((double)Math.Round(d, 2, MidpointRounding.AwayFromZero)),
        _ => node?.DeepClone(),
    };
}
