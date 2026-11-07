namespace TaskList.Data;

public static class AppLog
{
    public static void Write(AppPaths paths, Exception ex)
    {
        try
        {
            File.AppendAllText(paths.ErrorLogFile,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n\r\n");
        }
        catch
        {
            // Logging must never throw.
        }
    }
}
