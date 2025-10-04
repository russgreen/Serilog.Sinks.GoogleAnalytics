# Serilog.Sinks.GoogleAnalytics

Send Serilog log events to Google Analytics 4 (GA4) via the Measurement Protocol.

This sink batches log events and posts them as GA4 events, enabling lightweight operational or product usage telemetry without running your own ingestion stack.

> GA4 Measurement Protocol is designed for analytics, not for high-volume diagnostic logging. Use this sink for low/medium volume, non-personal, aggregate insights.

## Features

- GA4 Measurement Protocol integration
- Periodic batching (built on `Serilog.Sinks.PeriodicBatching`)
- Automatic subdivision to respect GA4 max 25 events per request (sink default 20 for headroom)
- Custom event name resolver
- Predicate-based inclusion filtering
- Optional exception detail capture
- Global (static) parameters sent with every event
- Level mapping to parameter
- Param value length trimming
- Non-personalized ads flag support
- Optional projection of `LogEvent` properties into GA parameters (with flattening, whitelist, naming, and caps)

## Quick Start

```powershell
# Install packages
Install-Package Serilog
Install-Package Serilog.Sinks.GoogleAnalytics
```

```csharp
using Serilog;
using Serilog.Sinks.GoogleAnalytics;

var logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.GoogleAnalytics(opts =>
    {
        opts.MeasurementId = "G-XXXXXXX";          // GA4 Measurement ID
        opts.ApiSecret      = "your_api_secret";   // Created in GA4 Admin > Data Streams > Measurement Protocol API secrets
        opts.ClientId       = "my-app-instance-01"; // Stable, anonymous identifier (NOT PII)
        // Optional customizations:
        // opts.EventNameResolver = e => e.Level == LogEventLevel.Error ? "error_log" : "app_log";
        // opts.IncludePredicate  = e => e.Level >= LogEventLevel.Information;
        // opts.GlobalParams["app_version"] = typeof(Program).Assembly.GetName().Version?.ToString();

        // Optional: project LogEvent properties to GA parameters
        // opts.IncludeLogEventProperties   = true;
        // opts.IncludedPropertyNames      = new HashSet<string> { "RequestId", "UserId" };
        // opts.FlattenStructuredProperties = true;   // expand objects/dicts/sequences
        // opts.FlattenSeparator            = "_";    // join nested names
        // opts.MaxPropertyParamsPerEvent   = 10;      // cap added params from properties
        // opts.PropertyNameFormatter       = name => ToSnakeCase(name); // custom mapping
    })
    .CreateLogger();

logger.Information("Service started on {Machine} at {Utc}", Environment.MachineName, DateTime.UtcNow);
logger.Error(new InvalidOperationException("Boom"), "Failure processing item {Id}", 42);
```

Dispose / flush as usual:

```csharp
Log.CloseAndFlush();
```

## Configuration (Options)

`GoogleAnalyticsOptions` properties:

