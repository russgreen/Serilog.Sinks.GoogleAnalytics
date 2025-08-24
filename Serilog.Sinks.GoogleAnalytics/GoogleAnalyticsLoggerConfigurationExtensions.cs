using Serilog.Configuration;
using Serilog.Sinks.PeriodicBatching;
using System;

namespace Serilog.Sinks.GoogleAnalytics;
public static class GoogleAnalyticsLoggerConfigurationExtensions
{
    /// <summary>
    /// Adds a sink that sends log events to Google Analytics using the Measurement Protocol.
    /// </summary>
    /// <param name="loggerConfiguration">The logger configuration.</param>
    /// <param name="measurementId">The Google Analytics Measurement ID.</param>
    /// <param name="apiSecret">The API secret for the Google Analytics property.</param>
    /// <param name="clientId">A unique identifier for the client (user).</param>
    /// <returns>The updated logger configuration.</returns>
    public static LoggerConfiguration GoogleAnalytics(
            this LoggerSinkConfiguration sinkConfiguration,
            Action<GoogleAnalyticsOptions> configure,
            Serilog.Events.LogEventLevel restrictedToMinimumLevel = Serilog.Events.LogEventLevel.Information)
    {
        if (sinkConfiguration == null) throw new ArgumentNullException(nameof(sinkConfiguration));
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        var opts = new GoogleAnalyticsOptions();
        configure(opts);

        var inner = new GoogleAnalyticsSink(opts);
        var batching = new PeriodicBatchingSink(inner, new PeriodicBatchingSinkOptions
        {
            BatchSizeLimit = opts.BatchSizeLimit > 0 ? opts.BatchSizeLimit : 40,
            Period = opts.FlushPeriod > TimeSpan.Zero ? opts.FlushPeriod : TimeSpan.FromSeconds(5),
            EagerlyEmitFirstEvent = true
        });
        return sinkConfiguration.Sink(batching, restrictedToMinimumLevel);
    }
}
