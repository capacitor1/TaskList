using System.Drawing.Drawing2D;
using TaskList.Data;
using TaskList.Models;
using TaskList.Services;

namespace TaskList.UI;

public sealed class MainForm : Form
{
    readonly TaskService _service;
    readonly AppSettings _settings;
    readonly AppPaths _paths;

    readonly HomePage _home;
    readonly LibraryPage _library;
    readonly SettingsPage _settingsPage;
    readonly NavButton _navHome;
    readonly NavButton _navLibrary;
    readonly NavButton _navSettings;
    readonly NotifyIcon _tray;
    readonly ContextMenuStrip _trayMenu;
    readonly System.Windows.Forms.Timer _timer;

    Panel _sidebar = null!;
    PictureBox _logo = null!;
    Label _appName = null!;
    Label _appVersion = null!;
    FlatButton _collapse = null!;
    bool _sidebarCollapsed;

    readonly HashSet<string> _notified = [];
    string _homeSignature = "";
    bool _reallyExit;
    bool _trayHintShown;
    int _tick;

    public MainForm(TaskService service, AppSettings settings, AppPaths paths)
    {
        _service = service;
        _settings = settings;
        _paths = paths;

        Text = "任务清单";
        Icon = AppIcon.Create();
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1160, 740);
        MinimumSize = new Size(960, 640);
        BackColor = Theme.Bg;
        Font = Theme.Font(9.5f);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        DoubleBuffered = true;

