using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using Serilog.Sinks.GoogleAnalytics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfTestApp;

internal static class Host
{
    private static IHost _host;

    public static async Task StartHost()
    {
        var logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ECE", "RRL_Desktop_Log.json");

#if DEBUG
        logPath = "log.json";
#endif

        var cultureInfo = Thread.CurrentThread.CurrentCulture;
        var regionInfo = new RegionInfo(cultureInfo.LCID);

        Serilog.Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .WriteTo.Debug(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.GoogleAnalytics(opts =>
            {
                opts.MeasurementId = "##MEASUREMENTID##";
                opts.ApiSecret = "##APISECRET##";
                opts.ClientId = Environment.MachineName.GetHashCode().ToString();

                opts.FlushPeriod = TimeSpan.FromSeconds(1);
                opts.BatchSizeLimit = 1;
                opts.MaxEventsPerRequest = 1;
                opts.IncludePredicate = e => e.Properties.ContainsKey("UsageTracking");            

                opts.GlobalParams["app_version"] = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString();
                opts.GlobalParams["region"] = regionInfo.EnglishName ?? "unknown";
            })
            .CreateLogger();

        _host = Microsoft.Extensions.Hosting.Host
            .CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((_, services) =>
            {

            })
            .Build();

        await _host.StartAsync();
    }

    public static async Task StartHost(IHost host)
    {
        _host = host;
        await host.StartAsync();
    }

    public static async Task StopHost()
    {
        await _host.StopAsync();
        _host.Dispose();
    }

    public static T GetService<T>() where T : class
    {
        return _host.Services.GetService(typeof(T)) as T;
    }
}

