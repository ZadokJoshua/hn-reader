using HNReader.Core.Enums;
using HNReader.Core.Interfaces;
using HNReader.Core.Services;
using HNReader.Core.Viewmodels;
using HNReader.WinUI.Factories;
using HNReader.WinUI.Services;
using HNReader.WinUI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;

namespace HNReader.WinUI;

public partial class App : Application
{
    private Window? _window;

    public IServiceProvider Services { get; }

    public static Window? CurrentWindow { get; private set; }

    public App()
    {
        Services = ConfigureServices();
        InitializeComponent();

        // Global unhandled exception handler
        UnhandledException += OnUnhandledException;

        // Subscribe to theme changes
        var settingsService = Services.GetRequiredService<ISettingsService>();
        settingsService.ThemeChanged += OnThemeChanged;
        ApplyTheme(settingsService.Theme);

        // Wire vault change lifecycle for knowledge base management
        var vaultFileService = Services.GetRequiredService<IVaultFileService>();
        settingsService.VaultChanged += async (_, newPath) =>
        {
            // Cleanup the old knowledge base (at the current base path)
            try
            {
                await vaultFileService.CleanupKnowledgeBaseAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cleaning up old knowledge base: {ex.Message}");
            }

            // Update to the new vault path
            vaultFileService.SetBasePath(newPath);

            // Initialize the new knowledge base if a vault is selected
            if (!string.IsNullOrEmpty(newPath))
            {
                try
                {
                    await vaultFileService.InitializeKnowledgeBaseAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error initializing knowledge base: {ex.Message}");
                }
            }
        };

        // Ensure knowledge base is initialized on startup if vault exists
        if (settingsService.HasVault)
        {
            _ = vaultFileService.InitializeKnowledgeBaseAsync();
        }
    }

    private void OnThemeChanged(object? sender, AppTheme theme)
    {
        ApplyTheme(theme);
    }

    private static void ApplyTheme(AppTheme theme)
    {
        if (CurrentWindow?.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = theme switch
            {
                AppTheme.Light => ElementTheme.Light,
                AppTheme.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }

    public void ApplyCurrentTheme()
    {
        var settingsService = Services.GetRequiredService<ISettingsService>();
        ApplyTheme(settingsService.Theme);
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;

        var errorMessage = e.Exception?.ToString() ?? "An unknown error occurred.";
        System.Diagnostics.Debug.WriteLine($"[UNHANDLED EXCEPTION] {errorMessage}");

        // Show error dialog on the UI thread
        _window?.DispatcherQueue?.TryEnqueue(async () =>
        {
            await ErrorDialogService.ShowErrorAsync(
                "Unexpected Error",
                $"An unexpected error occurred:\n\n{e.Message}\n\nIf this keeps happening, please restart the app.");
        });
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        var localFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HNReader");
        Directory.CreateDirectory(localFolder);
        var favouritesDbPath = Path.Combine(localFolder, "favorites.db");
        services.AddSingleton<IFavoritesService>(_ => new FavoritesService(favouritesDbPath));
        services.AddSingleton<ISettingsService>(_ => new SettingsService(localFolder));
        services.AddSingleton<IVaultFileService>(sp =>
        {
            var settings = sp.GetRequiredService<ISettingsService>();
            return new VaultFileService(settings.VaultPath);
        });
        // Content scraper for digest agent article fetching
        services.AddHttpClient<IContentScraperService, ContentScraperService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "HNReader/1.0");
        });

        services.AddSingleton<CopilotFunctions>();
        services.AddSingleton<CopilotCliService>();

        services.AddSingleton<TopPageViewModel>();
        services.AddSingleton<NewPageViewModel>();
        services.AddSingleton<FavouritesPageViewModel>();
        services.AddSingleton<BestPageViewModel>();
        services.AddSingleton<ShowPageViewModel>();
        services.AddSingleton<AskPageViewModel>();
        services.AddSingleton<NewsDigestViewModel>(sp => new NewsDigestViewModel(
            sp.GetRequiredService<ISettingsService>(),
            sp.GetRequiredService<CopilotCliService>(),
            sp.GetRequiredService<IVaultFileService>(),
            sp.GetRequiredService<IContentScraperService>(),
            sp.GetRequiredService<HNClient>()));
        services.AddSingleton<SettingsViewModel>();

        services.AddSingleton<MainViewModel>();

        services.AddSingleton<PageFactory>();
        services.AddSingleton<NavigationService>();

        services.AddTransient<TopPage>();
        services.AddTransient<NewPage>();
        services.AddTransient<FavouritesPage>();
        services.AddTransient<BestPage>();
        services.AddTransient<ShowPage>();
        services.AddTransient<AskPage>();
        services.AddTransient<NewsDigestPage>();
        services.AddTransient<SettingsPage>();

        // HN API client for stories
        services.AddHttpClient<HNClient>(client =>
        {
            client.BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        // HN Web client for faster comment loading (scrapes HTML instead of API)
        services.AddHttpClient<HNWebClient>(client =>
        {
            client.BaseAddress = new Uri("https://news.ycombinator.com/");
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Add("User-Agent", "HNReader/1.0");
        });


        services.AddSingleton<Func<ApplicationPages, BaseViewModel>>(x => name => name switch
        {
            ApplicationPages.Top => x.GetRequiredService<TopPageViewModel>(),
            ApplicationPages.New => x.GetRequiredService<NewPageViewModel>(),
            ApplicationPages.Favourites => x.GetRequiredService<FavouritesPageViewModel>(),
            ApplicationPages.Best => x.GetRequiredService<BestPageViewModel>(),
            ApplicationPages.Show => x.GetRequiredService<ShowPageViewModel>(),
            ApplicationPages.Ask => x.GetRequiredService<AskPageViewModel>(),
            ApplicationPages.NewsDigest => x.GetRequiredService<NewsDigestViewModel>(),
            ApplicationPages.Settings => x.GetRequiredService<SettingsViewModel>(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        });

        services.AddSingleton<Func<ApplicationPages, Page>>(x => name => name switch
        {
            ApplicationPages.Top => x.GetRequiredService<TopPage>(),
            ApplicationPages.New => x.GetRequiredService<NewPage>(),
            ApplicationPages.Favourites => x.GetRequiredService<FavouritesPage>(),
            ApplicationPages.Best => x.GetRequiredService<BestPage>(),
            ApplicationPages.Show => x.GetRequiredService<ShowPage>(),
            ApplicationPages.Ask => x.GetRequiredService<AskPage>(),
            ApplicationPages.NewsDigest => x.GetRequiredService<NewsDigestPage>(),
            ApplicationPages.Settings => x.GetRequiredService<SettingsPage>(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        });

        return services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var mainViewModel = Services.GetRequiredService<MainViewModel>();
        _window = new MainWindow(mainViewModel);
        CurrentWindow = _window;
        _window.Activate();

        // Apply theme after window is created
        ApplyCurrentTheme();
    }
}
