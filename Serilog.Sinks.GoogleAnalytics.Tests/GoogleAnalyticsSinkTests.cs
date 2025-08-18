using Serilog.Events;
using Serilog.Sinks.GoogleAnalytics;
using Serilog.Parsing;
using System;
using System.Collections.Generic;
using System.Net.Http;
using Xunit;
using System.Reflection;

namespace Serilog.Sinks.GoogleAnalytics.Tests;

public class GoogleAnalyticsSinkTests
{
    [Fact]
    public void Constructor_Throws_On_Null_Arguments()
    {
        Assert.Throws<ArgumentNullException>(() => new GoogleAnalyticsSink(null, "secret", "cid"));
        Assert.Throws<ArgumentNullException>(() => new GoogleAnalyticsSink("mid", null, "cid"));
        Assert.Throws<ArgumentNullException>(() => new GoogleAnalyticsSink("mid", "secret", null));
    }

    [Fact]
    public void BuildPayload_Produces_Valid_Json()
    {
        var sink = new GoogleAnalyticsSink("mid", "secret", "cid");
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            new MessageTemplate("Test message", new List<MessageTemplateToken>()),
            new List<LogEventProperty>()
        );
        var method = typeof(GoogleAnalyticsSink).GetMethod("BuildPayload", BindingFlags.NonPublic | BindingFlags.Instance);
        var payload = (string)method.Invoke(sink, new object[] { logEvent });
        Assert.Contains("\"client_id\": ", payload);
        Assert.Contains("\"message\": ", payload);
        Assert.Contains("\"level\": ", payload);
        Assert.Contains("\"timestamp\": ", payload);
    }
}