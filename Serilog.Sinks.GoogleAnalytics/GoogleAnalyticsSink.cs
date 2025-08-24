using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Serilog.Sinks.GoogleAnalytics;

// Implements the batched sink interface so it can be wrapped by PeriodicBatchingSink
public class GoogleAnalyticsSink : Serilog.Sinks.PeriodicBatching.IBatchedLogEventSink, IDisposable
{
    private readonly GoogleAnalyticsOptions _options;
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;

    public GoogleAnalyticsSink(GoogleAnalyticsOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_options.MeasurementId))
        {
            throw new ArgumentNullException(nameof(options.MeasurementId), "MeasurementId required");
        }
        if (string.IsNullOrWhiteSpace(_options.ApiSecret))
        {
            throw new ArgumentNullException(nameof(options.ApiSecret), "ApiSecret required");
        }
        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            _options.ClientId = Environment.MachineName.GetHashCode().ToString("X");
        }

        _httpClient = new HttpClient();
        _endpoint = $"https://www.google-analytics.com/mp/collect?measurement_id={_options.MeasurementId}&api_secret={_options.ApiSecret}";
    }

    public async Task EmitBatchAsync(IEnumerable<LogEvent> batch)
    {
        if (batch == null)
            return;

        // Apply predicate filter early
        var filtered = _options.IncludePredicate != null ? batch.Where(e => _options.IncludePredicate(e)) : batch;
        var eventsList = filtered.ToList();
        if (eventsList.Count == 0) return;

        int maxPerRequest = Math.Max(1, _options.MaxEventsPerRequest > 0 ? _options.MaxEventsPerRequest : 20); // GA hard limit 25

        for (int i = 0; i < eventsList.Count; i += maxPerRequest)
        {
            var slice = eventsList.Skip(i).Take(maxPerRequest).ToList();
            if (slice.Count == 0) continue;
            var payload = BuildBatchPayload(slice);
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            // Let exceptions bubble so PeriodicBatchingSink can handle retries if configured
            await _httpClient.PostAsync(_endpoint, content).ConfigureAwait(false);
        }
    }

    public Task OnEmptyBatchAsync() => Task.CompletedTask;

    private string BuildBatchPayload(IReadOnlyCollection<LogEvent> events)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        AppendJsonProperty(sb, "client_id", _options.ClientId, commaAfter: true);
        if (_options.NonPersonalizedAds)
        {
            sb.Append("\"non_personalized_ads\": true,");
        }
        sb.Append("\"events\": [");
        bool firstEvent = true;
        foreach (var e in events)
        {
            if (!firstEvent) sb.Append(',');
            firstEvent = false;
            sb.Append('{');

            var name = _options.EventNameResolver != null ? _options.EventNameResolver(e) : "log_event";
            if (string.IsNullOrWhiteSpace(name)) name = "log_event";
            AppendJsonProperty(sb, "name", TrimIfNeeded(name), commaAfter: true);
            sb.Append("\"params\": {");

            // Collect params then write (simpler comma management)
            var paramPairs = new List<(string Key, object? Value)>();
            paramPairs.Add(("message", TrimIfNeeded(e.RenderMessage())));
            if (_options.MapLevelToParam)
                paramPairs.Add(("level", e.Level.ToString()));
            paramPairs.Add(("timestamp", e.Timestamp.UtcDateTime.ToString("o")));

            if (_options.IncludeExceptionDetails && e.Exception != null)
            {
                paramPairs.Add(("exception_type", e.Exception.GetType().Name));
                paramPairs.Add(("exception_message", TrimIfNeeded(e.Exception.Message)));
            }
            if (_options.GlobalParams != null && _options.GlobalParams.Count > 0)
            {
                foreach (var kvp in _options.GlobalParams)
                {
                    if (kvp.Key == null) continue;
                    paramPairs.Add((kvp.Key, kvp.Value));
                }
            }

            for (int i = 0; i < paramPairs.Count; i++)
            {
                var (k, v) = paramPairs[i];
                if (v == null) continue;
                if (i > 0) sb.Append(',');
                AppendJsonProperty(sb, k, v, commaAfter: false);
            }
            sb.Append('}'); // params
            sb.Append('}'); // event
        }
        sb.Append(']'); // events
        sb.Append('}'); // root
        return sb.ToString();
    }

    private void AppendJsonProperty(StringBuilder sb, string key, object? value, bool commaAfter)
    {
        if (value == null) return;
        sb.Append('"').Append(EscapeJson(key)).Append("\": ");
        switch (value)
        {
            case string s:
                sb.Append('"').Append(EscapeJson(TrimIfNeeded(s))).Append('"');
                break;
            case bool b:
                sb.Append(b ? "true" : "false");
                break;
            case int or long or double or float or decimal:
                sb.Append(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
                break;
            default:
                sb.Append('"').Append(EscapeJson(TrimIfNeeded(value.ToString() ?? string.Empty))).Append('"');
                break;
        }
        if (commaAfter) sb.Append(',');
    }

    private string TrimIfNeeded(string value)
    {
        if (value == null) return string.Empty;
        int max = _options.MaxParamValueLength > 0 ? _options.MaxParamValueLength : 300;
        return value.Length <= max ? value : value.Substring(0, max);
    }

    private static string EscapeJson(string value)
    {
        return value?.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    // Legacy single-event payload builder retained for tests / reflection access
    private string BuildPayload(LogEvent logEvent) => BuildBatchPayload(new[] { logEvent });

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

