using System.Diagnostics;
using TaskList.Data;

namespace TaskList.UI;

public sealed class SettingsPage : UserControl
{
    readonly AppSettings _settings;
    readonly AppPaths _paths;
    readonly NumericUpDown _critical;
    readonly DateTimePicker _reset;
    readonly CheckBox _notifications;
    readonly ComboBox _closeBehavior;
    readonly Label _dataPath;
    readonly Label _savedHint;
    readonly ToolTip _toolTip;
    readonly NumericUpDown _accentR;
    readonly NumericUpDown _accentG;
    readonly NumericUpDown _accentB;
    readonly Panel _accentSwatch;
    readonly CardPanel _card1;
    readonly CardPanel _cardTheme;
    readonly CardPanel _card2;
    readonly Panel _content;

    public event Action? SettingsSaved;

    public SettingsPage(AppSettings settings, AppPaths paths)
    {
        _settings = settings;
        _paths = paths;
        _toolTip = new ToolTip
        {
            AutoPopDelay = 10000,
            InitialDelay = 400,
            ReshowDelay = 100,
            ShowAlways = true,
        };
        Dock = DockStyle.Fill;
        BackColor = Theme.Bg;
        AutoScaleMode = AutoScaleMode.Inherit;

        var header = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = Theme.Bg };
        var title = Ui.Label("设置", 18f, Theme.Text, FontStyle.Bold);
        title.Location = new Point(32, 20);
        var subtitle = Ui.Label("调整提醒阈值、托盘行为和数据存储。", 9.5f, Theme.SubText);
        subtitle.Location = new Point(34, 57);

        _savedHint = Ui.Label("", 9f, Theme.Success);
        _savedHint.AutoSize = false;
        _savedHint.Location = new Point(0, 36);
        _savedHint.Size = new Size(120, 22);
        _savedHint.TextAlign = ContentAlignment.MiddleRight;

        var save = new FlatButton
        {
            Text = "保存设置",
            Size = new Size(124, 40),
            Location = new Point(0, 24),
        };
        save.Click += (_, _) => Save();
        header.Resize += (_, _) =>
        {
            save.Location = new Point(header.ClientSize.Width - 32 - save.Width, 24);
            _savedHint.Location = new Point(save.Left - 8 - _savedHint.Width, 36);
        };
        header.Controls.AddRange([title, subtitle, _savedHint, save]);

