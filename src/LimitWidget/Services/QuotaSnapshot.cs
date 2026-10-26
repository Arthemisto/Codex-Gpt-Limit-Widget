using System.Text.Json;
using System.IO;

namespace LimitWidget.Services;

public sealed record QuotaWindow(double RemainingPercent, int? DurationMinutes, DateTimeOffset? ResetsAt)
{
    public string Label => DurationMinutes switch
    {
        10080 => "Weekly",
        1440 => "Daily",
        > 0 when DurationMinutes % 60 == 0 => $"{DurationMinutes / 60}h window",
        > 0 => $"{DurationMinutes}m window",
        _ => "Current window"
    };
}

public sealed record QuotaSnapshot(QuotaWindow? Primary, QuotaWindow? Secondary, DateTimeOffset UpdatedAt)
{
    public static QuotaSnapshot Parse(JsonElement response)
    {
        JsonElement bucket;
        if (response.TryGetProperty("rateLimitsByLimitId", out var buckets) && buckets.ValueKind == JsonValueKind.Object && buckets.TryGetProperty("codex", out bucket)) { }
        else if (response.TryGetProperty("rateLimits", out bucket) && bucket.ValueKind == JsonValueKind.Object &&
                 (!bucket.TryGetProperty("limitId", out var id) || id.ValueKind == JsonValueKind.Null || id.GetString() == "codex")) { }
        else throw new InvalidDataException("Codex quota is not available for this account.");

        if (bucket.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Codex quota is not available for this account.");
        var primary = ReadWindow(bucket, "primary");
        var secondary = ReadWindow(bucket, "secondary");
        if (primary == null && secondary == null) throw new InvalidDataException("The service returned no Codex quota windows.");
        return new(primary, secondary, DateTimeOffset.Now);
    }

    static QuotaWindow? ReadWindow(JsonElement bucket, string name)
    {
        if (!bucket.TryGetProperty(name, out var window) || window.ValueKind != JsonValueKind.Object) return null;
        if (!window.TryGetProperty("usedPercent", out var used) || used.ValueKind != JsonValueKind.Number || !used.TryGetDouble(out double percent) || !double.IsFinite(percent)) return null;
        int? duration = window.TryGetProperty("windowDurationMins", out var d) && d.ValueKind == JsonValueKind.Number && d.TryGetInt32(out int mins) && mins > 0 ? mins : null;
        DateTimeOffset? reset = null;
        if (window.TryGetProperty("resetsAt", out var r) && r.ValueKind == JsonValueKind.Number && r.TryGetInt64(out long timestamp))
        {
            try { reset = DateTimeOffset.FromUnixTimeSeconds(timestamp); } catch (ArgumentOutOfRangeException) { }
        }
        return new(Math.Clamp(100 - percent, 0, 100), duration, reset);
    }
}
