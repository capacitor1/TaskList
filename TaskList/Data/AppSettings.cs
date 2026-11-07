using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskList.Data;

public sealed class AppSettings
{
    /// <summary>Task turns red when remaining time is below this percentage of its total span.</summary>
    public double CriticalPercent { get; set; } = 5.0;

    /// <summary>Default first trigger time (minutes after midnight) for new repeating tasks.</summary>
    public int DefaultResetMinutes { get; set; } = 9 * 60;

    public bool NotificationsEnabled { get; set; } = true;

    /// <summary>When true, closing the main window hides it to the notification area.</summary>
    public bool CloseToTray { get; set; }

    /// <summary>Accent color as #RRGGBB; the default is the original blue.</summary>
    public string AccentColor { get; set; } = "#4C66E5";
}

public static class SettingsStore
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static AppSettings Load(string path)
    {
        AppSettings settings;
        try
        {
            settings = File.Exists(path)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions) ?? new AppSettings()
                : new AppSettings();
        }
        catch
        {
            TryBackupBrokenFile(path);
            settings = new AppSettings();
        }

        settings.CriticalPercent = Math.Clamp(settings.CriticalPercent, 0.5, 50);
        settings.DefaultResetMinutes = Math.Clamp(settings.DefaultResetMinutes, 0, 1439);
        if (!UI.Theme.TryParseColor(settings.AccentColor, out _))
            settings.AccentColor = "#4C66E5";
        try
        {
            Save(path, settings);
        }
        catch
        {
            // A read-only settings file must not prevent the app from starting.
        }
        return settings;
    }

    public static void Save(string path, AppSettings settings)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(tmp, path, overwrite: true);
    }

    static void TryBackupBrokenFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            var backup = $"{path}.bad-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Move(path, backup, overwrite: true);
        }
        catch
        {
            // A broken settings file must never prevent the app from starting.
        }
    }
}