| Property                   | Description                                                                                      | Default |
|---------------------------|--------------------------------------------------------------------------------------------------|---------|
| MeasurementId             | GA4 Measurement ID (required)                                                                    | (none)  |
| ApiSecret                 | GA4 API Secret (required)                                                                        | (none)  |
| ClientId                  | Stable anonymous client identifier. If blank, derived from machine name hash.                   | (auto)  |
| EventNameResolver         | `Func<LogEvent,string>` to pick event name per log                                               | `"log_event"` |
| IncludePredicate          | Predicate to include events                                                                      | `null` (all) |
| IncludeExceptionDetails   | Include exception type/message params                                                            | `true`  |
| NonPersonalizedAds        | Sets `non_personalized_ads` flag                                                                 | `false` |
| MaxEventsPerRequest       | Upper bound per HTTP post (GA max 25)                                                            | `20`    |
| MaxParamValueLength       | Trim parameter string values to length                                                           | `300`   |
| GlobalParams              | Extra key/value pairs always sent                                                                 | empty   |
| MapLevelToParam           | Adds `level` param                                                                               | `true`  |
| FlushPeriod               | Batch flush period                                                                               | 5s      |
| BatchSizeLimit            | Upper bound events kept per internal batch before emission (PeriodicBatching)                    | 40      |
| RetryCount                | (Reserved for future explicit retry policy)                                                      | 2       |
| IncludeLogEventProperties | Include `LogEvent.Properties` as event parameters                                                | `false` |
| IncludedPropertyNames     | If set, only these property names are included                                                   | `null`  |
| PropertyNameFormatter     | `Func<string,string>` mapping Serilog property name -> GA param name                             | `null`  |
| FlattenStructuredProperties | If `true`, flatten `StructureValue`/`DictionaryValue`/`SequenceValue`; else stringify complex values | `true`  |
| FlattenSeparator          | Separator joining nested names when flattening                                                   | `_`     |
| MaxParamNameLength        | Trim GA parameter names to this length (GA max 40)                                              | `40`    |
| MaxPropertyParamsPerEvent | Upper bound of parameters added from `LogEvent.Properties` per event                             | `10`    |
| City                      | The city's name. If the city is in the US, also set country_id and region_id so Google Analytics can properly map the city name to a city ID.                                                   | `null`  |
| RegionId                  | The ISO 3166 country and subdivision. For example, US-CA, US-AR, CA-BC, GB-LND, CN-HK.                                                   | `null`  |
| CountryId                 | The country in ISO 3166-1 alpha-2 format. For example, US, AU, ES, FR.                                                   | `null`  |
| SubcontinentId            | The subcontinent in UN M49 format. For example, 011, 021, 030, 039.                                                   | `null`  |
| ContinentId               | The continent in UN M49 format. For example, 002, 019, 142, 150.                                                   | `null`  |


### Event Parameters Emitted

Baseline parameters (when available):

- `message`
- `level` (if `MapLevelToParam`)
- `timestamp` (ISO 8601 UTC)
- `exception_type` / `exception_message` (if exception + enabled)
- Any `GlobalParams`
- Any `LogEvent` properties projected to GA parameters (if `IncludeLogEventProperties`), subject to filtering/flattening/naming rules below

### Property Projection (optional)

If `IncludeLogEventProperties` is enabled, `LogEvent.Properties` are mapped to GA parameters:

- Scalars map to native GA types when possible: numbers/bools sent as JSON numbers/bools; strings trimmed by `MaxParamValueLength`.
- Structured values (objects/dictionaries/sequences) are flattened into multiple parameters when `FlattenStructuredProperties = true`. Nested names are joined with `FlattenSeparator` (default `_`).
- If flattening is disabled, structured values are stringified into a single parameter.
- Parameter names are normalized: must start with a letter, only letters/digits/underscore, and trimmed to `MaxParamNameLength` (default 40). You can customize via `PropertyNameFormatter`.
- To avoid excessive payloads, the sink de-duplicates keys and caps the number of parameters added from properties via `MaxPropertyParamsPerEvent` (default 10).
- Use `IncludedPropertyNames` to whitelist only specific property names.

Example:

```csharp
opts.IncludeLogEventProperties   = true;
opts.IncludedPropertyNames      = new HashSet<string> { "Request", "UserId" };
opts.FlattenStructuredProperties = true;
opts.FlattenSeparator            = "_";

logger.Information(
    "Handled request {Request} for {UserId}",
    new { Method = "GET", Path = "/orders", StatusCode = 200 },
    "anon-123");
```

Emitted event parameters include (illustrative):

- `message`: "Handled request GET /orders for anon-123"
- `request_method`: "GET"
- `request_path`: "/orders"
- `request_statuscode`: 200
- `userid`: "anon-123" (actual casing depends on your `PropertyNameFormatter`)

### Batching Behavior

- Uses `PeriodicBatchingSink` wrapper.
- Collects up to `BatchSizeLimit` events or waits `FlushPeriod`, whichever occurs first.
- Each emitted batch is subdivided respecting `MaxEventsPerRequest` (<= 25 enforced by GA). Default 20 leaves room for future expansion without exceeding GA limits.

