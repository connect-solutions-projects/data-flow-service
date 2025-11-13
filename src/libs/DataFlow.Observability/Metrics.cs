using System.Diagnostics.Metrics;

namespace DataFlow.Observability;

public static class Metrics
{
    private static readonly Meter Meter = new("DataFlow", "1.0.0");

    public static readonly Counter<long> RateLimit429Counter = Meter.CreateCounter<long>(
        name: "dataflow_rate_limit_429_total",
        unit: "count",
        description: "Total de respostas 429 por rate limiting");

    public static readonly Counter<long> DeduplicationHitCounter = Meter.CreateCounter<long>(
        name: "dataflow_dedup_hit_total",
        unit: "count",
        description: "Total de conflitos de deduplicação por checksum");
}
