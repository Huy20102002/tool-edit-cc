using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using AutoVideoEditor.App.ViewModels;
using AutoVideoEditor.App.Views;
using AutoVideoEditor.Core.Interfaces;
using AutoVideoEditor.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AutoVideoEditor.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Set Vietnamese Culture for UI & Thread
        var culture = new CultureInfo("vi-VN");
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        var services = new ServiceCollection();
        ConfigureServices(services);

        _serviceProvider = services.BuildServiceProvider();

        // Enable thread-safe collection synchronization for WPF UI bindings
        var logService = _serviceProvider.GetRequiredService<ILogService>();
        BindingOperations.EnableCollectionSynchronization(logService.Logs, logService.SyncRoot);

        var jobManager = _serviceProvider.GetRequiredService<IJobManager>();
        BindingOperations.EnableCollectionSynchronization(jobManager.Jobs, jobManager.SyncRoot);

        // Perform startup cleanup of abandoned temp files
        try
        {
            var tempManager = _serviceProvider.GetRequiredService<ITempFileManager>();
            tempManager.CleanupAllOldTempFiles();
        }
        catch
        {
            // Ignore
        }

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Core & Infrastructure & Engines
        services.AddAutoVideoEditorServices();

        // ViewModels
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<QueueViewModel>();
        services.AddSingleton<PreviewViewModel>();
        services.AddSingleton<PresetViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<LogsViewModel>();
        services.AddSingleton<MainViewModel>();

        // Views
        services.AddSingleton<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
