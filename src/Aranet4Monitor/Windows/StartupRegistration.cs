using Microsoft.Win32;

namespace Aranet4Monitor.Windows;

/// <summary>"Start with Windows" via the per-user Run key (no admin rights needed).</summary>
public static class StartupRegistration
{
    /// <summary>Passed when Windows starts the app, so it can begin hidden in the tray.</summary>
    public const string TrayArgument = "--tray";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AranetHome";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                return key?.GetValue(ValueName) is string;
            }
            catch (Exception) { return false; }
        }
    }

    /// <summary>Returns false if the setting could not be changed.</summary>
    public static bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return true;
            }

            var path = Environment.ProcessPath;
            // When launched as "dotnet Aranet4Monitor.dll" the process is dotnet.exe; registering that would be wrong.
            if (path is null || Path.GetFileNameWithoutExtension(path).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            key.SetValue(ValueName, $"\"{path}\" {TrayArgument}");
            return true;
        }
        catch (Exception) { return false; }
    }
}
