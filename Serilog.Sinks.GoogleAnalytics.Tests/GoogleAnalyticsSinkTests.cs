using Serilog.Events;
using Serilog.Parsing;
using System.Reflection;

namespace Serilog.Sinks.GoogleAnalytics.Tests;

public class GoogleAnalyticsSinkTests
{
    private LogEvent CreateEvent(string message, LogEventLevel level = LogEventLevel.Information, Exception? ex = null)
    {
        return new LogEvent(
            DateTimeOffset.UtcNow,
            level,
            ex,
            new MessageTemplate(message, new List<MessageTemplateToken>()),
            new List<LogEventProperty>()
        );
    }

    [Fact]
    public void Constructor_Throws_When_MeasurementId_Missing()
    {
        var opts = new GoogleAnalyticsOptions { MeasurementId = null!, ApiSecret = "secret", ClientId = "cid" };
        Assert.Throws<ArgumentNullException>(() => new GoogleAnalyticsSink(opts));
    }

    [Fact]
    public void Constructor_Throws_When_ApiSecret_Missing()
    {
        var opts = new GoogleAnalyticsOptions { MeasurementId = "mid", ApiSecret = null!, ClientId = "cid" };
        Assert.Throws<ArgumentNullException>(() => new GoogleAnalyticsSink(opts));
    }

    [Fact]
    public void Constructor_Autogenerates_ClientId_When_Missing()
    {
        var opts = new GoogleAnalyticsOptions { MeasurementId = "mid", ApiSecret = "secret", ClientId = null! };
        var sink = new GoogleAnalyticsSink(opts);
        Assert.False(string.IsNullOrWhiteSpace(opts.ClientId));
    }

    [Fact]
    public void BuildPayload_Produces_Valid_Json_For_Single_Event()
    {
        var opts = new GoogleAnalyticsOptions { MeasurementId = "mid", ApiSecret = "secret", ClientId = "cid" };
        var sink = new GoogleAnalyticsSink(opts);
        var logEvent = CreateEvent("Test message");
        var method = typeof(GoogleAnalyticsSink).GetMethod("BuildPayload", BindingFlags.NonPublic | BindingFlags.Instance);
        var payload = (string)method!.Invoke(sink, new object[] { logEvent })!;
        Assert.Contains("\"client_id\": \"cid\"", payload);
        Assert.Contains("\"message\": \"Test message\"", payload);
        Assert.Contains("\"level\": \"Information\"", payload); // default mapping
        Assert.Contains("\"timestamp\": ", payload);
    }

    [Fact]
    public void BuildBatchPayload_Uses_EventNameResolver()
    {
        var opts = new GoogleAnalyticsOptions
        {
            MeasurementId = "mid",
            ApiSecret = "secret",
            ClientId = "cid",
            EventNameResolver = _ => "custom_name"
        };
        var sink = new GoogleAnalyticsSink(opts);
        var events = new List<LogEvent> { CreateEvent("hello") };
        var batchMethod = typeof(GoogleAnalyticsSink).GetMethod("BuildBatchPayload", BindingFlags.NonPublic | BindingFlags.Instance);
        var payload = (string)batchMethod!.Invoke(sink, new object[] { events })!;
        Assert.Contains("\"name\": \"custom_name\"", payload);
    }

    [Fact]
    public void BuildBatchPayload_Trims_Long_Message()
    {
        var opts = new GoogleAnalyticsOptions
        {
            MeasurementId = "mid",
            ApiSecret = "secret",
            ClientId = "cid",
            MaxParamValueLength = 10
        };
        var sink = new GoogleAnalyticsSink(opts);
        var events = new List<LogEvent> { CreateEvent("123456789012345") }; // > 10 chars
        var batchMethod = typeof(GoogleAnalyticsSink).GetMethod("BuildBatchPayload", BindingFlags.NonPublic | BindingFlags.Instance);
        var payload = (string)batchMethod!.Invoke(sink, new object[] { events })!;
        Assert.Contains("1234567890", payload); // trimmed
        Assert.DoesNotContain("12345678901\"", payload); // ensure not longer than 10 inside quotes
    }

    [Fact]
    public void BuildBatchPayload_Includes_Exception_When_Configured()
    {
        var opts = new GoogleAnalyticsOptions
        {
            MeasurementId = "mid",
            ApiSecret = "secret",
            ClientId = "cid",
            IncludeExceptionDetails = true
        };
        var sink = new GoogleAnalyticsSink(opts);
        var events = new List<LogEvent> { CreateEvent("boom", LogEventLevel.Error, new InvalidOperationException("fail")) };
        var batchMethod = typeof(GoogleAnalyticsSink).GetMethod("BuildBatchPayload", BindingFlags.NonPublic | BindingFlags.Instance);
        var payload = (string)batchMethod!.Invoke(sink, new object[] { events })!;
        Assert.Contains("exception_type", payload);
        Assert.Contains("InvalidOperationException", payload);
        Assert.Contains("exception_message", payload);
    }

    [Fact]
    public void BuildBatchPayload_Excludes_Exception_When_Disabled()
    {
        var opts = new GoogleAnalyticsOptions
        {
            MeasurementId = "mid",
            ApiSecret = "secret",
            ClientId = "cid",
            IncludeExceptionDetails = false
        };
        var sink = new GoogleAnalyticsSink(opts);
        var events = new List<LogEvent> { CreateEvent("boom", LogEventLevel.Error, new InvalidOperationException("fail")) };
        var batchMethod = typeof(GoogleAnalyticsSink).GetMethod("BuildBatchPayload", BindingFlags.NonPublic | BindingFlags.Instance);
        var payload = (string)batchMethod!.Invoke(sink, new object[] { events })!;
        Assert.DoesNotContain("exception_type", payload);
        Assert.DoesNotContain("exception_message", payload);
    }
}