        var content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };

        _home = new HomePage(service);
        _library = new LibraryPage(service);
        _settingsPage = new SettingsPage(settings, paths);
        content.Controls.AddRange([_home, _library, _settingsPage]);

        var sidebar = BuildSidebar(out _navHome, out _navLibrary, out _navSettings);
        Controls.Add(content);
        Controls.Add(sidebar);

        _home.AddRequested += AddTask;
        _home.EditRequested += EditTask;
        _home.DeleteRequested += DeleteTask;
        _home.CompleteRequested += CompleteTask;
        _library.EditRequested += EditTask;
        _library.DeleteRequested += DeleteTask;
        _settingsPage.SettingsSaved += OnSettingsSaved;

        _navHome.Click += (_, _) => ShowPage(_home, _navHome);
        _navLibrary.Click += (_, _) => ShowPage(_library, _navLibrary);
        _navSettings.Click += (_, _) => ShowPage(_settingsPage, _navSettings);

        ShowPage(_home, _navHome, refresh: false);

        (_tray, _trayMenu) = BuildTray();
        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += OnTick;

        Shown += (_, _) =>
        {
            var area = Screen.FromControl(this).WorkingArea;
            if (Width > area.Width) Width = area.Width;
            if (Height > area.Height) Height = area.Height;
            ApplyTheme();
            _home.RefreshData();
            _homeSignature = HomeSignature(UtcTime.Now);
            PrimeNotifications(UtcTime.Now);
            _timer.Start();
        };
        FormClosing += OnFormClosing;
        FormClosed += (_, _) =>
        {
            _timer.Stop();
            _timer.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            _trayMenu.Dispose();
        };
    }

    Control BuildSidebar(out NavButton navHome, out NavButton navLibrary, out NavButton navSettings)
    {
        _sidebar = new Panel { Dock = DockStyle.Left, Width = 200, BackColor = Theme.Surface };
        _sidebar.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border, 1f);
            e.Graphics.DrawLine(pen, _sidebar.Width - 1, 0, _sidebar.Width - 1, _sidebar.Height);
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 156));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface };
        _logo = new PictureBox
        {
            Size = new Size(36, 36),
            Location = new Point(20, 28),
            SizeMode = PictureBoxSizeMode.StretchImage,
            BackColor = Color.Transparent,
            Image = AppIcon.Bitmap(36),
        };
        _appName = Ui.Label("任务清单", 13f, Theme.Text, FontStyle.Bold);
        _appName.Location = new Point(68, 30);
        _appVersion = Ui.Label("v1.0 · 本地待办", 8.4f, Theme.Muted);
        _appVersion.Location = new Point(69, 56);
        header.Controls.AddRange([_logo, _appName, _appVersion]);

        var nav = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12, 0, 12, 0),
            BackColor = Theme.Surface,
        };
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 3; i++) nav.RowStyles.Add(new RowStyle(SizeType.Percent, 33.34f));

        navHome = new NavButton { Text = "主页", Icon = "", Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4) };
        navLibrary = new NavButton { Text = "任务库", Icon = "", Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4) };
        navSettings = new NavButton { Text = "设置", Icon = "", Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4) };
        nav.Controls.Add(navHome, 0, 0);
        nav.Controls.Add(navLibrary, 0, 1);
        nav.Controls.Add(navSettings, 0, 2);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            Padding = new Padding(12, 4, 12, 12),
            BackColor = Theme.Surface,
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _collapse = new FlatButton
        {
            Text = "«  收起",
            Style = FlatButtonStyle.Ghost,
            Dock = DockStyle.Fill,
            Font = Theme.Font(9.2f),
            Margin = Padding.Empty,
        };
        _collapse.Click += (_, _) => SetSidebarCollapsed(!_sidebarCollapsed);
        footer.Controls.Add(_collapse, 0, 0);

        var spacer = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface };
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(nav, 0, 1);
        layout.Controls.Add(spacer, 0, 2);
        layout.Controls.Add(footer, 0, 3);
        _sidebar.Controls.Add(layout);
        return _sidebar;
    }

    void SetSidebarCollapsed(bool collapsed)
    {
        _sidebarCollapsed = collapsed;
        _sidebar.Width = collapsed ? 68 : 200;
        _logo.Left = collapsed ? (68 - _logo.Width) / 2 : 20;
        _appName.Visible = !collapsed;
        _appVersion.Visible = !collapsed;
        _navHome.Compact = collapsed;
        _navLibrary.Compact = collapsed;
        _navSettings.Compact = collapsed;
        _collapse.Text = collapsed ? "»" : "«  收起";
    }

    (NotifyIcon Tray, ContextMenuStrip Menu) BuildTray()
    {
        var tray = new NotifyIcon
        {
            Icon = Icon,
            Text = "任务清单 v1.0",
            Visible = true,
        };
        var menu = Ui.CreateMenu();
        var open = new ToolStripMenuItem("打开任务清单");
        open.Click += (_, _) => ShowMainWindow();
        var add = new ToolStripMenuItem("新建任务");
        add.Click += (_, _) => { ShowMainWindow(); AddTask(); };
        var exit = new ToolStripMenuItem("退出");
        exit.Click += (_, _) => { _reallyExit = true; Close(); };
        menu.Items.AddRange([open, add, new ToolStripSeparator(), exit]);
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => ShowMainWindow();
        return (tray, menu);
    }

    void ShowMainWindow()
    {
        ShowInTaskbar = true;
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        if (_home.Visible) _home.RefreshData();
        else if (_library.Visible) _library.RefreshData();
        _homeSignature = HomeSignature(UtcTime.Now);
        Activate();
        BringToFront();
    }

    void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
        if (_trayHintShown) return;
        _trayHintShown = true;
        _tray.ShowBalloonTip(3000, "任务清单", "已最小化到托盘，双击图标可以重新打开。",
            ToolTipIcon.Info);
    }

    void ShowPage(Control page, NavButton nav, bool refresh = true)
    {
        _home.Visible = page == _home;
        _library.Visible = page == _library;
        _settingsPage.Visible = page == _settingsPage;
        _navHome.Selected = nav == _navHome;
        _navLibrary.Selected = nav == _navLibrary;
        _navSettings.Selected = nav == _navSettings;
        if (!refresh) return;
        if (page == _home) _home.RefreshData();
        else if (page == _library) _library.RefreshData();
        else _settingsPage.LoadValues();
        _homeSignature = HomeSignature(UtcTime.Now);
    }

    void OnSettingsSaved()
    {
        ApplyTheme();
        if (_settings.NotificationsEnabled) PrimeNotifications(UtcTime.Now);
        else _notified.Clear();
        if (_home.Visible) _home.RefreshData();
        if (_library.Visible) _library.RefreshData();
        _homeSignature = HomeSignature(UtcTime.Now);
    }

    void ApplyTheme()
    {
        if (Theme.TryParseColor(_settings.AccentColor, out var accent))
            Theme.SetAccent(accent);
        _library.ApplyTheme();
        _settingsPage.RefreshAccentPreview();
        Invalidate(true);
    }

    void AddTask()
    {
        _timer.Stop();
        try
        {
            using var dialog = new TaskEditForm(_service, _settings, null);
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result == null) return;
            _service.Insert(dialog.Result);
            RefreshVisiblePage();
        }
        catch (Exception ex)
        {
            ShowError("新建任务失败", ex);
        }
        finally
        {
            _timer.Start();
        }
    }

    void EditTask(TaskItem task)
    {
        _timer.Stop();
        try
        {
            using var dialog = new TaskEditForm(_service, _settings, task);
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result == null) return;
            if (task.Archived) _service.Insert(dialog.Result);
            else _service.Update(dialog.Result);
            RefreshVisiblePage();
        }
        catch (Exception ex)
        {
            ShowError("保存任务失败", ex);
        }
        finally
        {
            _timer.Start();
        }
    }

    void CompleteTask(TaskItem task)
    {
        if (task.Archived) return;
        var message = task.IsRepeating
            ? $"确认已完成任务「{task.Title}」的本次触发吗？"
            : $"确认已完成任务「{task.Title}」吗？";
        if (MessageBox.Show(this, message, "完成任务", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes) return;

        try
        {
            if (_service.Complete(task, UtcTime.Now))
                RefreshVisiblePage();
        }
        catch (Exception ex)
        {
            ShowError("更新任务失败", ex);
        }
    }

    void DeleteTask(TaskItem task)
    {
        var message = $"确定要永久删除任务「{task.Title}」吗？\n\n此操作不可撤销。";
        if (MessageBox.Show(this, message, "删除任务", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes) return;

        try
        {
            _service.Delete(task);
            RefreshVisiblePage();
        }
        catch (Exception ex)
        {
            ShowError("删除任务失败", ex);
        }
    }

    void RefreshVisiblePage()
    {
        if (_home.Visible) _home.RefreshData();
        if (_library.Visible) _library.RefreshData();
        _homeSignature = HomeSignature(UtcTime.Now);
    }

    string HomeSignature(DateTime now) =>
        string.Join(',', _service.HomeTasks(now).Select(t => $"{t.Id}:{_service.OccurrenceKey(t, now)}"));

    void OnTick(object? sender, EventArgs e)
    {
        var now = UtcTime.Now;
        if (_home.Visible) _home.Tick();

        _tick++;
        if (_tick % 2 == 0)
        {
            var signature = HomeSignature(now);
            if (signature != _homeSignature)
            {
                _homeSignature = signature;
                if (_home.Visible)
                {
                    _home.RefreshData();
                    _homeSignature = HomeSignature(now);
                }
            }
        }
        if (_tick % 30 == 0)
        {
            try
            {
                if (_home.Visible) _home.RefreshData();
                else if (_library.Visible) _library.RefreshData();
                else
                {
                    _service.Reload();
                    _service.ArchiveExpired(now);
                }
                _homeSignature = HomeSignature(now);
            }
            catch (Exception ex)
            {
                AppLog.Write(_paths, ex);
            }
        }

        UpdateNotifications(now);
    }

    void PrimeNotifications(DateTime now)
    {
        _notified.Clear();
        if (_settings.NotificationsEnabled)
            foreach (var task in _service.HomeTasks(now))
                if (_service.IsCritical(task, now))
                    _notified.Add(NotificationKey(task, now));
    }

    void UpdateNotifications(DateTime now)
    {
        if (!_settings.NotificationsEnabled)
        {
            _notified.Clear();
            return;
        }

        var keys = new HashSet<string>();
        foreach (var task in _service.HomeTasks(now))
        {
            if (!_service.IsCritical(task, now)) continue;
            var key = NotificationKey(task, now);
            keys.Add(key);
            if (!_notified.Contains(key))
            {
                var deadline = _service.EffectiveDeadline(task, now);
                var remaining = deadline.HasValue
                    ? TaskService.FormatDuration(deadline.Value - now)
                    : "";
                _tray.ShowBalloonTip(6000, "任务即将到期",
                    $"{task.Title}\n剩余 {remaining}", ToolTipIcon.Info);
                _notified.Add(key);
            }
        }
        _notified.IntersectWith(keys);
    }

    string NotificationKey(TaskItem task, DateTime now) =>
        $"{task.Id}:{(task.IsRepeating ? _service.OccurrenceKey(task, now) : 0)}";

    void ShowError(string title, Exception ex)
    {
        AppLog.Write(_paths, ex);
        MessageBox.Show(this, $"{title}：\n{ex.Message}", title,
            MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_reallyExit || !_settings.CloseToTray) return;
        if (e.CloseReason != CloseReason.UserClosing) return;
        e.Cancel = true;
        HideToTray();
    }
}
