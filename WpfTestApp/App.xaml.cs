using Microsoft.Extensions.Logging;
using System.Configuration;
using System.Data;
using System.Reflection;
using System.Windows;

namespace WpfTestApp;
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ILogger<App> _logger;

    void App_Startup(object sender, StartupEventArgs e)
    {
        Host.StartHost().Wait();

        _logger = Host.GetService<ILogger<App>>();
        _logger.LogInformation("Application {version} started.", Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion);

        MainWindow mainView = new();
        mainView.Show();

        return;
    }
}

