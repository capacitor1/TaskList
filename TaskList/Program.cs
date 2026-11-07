using TaskList.Data;
using TaskList.Services;
using TaskList.UI;

namespace TaskList;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        SQLitePCL.Batteries_V2.Init();

        var paths = CreatePaths();
        if (paths == null) return;

        using var mutex = new Mutex(true, @"Local\TaskList.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("任务清单已经在运行。\n请通过系统托盘中的图标打开。", "任务清单",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            var settings = SettingsStore.Load(paths.SettingsFile);
            if (Theme.TryParseColor(settings.AccentColor, out var accent))
                Theme.SetAccent(accent);
            using var db = new TaskDatabase(paths.DatabaseFile);
            var service = new TaskService(db, settings);

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) =>
            {
                AppLog.Write(paths, e.Exception);
                MessageBox.Show($"程序遇到未处理的错误：\n{e.Exception.Message}",
                    "任务清单", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception ex) AppLog.Write(paths, ex);
            };

            Application.Run(new MainForm(service, settings, paths));
        }
        catch (Exception ex)
        {
            AppLog.Write(paths, ex);
            MessageBox.Show($"无法启动任务清单：\n{ex.Message}\n\n详细信息已写入：\n{paths.ErrorLogFile}",
                "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    static AppPaths? CreatePaths()
    {
        try
        {
            return new AppPaths();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法创建数据目录：\n{ex.Message}", "启动失败",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }
    }
}
