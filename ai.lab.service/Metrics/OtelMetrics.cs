using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ai.lab.service.Metrics;

public class OtelMetrics
{
    public Meter Meter { get; }

    public string MeterName { get; }

    public readonly Counter<long> ServiceCallCounter;

    public readonly Counter<long> ServiceCallErrorCounter;

    public readonly Counter<long> ServiceCallSuccessCounter;

    public readonly Counter<long> MariaDbCallCounter;

    public readonly Counter<long> QdrantDbCallCounter;

    public readonly Counter<long> TotalNumberOfActiveChats;

    public readonly Histogram<long> ServiceCallDuration;

    public readonly Histogram<long> QdrantDatabaseCallDuration;

    public readonly ActivitySource ActivitySource = new("AI.Lab.Service");

    public static readonly double[] histogramBuckets = new double[] { 0, 5, 10, 25, 50, 75, 100, 250, 500, 1000, double.PositiveInfinity };

    public OtelMetrics(string meterName = "AI.Lab.Service")
    {
        this.MeterName = meterName;
        this.Meter = new Meter(meterName, "1.0.0");

        ServiceCallCounter =
            Meter.CreateCounter<long>("ws_service_call_count", description: "Counts the number of service calls");
        TotalNumberOfActiveChats =
            Meter.CreateCounter<long>("ai_lab_total_active_chats", description: "Counts the total number of active chats");
        ServiceCallErrorCounter =
            Meter.CreateCounter<long>("ws_service_call_error_count", description: "Counts the number of service call errors");
        ServiceCallSuccessCounter =
            Meter.CreateCounter<long>("ws_service_call_success_count", description: "Counts the number of successful service calls");
        QdrantDbCallCounter =
            Meter.CreateCounter<long>("al_lab_qdrant_db_call_count", description: "Counts the total amount of calls made to qdrant (from user interaction)");
        MariaDbCallCounter =
            Meter.CreateCounter<long>("ai_lab_maria_db_call_count", description: "Counts the total amount of calls made to MariaDB (from user interaction)");

        ServiceCallDuration = Meter.CreateHistogram<long>("ws_service_call_duration_ticks", description: "Records the duration of service calls in ticks");

        QdrantDatabaseCallDuration = Meter.CreateHistogram<long>("ai_qdrant_database_call_duration_ticks", description: "Records the duration of database calls in ticks");
    }
}
