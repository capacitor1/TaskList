namespace TaskList.Data;

/// <summary>Resolves the writable data directory that lives next to the executable.</summary>
public sealed class AppPaths
{
    public string DataDir { get; }
    public string DatabaseFile => Path.Combine(DataDir, "tasks.db");
    public string SettingsFile => Path.Combine(DataDir, "settings.json");
    public string ErrorLogFile => Path.Combine(DataDir, "error.log");

    public AppPaths(string? baseDir = null)
    {
        DataDir = Path.Combine(baseDir ?? AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(DataDir);
    }
}
