using Serilog.Core;
using Serilog.Events;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Serilog.Sinks.GoogleAnalytics;

public class GoogleAnalyticsSink : ILogEventSink, IDisposable
{
    private readonly string _measurementId;
    private readonly string _apiSecret;
    private readonly string _clientId;
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;

    public GoogleAnalyticsSink(string measurementId, string apiSecret, string clientId)
    {
        _measurementId = measurementId ?? throw new ArgumentNullException(nameof(measurementId));
        _apiSecret = apiSecret ?? throw new ArgumentNullException(nameof(apiSecret));
        _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        _httpClient = new HttpClient();
        _endpoint = $"https://www.google-analytics.com/mp/collect?measurement_id={_measurementId}&api_secret={_apiSecret}";
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent == null)
        {
            return;
        }

        var payload = BuildPayload(logEvent);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        // Fire and forget
        Task.Run(() => _httpClient.PostAsync(_endpoint, content));
    }

    private string BuildPayload(LogEvent logEvent)
    {
        var message = logEvent.RenderMessage();
        var level = logEvent.Level.ToString();
        var timestamp = logEvent.Timestamp.UtcDateTime.ToString("o");
        // Google Analytics 4 Measurement Protocol event format
        return $@"{{" +
               $"\"client_id\": \"{_clientId}\"," +
               "\"events\": [{" +
               "\"name\": \"log_event\"," +
               "\"params\": {" +
               $"\"message\": \"{EscapeJson(message)}\"," +
               $"\"level\": \"{level}\"," +
               $"\"timestamp\": \"{timestamp}\"" +
               "}" +
               "}]" +
               "}";
    }

    private static string EscapeJson(string value)
    {
        return value?.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

