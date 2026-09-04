using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Scrubbler.Host.Presentation.Accounts;
using Scrubbler.Host.Presentation.Logging;
using Scrubbler.Host.Presentation.Plugins;
using Scrubbler.Host.Presentation.Settings;
using Scrubbler.Host.Services;
using Scrubbler.Host.Services.Logging;
using Scrubbler.Host.Updates;
using Scrubbler.PluginBase.Discord;
using Scrubbler.PluginBase.Services;
using Scrubbler.PluginBase.Settings;

namespace Scrubbler.Host;

public partial class App : Application
{
    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    internal Window? MainWindow { get; private set; }
    public IHost? Host { get; private set; }

    public event EventHandler? Ready;

    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var dispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("Application startup requires a UI dispatcher queue.");

        if (Environment.GetEnvironmentVariable("SCRUBBLER_PLUGIN_MODE") == "Debug")
        {
            var slnDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
            Environment.SetEnvironmentVariable("SOLUTIONDIR", slnDir);
        }

        var builder = this.CreateBuilder(args)
            // Add navigation support for toolkit controls such as TabBar and NavigationView
            .UseToolkitNavigation()
            .Configure(host => host
#if DEBUG
                // Switch to Development environment when running in DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .UseLogging(configure: (context, logBuilder) =>
                {
                    // Configure log levels for different categories of logging
                    logBuilder
                        .SetMinimumLevel(
                            context.HostingEnvironment.IsDevelopment() ?
                                LogLevel.Trace :
                                LogLevel.Warning)

                        // Default filters for core Uno Platform namespaces
                        .CoreLogLevel(LogLevel.Warning).XamlBindingLogLevel(LogLevel.Trace);

                }, enableUnoLogging: true)

                .UseSerialization()
                .UseConfiguration(configure: configBuilder =>
                    configBuilder
                        .EmbeddedSource<App>()
                        .Section<AppConfig>()
                        .Section<UserConfig>()
                )

                .ConfigureServices((context, services) =>
                {
                    services.AddHttpClient();
                    services.AddSingleton(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient());

                    services.AddSingleton<HostLogService>();
                    services.AddHostedService<HostLogInitializer>();
                    services.AddSingleton<LogViewModel>();
                    services.AddHostedService<LogViewModelInitializer>();

                    services.AddSingleton<IUserFeedbackService, UserFeedbackService>();
                    services.AddSingleton<IDialogService, DialogService>();
                    services.AddSingleton<ISettingsStore, JsonSettingsStore>();
                    services.AddSingleton(dispatcherQueue);
                    services.AddSingleton<IPluginManager, PluginManager>();
                    services.AddSingleton<ILinkOpenerService, LinkOpenerService>();
                    services.AddSingleton<IModuleLogServiceFactory, ModuleLogServiceFactory>();
                    services.AddSingleton<IWindowHandleProvider, WindowHandleProvider>();
                    services.AddSingleton<IFilePickerService, FilePickerService>();
                    services.AddSingleton<IFileStorageService, FileStorageService>();
                    services.AddSingleton<IDiscordRichPresence, DiscordRichPresence>();

                    if (Environment.GetEnvironmentVariable("SCRUBBLER_UPDATE_MODE") == "JSON")
                    {
                        services.AddSingleton<IUpdateSource>(sp =>
                                                         new JsonManifestUpdateSource(new Uri("http://localhost:8000/manifest.json"),
                                                         sp.GetRequiredService<HttpClient>()));
                    }
                    else
                    {
                        services.AddSingleton<IUpdateSource>(sp =>
                        {
                            // todo: inject token
                            return new GitHubReleasesUpdateSource(
                                sp.GetRequiredService<HttpClient>(),
                                new GitHubReleasesOptions
                                {
                                    Owner = "SHOEGAZEssb",
                                    Repo = "Scrubbler-2",
                                    UserAgent = "Scrubbler/2"
                                });
                        });
                    }

                    services.AddSingleton<IUpdateManagerService, UpdateManagerService>();
                    services.AddTransient<AccountsViewModel>();
                    services.AddTransient<PluginManagerViewModel>();
                    services.AddTransient<SettingsViewModel>();
                })

                .UseNavigation(RegisterRoutes)
            );
        MainWindow = builder.Window;

        var version = Package.Current.Id.Version;
        var versionString =
            $"{version.Major}.{version.Minor}.{version.Build}";
        MainWindow.Title = $"Scrubbler 2 v{versionString}";

#if DEBUG
        MainWindow.UseStudio();
#endif
        MainWindow.SetWindowIcon();

        Host = await builder.NavigateAsync<Shell>(async (services, navigator) =>
        {
            await services.GetRequiredService<IPluginManager>().InitializeAsync();
            await navigator.NavigateViewModelAsync<MainViewModel>(this);
        });
        Ready?.Invoke(this, EventArgs.Empty);
    }

    private static void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
    {
        views.Register(
            new ViewMap(ViewModel: typeof(ShellViewModel)),
            new ViewMap<MainPage, MainViewModel>()
        );

        routes.Register(
            new RouteMap("", View: views.FindByViewModel<ShellViewModel>(),
                Nested:
                [
                    new ("Main", View: views.FindByViewModel<MainViewModel>(), IsDefault:true),
                ]
            )
        );
    }
}
