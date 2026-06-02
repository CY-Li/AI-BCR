#nullable enable
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PlustekBCR.Services;
using PlustekBCR.ViewModels;
using PlustekBCR.Views;
using WinUIEx;

namespace PlustekBCR
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public static Window? Window { get; private set; }

        // Dependency Injection Host
        public static IHost Host { get; } = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Register ViewModels
                services.AddSingleton<MainViewModel>();
                services.AddTransient<EmptyViewModel>();
                services.AddSingleton<AllCardsViewModel>();
                services.AddTransient<CardDetailViewModel>();
                
                // Register Services
                services.AddSingleton<IUpdateService, UpdateService>();
                services.AddSingleton<ITagCatalogService, TagCatalogService>();
            })
            .Build();

        public static T GetService<T>() where T : class
        {
            if (Host.Services.GetService(typeof(T)) is not T service)
            {
                throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices.");
            }
            return service;
        }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            // Fix for SingleFile publish warning
            Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);
            
            this.InitializeComponent();
            this.UnhandledException += (s, e) =>
            {
                e.Handled = true;
                try
                {
                    System.IO.File.WriteAllText("crash_log.txt", e.Exception.ToString());
                }
                catch { }
            };
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Window = new MainWindow();
            
            // Eagerly resolve AllCardsViewModel so it registers for messages immediately
            GetService<AllCardsViewModel>();

            Window.Maximize();
            Window.Activate();
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }
    }
}