### Filtering

Supply `IncludePredicate`:

```csharp
opts.IncludePredicate = e => e.Level >= LogEventLevel.Warning && e.Exception != null;
```

### Custom Event Names

```csharp
opts.EventNameResolver = e => e.Level switch
{
    LogEventLevel.Error => "error_log",
    LogEventLevel.Warning => "warn_log",
    _ => "app_log"
};
```

### Global Params

```csharp
opts.GlobalParams["service"] = "orders";
opts.GlobalParams["region"]  = Environment.GetEnvironmentVariable("REGION") ?? "unknown";
```

### Trimming

Long string parameter values are truncated to `MaxParamValueLength` to avoid GA rejection.

### Non-personalized Ads

Set `NonPersonalizedAds = true` to add `"non_personalized_ads": true` to root payload (see GA4 docs).

## AppSettings (JSON) Example

You can wire through a configuration section before calling the sink; a simple pattern:

```csharp
var gaSection = configuration.GetSection("GoogleAnalytics");
loggerConfiguration.WriteTo.GoogleAnalytics(opts =>
{
    opts.MeasurementId = gaSection["MeasurementId"]!;
    opts.ApiSecret      = gaSection["ApiSecret"]!;
    opts.ClientId       = gaSection["ClientId"] ?? Environment.MachineName;
});
```

`appsettings.json` snippet:

```json
{
  "GoogleAnalytics": {
    "MeasurementId": "G-XXXXXXX",
    "ApiSecret": "your_api_secret",
    "ClientId": "instance-01"
  }
}
```

## GA4 Limits & Considerations

- Max 25 events per request (enforced by sink subdivision)
- Event parameter names should be <= 40 chars, start with a letter, and contain only letters/digits/underscore (the sink normalizes and trims)
- GA may drop/reject events with too many parameters or oversized payloads; tune `MaxPropertyParamsPerEvent` and trimming accordingly
- Excessive volume or PII can violate GA Terms  ensure compliance
- GA4 is analytics-focused; delivery latency is not guaranteed
- GA sampling / retention policies apply

## Privacy / PII

Do not send personally identifiable information (PII). Use stable, anonymous identifiers (e.g., random GUID per install) for `ClientId`.

## Extensibility Ideas

- Additional property mapping helpers (e.g., built-in snake_case formatter)
- Configurable backoff/retry policy
- Async queue with backpressure for high spikes
- Structured property flattening with advanced rules

## Testing

Run the test project:

```bash
dotnet test -c Release
```

Tests cover option validation, payload shaping, trimming, exception inclusion, custom event naming, and property projection.

## Version Compatibility

- Library: .NET Standard 2.0 (works on .NET Framework 4.6.1+, .NET Core 2.0+, .NET 5.0+)
- Tests: .NET 8.0

## Example Dashboarding

After events appear in GA4 (can take a few minutes), create custom reports filtering on the event name(s) you emitted (`log_event`, `error_log`, etc.). Parameters show under event parameter exploration.

## Troubleshooting

| Symptom              | Cause                                    | Action                                                                                       |
|----------------------|------------------------------------------|----------------------------------------------------------------------------------------------|
| Events not visible   | Propagation delay                         | Wait up to several minutes                                                                   |
| Completely missing   | Invalid Measurement ID / API Secret       | Verify GA4 Admin settings                                                                    |
| Some events missing  | GA rejection (too many params / size)     | Reduce message size or param count                                                           |
| High latency         | Batch period too large                    | Reduce `FlushPeriod`                                                                          |
| 429 / 5xx responses  | GA throttling                             | Reduce volume / add backoff (future enhancement)                                            |

## Roadmap

- Configurable backoff/retry policy
- Telemetry for sink health (dropped events count)
- Async `HttpClientFactory` integration

## Contributing

PRs & issues welcome. Please add/extend unit tests for new behaviors.

## Disclaimer

This project is not an official Google product. Use at your own risk and ensure compliance with Google Analytics Terms of Service.

## License
MIT

---
Made with Serilog and the GA4 Measurement Protocol.

