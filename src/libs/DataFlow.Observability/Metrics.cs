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

    public static readonly Histogram<double> BatchDurationSeconds = Meter.CreateHistogram<double>(
        name: "dataflow_batch_duration_seconds",
        unit: "s",
        description: "Duração do processamento de batches em segundos");

    public static readonly Counter<long> BatchTotal = Meter.CreateCounter<long>(
        name: "dataflow_batch_total",
        unit: "count",
        description: "Total de batches processados");

    public static readonly Counter<long> ChunkTotal = Meter.CreateCounter<long>(
        name: "dataflow_chunk_total",
        unit: "count",
        description: "Total de chunks enviados");

    public static readonly Counter<long> ChunkErrorsTotal = Meter.CreateCounter<long>(
        name: "dataflow_chunk_errors_total",
        unit: "count",
        description: "Total de erros ao enviar chunks");

    public static readonly Gauge<long> BatchesProcessing = Meter.CreateGauge<long>(
        name: "dataflow_batches_processing",
        unit: "count",
        description: "Número de batches em processamento");

    public static readonly Counter<long> WebhookFailuresTotal = Meter.CreateCounter<long>(
        name: "dataflow_webhook_failures_total",
        unit: "count",
        description: "Total de falhas de entrega de webhooks");

    public static readonly Counter<long> WebhookDeliveriesTotal = Meter.CreateCounter<long>(
        name: "dataflow_webhook_deliveries_total",
        unit: "count",
        description: "Total de webhooks entregues com sucesso");

    public static readonly Counter<long> RetentionRunsTotal = Meter.CreateCounter<long>(
        name: "dataflow_retention_runs_total",
        unit: "count",
        description: "Total de execuções do job de retenção");

    public static readonly Counter<long> RetentionBatchesDeletedTotal = Meter.CreateCounter<long>(
        name: "dataflow_retention_batches_deleted_total",
        unit: "count",
        description: "Total de batches removidos pelo job de retenção");
}
