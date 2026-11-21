namespace CSharpSense.Server;

/// <summary>Configurable latency budgets. All values are in milliseconds.</summary>
public static class LatencyBudget
{
    /// <summary>Warm-path p95 target for a completion response.</summary>
    public static int WarmP95Ms { get; set; } = 150;

    /// <summary>Warm-path p99 target for a completion response.</summary>
    public static int WarmP99Ms { get; set; } = 300;

    /// <summary>Cold-start target for a small solution (≤5 projects).</summary>
    public static int ColdStartSmallMs { get; set; } = 3_000;

    /// <summary>Cold-start target for a large solution (50 projects).</summary>
    public static int ColdStartLargeMs { get; set; } = 15_000;
}
