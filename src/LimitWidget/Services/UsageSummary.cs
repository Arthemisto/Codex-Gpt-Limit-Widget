using System.Globalization;
using System.Text.Json;

namespace LimitWidget.Services;

public sealed record UsageSummary(long? LifetimeTokens)
{
    public string DisplayValue => LifetimeTokens?.ToString("N0", CultureInfo.CurrentCulture) ?? "—";
    public static UsageSummary Parse(JsonElement response)
    {
        if (response.TryGetProperty("summary", out var summary) && summary.ValueKind == JsonValueKind.Object &&
            summary.TryGetProperty("lifetimeTokens", out var tokens) && tokens.ValueKind == JsonValueKind.Number &&
            tokens.TryGetInt64(out long value) && value >= 0) return new(value);
        return new((long?)null);
    }
}
