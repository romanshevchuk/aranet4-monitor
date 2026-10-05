using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Aranet4Monitor.Presentation;

public enum AppTheme
{
    Auto, Light, Dark
}

public static class ThemeResolver
{
    /// <summary>Auto follows the Windows "app mode" setting.</summary>
    public static bool IsDark(AppTheme mode, bool windowsUsesLightApps) => mode switch
    {
        AppTheme.Dark => true,
        AppTheme.Light => false,
        _ => !windowsUsesLightApps,
    };
}

/// <summary>Swaps the colour-token dictionary at runtime. Everything else references the tokens with DynamicResource.</summary>
public static class ThemeService
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static AppTheme mode = AppTheme.Auto;
    private static ResourceDictionary? themeDictionary;
    private static bool? appliedDark;
    private static bool hooked;

    /// <summary>Raised on the UI thread after the palette changed.</summary>
    public static event EventHandler? Changed;

    /// <summary>Increases with every palette change so cached drawing objects know when to rebuild.</summary>
    public static int Version { get; private set; }

    public static AppTheme Mode => mode;

    public static void Initialize(AppTheme initialMode)
    {
        mode = initialMode;
        if (!hooked)
        {
            hooked = true;
            SystemEvents.UserPreferenceChanged += (_, e) =>
            {
                if (e.Category == UserPreferenceCategory.General && System.Windows.Application.Current is { } app)
                {
                    app.Dispatcher.BeginInvoke(Apply);
                }
            };
        }

        Apply();
    }

    public static void SetMode(AppTheme newMode)
    {
        mode = newMode;
        Apply();
    }

    public static Brush GetBrush(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as Brush ?? Brushes.Transparent;

    public static Color GetColor(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) is Color color ? color : Colors.Transparent;

    public static Brush AccentBrush(MetricKind kind) => new SolidColorBrush(MetricColors.Accent(kind));

    private static void Apply()
    {
        var app = System.Windows.Application.Current;
        if (app is null)
        {
            return;
        }

        var dark = ThemeResolver.IsDark(mode, WindowsUsesLightApps());
        if (appliedDark == dark)
        {
            return;
        }

        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"/Aranet4Monitor;component/Presentation/Resources/Theme.{(dark ? "Dark" : "Light")}.xaml", UriKind.Relative),
        };
        var merged = app.Resources.MergedDictionaries;
        if (themeDictionary is not null)
        {
            merged.Remove(themeDictionary);
        }

        merged.Insert(0, dictionary);
        themeDictionary = dictionary;
        appliedDark = dark;
        Version++;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static bool WindowsUsesLightApps()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return true;
        }
    }
}
