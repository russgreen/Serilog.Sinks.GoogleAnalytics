using Microsoft.Extensions.Logging;
using Serilog.Context;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfTestApp;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private ILogger<MainWindow> _logger;

    public MainWindow()
    {
        InitializeComponent();

        _logger = Host.GetService<ILogger<MainWindow>>();
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        using (LogContext.PushProperty("UsageTracking", true))
        {
            _logger.LogInformation("{log_message}", "Logged information");
        }

    }

    private void Button_Click_1(object sender, RoutedEventArgs e)
    {
        using (LogContext.PushProperty("UsageTracking", true))
        {
            _logger.LogWarning("{log_message}", "Logged warning");
        }

    }

    private void Button_Click_2(object sender, RoutedEventArgs e)
    {
        using (LogContext.PushProperty("UsageTracking", true))
        {
            _logger.LogError("{log_message}", "Logged error");
        }

    }

    private void Button_Click_3(object sender, RoutedEventArgs e)
    {
        using (LogContext.PushProperty("UsageTracking", true))
        {
            _logger.LogCritical("{log_message}", "Logged critical");
        }

    }

    private void Button_Click_4(object sender, RoutedEventArgs e)
    {
            throw new Exception(); 
    }
}