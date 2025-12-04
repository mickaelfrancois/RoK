using Microsoft.Extensions.Configuration;
using Rok.Application.Options;
using Rok.Import;
using Rok.Infrastructure;
using Serilog;
using System.IO;
using Windows.Storage;
using Windows.Storage.AccessCache;
using WinRT.Interop;

namespace Rok
{
    public partial class App : Microsoft.UI.Xaml.Application
    {
        public static ServiceProvider ServiceProvider { get; private set; } = null!;

        public static Window MainWindow { get; private set; } = null!;
        public static nint MainWindowHandle { get; private set; }

        public App()
        {
            HookGlobalDiagnostics();
            this.UnhandledException += Application_UnhandledException;

            this.InitializeComponent();

#if DEBUG
            Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = "en";
#endif
        }




        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            ServiceProvider = ConfigureServices();

            IAppOptions options = await LoadOptionsAsync();

            IAppDbContext appDbContext = ServiceProvider.GetRequiredService<IAppDbContext>();
            appDbContext.GetOpenConnection();
            appDbContext.EnsureCreated();

            NavigationService navigationService = ServiceProvider.GetRequiredService<NavigationService>();
            ResourceLoader resourceLoader = ServiceProvider.GetRequiredService<ResourceLoader>();

            string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets/Square44x44Logo.ico");

            MainWindow = new MainWindow(navigationService, resourceLoader, appDbContext, options);
            MainWindow.AppWindow.SetIcon(iconPath);
            MainWindow.Title = "RoK";
#if DEBUG
            MainWindow.Title += " [DEBUG]";
#endif
            MainWindow.Activate();
            MainWindow.Closed += MainWindow_Closed;

            MainWindowHandle = WindowNative.GetWindowHandle(MainWindow);


#if DEBUG
            TryEnableXamlDiagnostics();
#endif

            ThemeManager.Initialize(options.Theme, MainWindow);
        }


        [Conditional("DEBUG")]
        private void TryEnableXamlDiagnostics()
        {
            try
            {
                DebugSettings debug = this.DebugSettings;
                debug.IsBindingTracingEnabled = true;
                debug.BindingFailed += (_, e) =>
                {
                    Debug.WriteLine("[BindingFailed] " + e.Message);
                };
                debug.XamlResourceReferenceFailed += (_, e2) =>
                {
                    Debug.WriteLine("[XamlResourceReferenceFailed] " + e2.Message);
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Activation diagnostics XAML échouée: " + ex);
            }
        }


        private static ServiceProvider ConfigureServices()
        {
            IServiceCollection services = new ServiceCollection();

            IConfiguration config = new ConfigurationBuilder()
                        .SetBasePath(AppContext.BaseDirectory)
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                        .Build();
            services.AddSingleton<IConfiguration>(config);
            services.Configure<TelemetryOptions>(config.GetSection("Telemetry"));
            services.Configure<NovaApiOptions>(config.GetSection("NovaApi"));
            services.Configure<DiscordOptions>(config.GetSection("Discord"));

            services.AddHttpClient();

            services.AddApplication();
            services.AddImport();
            services.AddInfrastructure(ApplicationData.Current.LocalFolder.Path);
            services.AddLogger(ApplicationData.Current.LocalFolder.Path);
            services.AddLogic();

            return services.BuildServiceProvider();
        }


        private static async Task<IAppOptions> LoadOptionsAsync()
        {
            IAppOptions options = ServiceProvider.GetRequiredService<IAppOptions>();
            ISettingsFile settingFileService = ServiceProvider.GetRequiredService<ISettingsFile>();

            if (settingFileService.Exists())
            {
                IAppOptions? newOptions = settingFileService.Load<AppOptions>();
                if (newOptions != null)
                    options.CopyFrom(newOptions);

                await settingFileService.RemoveInvalidLibraryTokensAsync(options);
            }
            else
            {
                options.InitializeOptions(ApplicationData.Current.LocalFolder.Path);
            }

            if (options.LibraryTokens.Count == 0)
            {
                try
                {
                    string token = StorageApplicationPermissions.FutureAccessList.Add(KnownFolders.MusicLibrary);
                    options.LibraryTokens.Add(token);
                }
                catch (Exception ex)
                {
                    ITelemetryClient telemetry = ServiceProvider.GetRequiredService<ITelemetryClient>();
                    _ = telemetry.CaptureExceptionAsync(ex);
                }
            }

            return options;
        }


        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            IAppOptions options = ServiceProvider.GetRequiredService<IAppOptions>();
            ISettingsFile settingFileService = ServiceProvider.GetRequiredService<ISettingsFile>();

            settingFileService.Save(options);

            Log.CloseAndFlush();
        }


        private void Application_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            try
            {
                ILogger<App>? logger = ServiceProvider.GetService<Microsoft.Extensions.Logging.ILogger<App>>();
                logger?.LogError(e.Exception, "Unhandler exception: {Message}", e.Exception.Message);

#if DEBUG
                // handle to avoid crash:
                e.Handled = true;
#else
                ITelemetryClient telemetry = ServiceProvider.GetRequiredService<ITelemetryClient>();
                _ = telemetry.CaptureExceptionAsync(e.Exception);
#endif
            }
            catch
            {
                // Ignore any logging errors to avoid masking the original exception.
            }
        }

        private void HookGlobalDiagnostics()
        {
            // 1st chance
            AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
            {
#if DEBUG
                if (e.Exception is NullReferenceException or InvalidOperationException or FileNotFoundException)
                    Debug.WriteLine("[FirstChance] " + e.Exception.GetType().Name + " - " + e.Exception.Message);
#endif
            };

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                Debug.WriteLine("[AppDomain.Unhandled] " + e.ExceptionObject);

#if !DEBUG
                if (e.ExceptionObject is Exception ex)
                {
                    ITelemetryClient telemetry = ServiceProvider.GetRequiredService<ITelemetryClient>();
                    telemetry.CaptureExceptionAsync(ex);
                    System.Threading.Thread.Sleep(500); // Give some time to send the telemetry before exiting
                }
#endif
            };

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                if (e.Exception is Exception ex)
                {
#if !DEBUG
                    ITelemetryClient telemetry = ServiceProvider.GetRequiredService<ITelemetryClient>();
                    Task.Run(async () => await telemetry.CaptureExceptionAsync(ex))
                        .Wait(TimeSpan.FromSeconds(2));
#endif
                }
                e.SetObserved();
            };
        }
    }
}
