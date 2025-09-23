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
        {
            return;
        }

        // Apply predicate filter early
        var filtered = _options.IncludePredicate != null ? batch.Where(e => _options.IncludePredicate(e)) : batch;
        var eventsList = filtered.ToList();
        if (eventsList.Count == 0)
        {
            return;
        }

        int maxPerRequest = Math.Max(1, _options.MaxEventsPerRequest > 0 ? _options.MaxEventsPerRequest : 20); // GA hard limit 25

        for (int i = 0; i < eventsList.Count; i += maxPerRequest)
        {
            var slice = eventsList.Skip(i).Take(maxPerRequest).ToList();
            if (slice.Count == 0)
            {
                continue;
            }

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
            if (!firstEvent)
            {
                sb.Append(',');
            }

            firstEvent = false;
            sb.Append('{');

            var name = _options.EventNameResolver != null ? _options.EventNameResolver(e) : "log_event";
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "log_event";
            }

            AppendJsonProperty(sb, "name", TrimIfNeeded(name), commaAfter: true);
            sb.Append("\"params\": {");

            // Collect params then write (simpler comma management)
            var paramPairs = new List<(string Key, object? Value)>();
            paramPairs.Add(("message", TrimIfNeeded(e.RenderMessage())));
            if (_options.MapLevelToParam)
            {
                paramPairs.Add(("level", e.Level.ToString()));
            }

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
                    if (kvp.Key == null)
                    {
                        continue;
                    }

                    paramPairs.Add((kvp.Key, kvp.Value));
                }
            }

            // NEW: Map LogEvent.Properties into GA params if enabled
            AddLogEventProperties(paramPairs, e);

            // Write params (fixed comma handling)
            int written = 0;
            for (int i = 0; i < paramPairs.Count; i++)
            {
                var (k, v) = paramPairs[i];
                if (v == null)
                {
                    continue;
                }

                if (written > 0)
                {
                    sb.Append(',');
                }

                AppendJsonProperty(sb, k, v, commaAfter: false);
                written++;
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
        if (value == null)
        {
            return;
        }

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
        if (commaAfter)
        {
            sb.Append(',');
        }
    }

    private string TrimIfNeeded(string value)
    {
        if (value == null)
        {
            return string.Empty;
        }

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

    // --- NEW helpers for serializing LogEvent.Properties ---

    private void AddLogEventProperties(List<(string Key, object? Value)> paramPairs, LogEvent e)
    {
        if (!_options.IncludeLogEventProperties || e.Properties == null || e.Properties.Count == 0)
        {
            return;
        }

        // Avoid duplicate keys with earlier params/global params
        var existingKeys = new HashSet<string>(paramPairs.Select(p => p.Key), StringComparer.OrdinalIgnoreCase);

        int added = 0;
        foreach (var kvp in e.Properties)
        {
            if (_options.IncludedPropertyNames != null && !_options.IncludedPropertyNames.Contains(kvp.Key))
            {
                continue;
            }

            var baseName = FormatParamName(kvp.Key);
            AddPropertyValue(paramPairs, existingKeys, baseName, kvp.Value, ref added);

            if (_options.MaxPropertyParamsPerEvent > 0 && added >= _options.MaxPropertyParamsPerEvent)
            {
                break;
            }
        }
    }

    private void AddPropertyValue(
        List<(string Key, object? Value)> paramPairs,
        HashSet<string> existingKeys,
        string name,
        LogEventPropertyValue value,
        ref int addedCount)
    {
        // Respect cap
        if (_options.MaxPropertyParamsPerEvent > 0 && addedCount >= _options.MaxPropertyParamsPerEvent)
        {
            return;
        }

        switch (value)
        {
            case ScalarValue scalar:
                var (scalarValue, isNull) = ConvertScalar(scalar);
                if (!isNull)
                {
                    TryAdd(paramPairs, existingKeys, name, scalarValue, ref addedCount);
                }
                break;

            case StructureValue sv:
                if (_options.FlattenStructuredProperties)
                {
                    foreach (var p in sv.Properties)
                    {
                        if (_options.MaxPropertyParamsPerEvent > 0 && addedCount >= _options.MaxPropertyParamsPerEvent)
                            break;

                        var childName = CombineNames(name, p.Name);
                        AddPropertyValue(paramPairs, existingKeys, childName, p.Value, ref addedCount);
                    }
                }
                else
                {
                    // Stringify complex value
                    TryAdd(paramPairs, existingKeys, name, TrimIfNeeded(value.ToString() ?? string.Empty), ref addedCount);
                }
                break;

            case DictionaryValue dv:
                if (_options.FlattenStructuredProperties)
                {
                    foreach (var kv in dv.Elements)
                    {
                        if (_options.MaxPropertyParamsPerEvent > 0 && addedCount >= _options.MaxPropertyParamsPerEvent)
                            break;

                        var keyText = kv.Key.Value?.ToString() ?? "key";
                        var childName = CombineNames(name, keyText);
                        AddPropertyValue(paramPairs, existingKeys, childName, kv.Value, ref addedCount);
                    }
                }
                else
                {
                    TryAdd(paramPairs, existingKeys, name, TrimIfNeeded(value.ToString() ?? string.Empty), ref addedCount);
                }
                break;

            case SequenceValue seq:
                if (_options.FlattenStructuredProperties)
                {
                    int idx = 0;
                    foreach (var item in seq.Elements)
                    {
                        if (_options.MaxPropertyParamsPerEvent > 0 && addedCount >= _options.MaxPropertyParamsPerEvent)
                            break;

                        var childName = CombineNames(name, idx.ToString());
                        AddPropertyValue(paramPairs, existingKeys, childName, item, ref addedCount);
                        idx++;
                    }
                }
                else
                {
                    TryAdd(paramPairs, existingKeys, name, TrimIfNeeded(value.ToString() ?? string.Empty), ref addedCount);
                }
                break;

            default:
                TryAdd(paramPairs, existingKeys, name, TrimIfNeeded(value.ToString() ?? string.Empty), ref addedCount);
                break;
        }
    }

    private (object? Value, bool IsNull) ConvertScalar(ScalarValue scalar)
    {
        var v = scalar.Value;
        if (v == null) return (null, true);

        switch (v)
        {
            case string s:
                return (TrimIfNeeded(s), false);
            case bool b:
                return (b, false);
            case sbyte or byte or short or ushort or int or uint or long or ulong:
                // GA likes numeric params as numbers
                return (Convert.ToInt64(v, System.Globalization.CultureInfo.InvariantCulture), false);
            case float or double or decimal:
                return (Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture), false);
            case DateTime dt:
                return (dt.ToUniversalTime().ToString("o"), false);
            case DateTimeOffset dto:
                return (dto.ToUniversalTime().ToString("o"), false);
            case Guid g:
                return (g.ToString("D"), false);
            default:
                return (TrimIfNeeded(v.ToString() ?? string.Empty), false);
        }
    }

    private void TryAdd(
        List<(string Key, object? Value)> paramPairs,
        HashSet<string> existingKeys,
        string key,
        object? value,
        ref int addedCount)
    {
        if (value == null) return;

        // Avoid overwriting existing keys (message, level, timestamp, etc.)
        if (!existingKeys.Contains(key))
        {
            paramPairs.Add((key, value));
            existingKeys.Add(key);
            addedCount++;
        }
    }

    private string CombineNames(string a, string b)
    {
        var sep = string.IsNullOrEmpty(_options.FlattenSeparator) ? "_" : _options.FlattenSeparator;
        return FormatParamName(a + sep + b);
    }

    private string FormatParamName(string name)
    {
        // Allow user customization first
        if (_options.PropertyNameFormatter != null)
        {
            var custom = _options.PropertyNameFormatter(name);
            if (!string.IsNullOrWhiteSpace(custom))
            {
                name = custom!;
            }
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = "p";
        }

        // Allowed: letters, digits, underscore; ensure starts with a letter
        var sb = new StringBuilder(name.Length + 2);
        if (!char.IsLetter(name[0]))
        {
            sb.Append("p_");
        }
        for (int i = 0; i < name.Length; i++)
        {
            var ch = name[i];
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                sb.Append(ch);
            }
            else
            {
                sb.Append('_');
            }
        }

        var result = sb.ToString();
        int max = _options.MaxParamNameLength > 0 ? _options.MaxParamNameLength : 40;
        if (result.Length > max)
        {
            result = result.Substring(0, max);
        }
        return result;
    }
}

