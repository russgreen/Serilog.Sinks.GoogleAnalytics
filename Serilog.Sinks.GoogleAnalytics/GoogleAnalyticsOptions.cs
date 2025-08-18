using System;
using System.Collections.Generic;

namespace Serilog.Sinks.GoogleAnalytics;
public class GoogleAnalyticsOptions
{
    public string MeasurementId { get; set; } = default!;
    public string ApiSecret { get; set; } = default!;
    public string ClientId { get; set; }  // e.g. () => Machine GUID or a random stable UUID
    public Func<Serilog.Events.LogEvent, string>? EventNameResolver { get; set; }
    public Func<Serilog.Events.LogEvent, bool>? IncludePredicate { get; set; }
    public bool IncludeExceptionDetails { get; set; } = true;
    public bool NonPersonalizedAds { get; set; } = false;
    public int MaxEventsPerRequest { get; set; } = 20; // keep below GA's 25 to leave headroom
    public int MaxParamValueLength { get; set; } = 300;
    public Dictionary<string, object?> GlobalParams { get; set; } = new();
    public bool MapLevelToParam { get; set; } = true;
    public TimeSpan FlushPeriod { get; set; } = TimeSpan.FromSeconds(5);
    public int BatchSizeLimit { get; set; } = 40; // batches subdivided before send
    public int RetryCount { get; set; } = 2;
}
