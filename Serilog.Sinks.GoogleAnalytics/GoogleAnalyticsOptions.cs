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

    // NEW: control serializing LogEvent.Properties into GA params
    public bool IncludeLogEventProperties { get; set; } = false;

    // If set, only these property names are included; otherwise all properties are considered.
    public ISet<string>? IncludedPropertyNames { get; set; }

    // Optional custom mapping for property names (Serilog name -> GA param name).
    public Func<string, string>? PropertyNameFormatter { get; set; }

    // Flatten Serilog structures (StructureValue/DictionaryValue) into separate GA parameters.
    // If false, complex values are stringified into a single parameter.
    public bool FlattenStructuredProperties { get; set; } = true;

    // When flattening, join nested names with this separator, e.g. "request_method".
    public string FlattenSeparator { get; set; } = "_";

    // GA4 event parameter name max length is 40.
    public int MaxParamNameLength { get; set; } = 40;

    // Limit how many parameters we add from LogEvent properties to avoid exceeding GA limits.
    public int MaxPropertyParamsPerEvent { get; set; } = 10;
}