        _content = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Bg,
            AutoScroll = true,
        };

        _card1 = new CardPanel { Location = new Point(32, 0), Size = new Size(820, 260) };

        var section1 = Ui.Label("提醒与通知", 12.5f, Theme.Text, FontStyle.Bold);
        section1.Location = new Point(24, 20);
        var criticalLabel = Ui.Label("重要提醒阈值", 9.5f, Theme.Text);
        criticalLabel.Location = new Point(24, 68);
        _critical = new NumericUpDown
        {
            Location = new Point(220, 64),
            Size = new Size(84, 30),
            Minimum = 0.5M,
            Maximum = 50M,
            DecimalPlaces = 1,
            Increment = 0.5M,
            Value = 5M,
            Font = Theme.Font(9.5f),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
        };
        var percent = Ui.Label("%", 9.5f, Theme.SubText);
        percent.Location = new Point(310, 68);
        var criticalHint = Ui.Label(
            "剩余时间低于任务总时长的该比例时，任务标题和截止时间标红，并显示倒计时。",
            8.8f, Theme.Muted);
        criticalHint.Location = new Point(24, 96);

        var resetLabel = Ui.Label("默认首个触发时间", 9.5f, Theme.Text);
        resetLabel.Location = new Point(24, 138);
        _reset = new DateTimePicker
        {
            Location = new Point(220, 134),
            Size = new Size(100, 30),
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "HH:mm",
            ShowUpDown = true,
            Font = Theme.Font(9.5f),
        };
        var resetHint = Ui.Label("新建多次任务时时间轴的第一个检查点，可在每个任务里单独修改。", 8.8f, Theme.Muted);
        resetHint.Location = new Point(24, 166);

        _notifications = new CheckBox
        {
            Text = "任务进入提醒区间时发送系统通知",
            Location = new Point(24, 206),
            AutoSize = true,
            Font = Theme.Font(9.5f),
            ForeColor = Theme.Text,
            BackColor = Color.White,
        };
        _card1.Controls.AddRange([section1, criticalLabel, _critical, percent, criticalHint,
            resetLabel, _reset, resetHint, _notifications]);

        _cardTheme = new CardPanel { Location = new Point(32, 280), Size = new Size(820, 132) };
        var sectionTheme = Ui.Label("外观", 12.5f, Theme.Text, FontStyle.Bold);
        sectionTheme.Location = new Point(24, 20);
        var accentLabel = Ui.Label("强调色 RGB", 9.5f, Theme.Text);
        accentLabel.Location = new Point(24, 64);
        _accentR = AccentSpinner(208, 60);
        _accentG = AccentSpinner(294, 60);
        _accentB = AccentSpinner(380, 60);
        var rLabel = Ui.Label("R", 9f, Theme.SubText);
        rLabel.Location = new Point(190, 65);
        var gLabel = Ui.Label("G", 9f, Theme.SubText);
        gLabel.Location = new Point(276, 65);
        var bLabel = Ui.Label("B", 9f, Theme.SubText);
        bLabel.Location = new Point(362, 65);
        _accentSwatch = new Panel
        {
            Location = new Point(468, 61),
            Size = new Size(52, 30),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.Accent,
        };
        var pickColor = new FlatButton
        {
            Text = "选择颜色",
            Style = FlatButtonStyle.Secondary,
            Location = new Point(532, 58),
            Size = new Size(108, 36),
        };
        pickColor.Click += (_, _) => PickAccentColor();
        var accentHint = Ui.Label("应用到主要按钮、导航选中状态和任务完成按钮。", 8.8f, Theme.Muted);
        accentHint.Location = new Point(24, 98);
        _cardTheme.Controls.AddRange([sectionTheme, accentLabel, rLabel, gLabel, bLabel,
            _accentR, _accentG, _accentB, _accentSwatch, pickColor, accentHint]);

        _card2 = new CardPanel { Location = new Point(32, 430), Size = new Size(820, 148) };

        var section2 = Ui.Label("窗口与数据", 12.5f, Theme.Text, FontStyle.Bold);
        section2.Location = new Point(24, 20);
        var closeLabel = Ui.Label("关闭主窗口时", 9.5f, Theme.Text);
        closeLabel.Location = new Point(24, 64);
        _closeBehavior = new ComboBox
        {
            Location = new Point(220, 60),
            Size = new Size(180, 30),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = Theme.Font(9.5f),
            BackColor = Color.White,
        };
        _closeBehavior.Items.AddRange(["直接退出", "最小化到托盘"]);
        _closeBehavior.SelectedIndex = 0;

        var pathLabel = Ui.Label("数据目录", 9.5f, Theme.Text);
        pathLabel.Location = new Point(24, 100);
        _dataPath = new Label
        {
            Location = new Point(220, 96),
            Size = new Size(560, 24),
            AutoSize = false,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = Theme.Font(8.9f, FontStyle.Underline),
            ForeColor = Theme.Accent,
            BackColor = Color.White,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };
        _dataPath.Click += (_, _) => OpenDataFolder();
        _dataPath.MouseEnter += (_, _) => _dataPath.ForeColor = Theme.AccentHover;
        _dataPath.MouseLeave += (_, _) => _dataPath.ForeColor = Theme.Accent;
        _toolTip.SetToolTip(_dataPath, $"点击打开：{_paths.DataDir}");
        _card2.Controls.AddRange([section2, closeLabel, _closeBehavior, pathLabel, _dataPath]);

        _content.Controls.AddRange([_card1, _cardTheme, _card2]);
        _content.ClientSizeChanged += (_, _) => ResizeCards();
        Controls.Add(_content);
        Controls.Add(header);
        ResizeCards();
        LoadValues();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _toolTip.Dispose();
        base.Dispose(disposing);
    }

    void ResizeCards()
    {
        var width = Math.Max(520, _content.ClientSize.Width - 64);
        _card1.Width = width;
        _cardTheme.Width = width;
        _card2.Width = width;
    }

    NumericUpDown AccentSpinner(int x, int y)
    {
        var spinner = new NumericUpDown
        {
            Location = new Point(x, y),
            Size = new Size(64, 30),
            Minimum = 0,
            Maximum = 255,
            Font = Theme.Font(9.5f),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
        };
        spinner.ValueChanged += (_, _) => RefreshAccentPreview();
        return spinner;
    }

    void PickAccentColor()
    {
        using var dialog = new ColorDialog
        {
            Color = CurrentAccent(),
            FullOpen = true,
            AnyColor = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _accentR.Value = dialog.Color.R;
        _accentG.Value = dialog.Color.G;
        _accentB.Value = dialog.Color.B;
        RefreshAccentPreview();
    }

    Color CurrentAccent() =>
        Color.FromArgb((int)_accentR.Value, (int)_accentG.Value, (int)_accentB.Value);

    public void RefreshAccentPreview()
    {
        _accentSwatch.BackColor = CurrentAccent();
        _dataPath.ForeColor = Theme.Accent;
    }

    public void LoadValues()
    {
        _critical.Value = (decimal)Math.Clamp(_settings.CriticalPercent, 0.5, 50);
        _reset.Value = DateTime.Today.AddMinutes(Math.Clamp(_settings.DefaultResetMinutes, 0, 1439));
        _notifications.Checked = _settings.NotificationsEnabled;
        _closeBehavior.SelectedIndex = _settings.CloseToTray ? 1 : 0;
        var accent = Theme.TryParseColor(_settings.AccentColor, out var parsed)
            ? parsed
            : Color.FromArgb(76, 102, 229);
        _accentR.Value = accent.R;
        _accentG.Value = accent.G;
        _accentB.Value = accent.B;
        RefreshAccentPreview();
        _dataPath.Text = _paths.DataDir;
        _savedHint.Text = "";
    }

    void Save()
    {
        var updated = new AppSettings
        {
            CriticalPercent = (double)_critical.Value,
            DefaultResetMinutes = _reset.Value.Hour * 60 + _reset.Value.Minute,
            NotificationsEnabled = _notifications.Checked,
            CloseToTray = _closeBehavior.SelectedIndex == 1,
            AccentColor = Theme.ToHex(CurrentAccent()),
        };
        try
        {
            SettingsStore.Save(_paths.SettingsFile, updated);
            _settings.CriticalPercent = updated.CriticalPercent;
            _settings.DefaultResetMinutes = updated.DefaultResetMinutes;
            _settings.NotificationsEnabled = updated.NotificationsEnabled;
            _settings.CloseToTray = updated.CloseToTray;
            _settings.AccentColor = updated.AccentColor;
            _savedHint.Text = $"已保存 {DateTime.Now:HH:mm:ss}";
            SettingsSaved?.Invoke();
        }
        catch (Exception ex)
        {
            _savedHint.Text = "";
            MessageBox.Show(this, $"保存设置失败：\n{ex.Message}", "设置",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void OpenDataFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _paths.DataDir,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"无法打开数据目录：\n{ex.Message}", "设置",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
