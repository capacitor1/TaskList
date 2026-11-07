using TaskList.Data;
using TaskList.Models;
using TaskList.Services;

namespace TaskList.UI;

/// <summary>Create, edit, or reuse a task. The dialog validates input and builds the result.</summary>
public sealed class TaskEditForm : Form
{
    sealed record RepeatOption(string Text, RepeatKind Kind)
    {
        public override string ToString() => Text;
    }

    const int MarginX = 30;
    const int ContentWidth = 700;
    const int DetailTop = 332;
    const int DailyDetailHeight = 142;
    const int WeeklyDetailHeight = 206;
    const int MonthlyDetailHeight = 374;

    readonly TaskService _service;
    readonly AppSettings _settings;
    readonly TaskItem? _source;

    readonly TextBox _title;
    readonly NumericUpDown _importance;
    readonly Badge _importanceBadge;
    readonly RadioButton _rbOnce;
    readonly RadioButton _rbRepeat;
    readonly Panel _repeatPanel;
    readonly ComboBox _repeatKind;
    readonly Label _repeatHint;
    readonly Panel _repeatDetail;
    readonly Label _timelineLabel;
    readonly TimelineControl _timeline;
    readonly Label _checkpointLabel;
    readonly DateTimePicker _checkpointTime;
    readonly Label _checkpointHint;
    readonly Label _weekdayLabel;
    readonly WeekdayPicker _weekdays;
    readonly MonthDayPicker _monthDays;
    readonly CheckBox _chkStart;
    readonly DateTimePicker _startPicker;
    readonly Label _startLabel;
    readonly Label _startHint;
    readonly Label _dueLabel;
    readonly SegmentedControl _dueMode;
    readonly Panel _dueDateRow;
    readonly CheckBox _chkDue;
    readonly DateTimePicker _duePicker;
    readonly Label _dueDateHint;
    readonly Panel _dueDurationRow;
    readonly NumericUpDown _durDays;
    readonly NumericUpDown _durHours;
    readonly NumericUpDown _durMinutes;
    readonly NumericUpDown _durSeconds;
    readonly Panel _previewHost;
    readonly Label _preview;
    readonly FlatButton _save;
    readonly FlatButton _cancel;

    bool _loading = true;
    bool _updatingCheckpoint;
    bool _updatingDueMode;
    bool _fitting;

    public TaskItem? Result { get; private set; }

    public TaskEditForm(TaskService service, AppSettings settings, TaskItem? source)
    {
        _service = service;
        _settings = settings;
        _source = source;

        var reuse = source?.Archived == true;

        Text = source == null ? "新建任务" : reuse ? $"复用任务 #{source.Id}" : $"修改任务 #{source.Id}";
        Icon = AppIcon.Create();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.White;
        ClientSize = new Size(760, 820);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScroll = true;
        Font = Theme.Font(9.5f);

        var headerText = source == null
            ? "新建任务"
            : reuse
                ? $"复用任务 #{source.Id} · 保存后创建新任务"
                : $"修改任务 #{source.Id}";
        var header = Ui.Label(headerText, 17f, Theme.Text, FontStyle.Bold);
        header.Location = new Point(MarginX, 20);
        var line = new Panel { Location = new Point(MarginX, 58), Size = new Size(ContentWidth, 1), BackColor = Theme.Border };

        var titleLabel = Ui.Label("任务内容 *", 9.5f, Theme.Text, FontStyle.Bold);
        titleLabel.Location = new Point(MarginX, 72);
        _title = new TextBox
        {
            Location = new Point(MarginX, 94),
            Size = new Size(ContentWidth, 64),
            Multiline = true,
            AcceptsReturn = true,
            ScrollBars = ScrollBars.Vertical,
            MaxLength = 500,
            Font = Theme.Font(10.5f),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
        };

        var importanceLabel = Ui.Label("重要等级", 9.5f, Theme.Text, FontStyle.Bold);
        importanceLabel.Location = new Point(MarginX, 170);
        _importance = new WheelStepNumericUpDown
        {
            Location = new Point(MarginX, 192),
            Size = new Size(70, 30),
            Minimum = 0,
            Maximum = 10,
            Increment = 1,
            Value = 5,
            Font = Theme.Font(10f),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
        };
        _importanceBadge = new Badge { Location = new Point(MarginX + 82, 194), Size = new Size(92, 28) };
        var importanceHint = Ui.Label("0 紧急 · 1-3 重要 · 4-6 次要 · 7-9 普通 · 10 不重要",
            8.6f, Theme.Muted);
        importanceHint.Location = new Point(MarginX + 190, 198);

        var typeLabel = Ui.Label("任务类型", 9.5f, Theme.Text, FontStyle.Bold);
        typeLabel.Location = new Point(MarginX, 232);
        _rbOnce = new RadioButton
        {
            Text = "单次",
            Location = new Point(MarginX, 256),
            AutoSize = true,
            Checked = true,
            Font = Theme.Font(10f),
            ForeColor = Theme.Text,
            BackColor = Color.White,
        };
        _rbRepeat = new RadioButton
        {
            Text = "多次",
            Location = new Point(MarginX + 100, 256),
            AutoSize = true,
            Font = Theme.Font(10f),
            ForeColor = Theme.Text,
            BackColor = Color.White,
        };

        _repeatPanel = new Panel
        {
            Location = new Point(MarginX, 290),
            Size = new Size(ContentWidth, 36),
            BackColor = Color.White,
        };
        _repeatKind = new ComboBox
        {
            Location = new Point(0, 2),
            Size = new Size(128, 30),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = Theme.Font(9.5f),
            BackColor = Color.White,
        };
        _repeatKind.Items.AddRange([
            new RepeatOption("每天…", RepeatKind.Daily),
            new RepeatOption("每周…", RepeatKind.Weekly),
            new RepeatOption("每月…", RepeatKind.Monthly),
        ]);
        _repeatKind.SelectedIndex = 0;
        _repeatHint = Ui.Label("", 9.2f, Theme.Muted);
        _repeatHint.Location = new Point(142, 8);
        _repeatPanel.Controls.AddRange([_repeatKind, _repeatHint]);

        _repeatDetail = new Panel
        {
            Location = new Point(MarginX, DetailTop),
            Size = new Size(ContentWidth, DailyDetailHeight),
            BackColor = Color.White,
        };

        _timelineLabel = Ui.Label("触发器", 9.2f, Theme.Text, FontStyle.Bold);
        _timelineLabel.Location = new Point(0, 0);
        _timeline = new TimelineControl
        {
            Location = new Point(0, 20),
            Size = new Size(ContentWidth, 80),
        };
        _checkpointLabel = Ui.Label("选中检查点", 9f, Theme.SubText);
        _checkpointLabel.Location = new Point(0, 108);
        _checkpointTime = new DateTimePicker
        {
            Location = new Point(84, 104),
            Size = new Size(110, 30),
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "HH:mm:ss",
            ShowUpDown = true,
            Font = Theme.Font(9.5f),
        };
        _checkpointHint = Ui.Label("可添加多个检查点", 8.6f, Theme.Muted);
        _checkpointHint.Location = new Point(204, 110);

        _weekdayLabel = Ui.Label("执行日（未选中的日期不触发）", 9f, Theme.SubText);
        _weekdayLabel.Location = new Point(0, 146);
        _weekdays = new WeekdayPicker
        {
            Location = new Point(0, 166),
            Size = new Size(ContentWidth, 40),
        };
        _monthDays = new MonthDayPicker
        {
            Location = new Point(0, 146),
            Size = new Size(ContentWidth, 228),
        };

        _repeatDetail.Controls.AddRange([
            _timelineLabel, _timeline, _checkpointLabel, _checkpointTime,
            _checkpointHint, _weekdayLabel, _weekdays, _monthDays,
        ]);

        _startLabel = Ui.Label("开始时间", 9.5f, Theme.Text, FontStyle.Bold);
        _chkStart = new CheckBox
        {
            Text = "指定",
            AutoSize = true,
            Font = Theme.Font(9.5f),
            ForeColor = Theme.Text,
            BackColor = Color.White,
        };
        _startPicker = new DateTimePicker
        {
            Size = new Size(220, 30),
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy-MM-dd HH:mm:ss",
            Font = Theme.Font(9.5f),
            Enabled = false,
        };
        _startHint = Ui.Label("不勾选则从当前时间开始", 8.6f, Theme.Muted);

        _dueLabel = Ui.Label("截止时间", 9.5f, Theme.Text, FontStyle.Bold);
        _dueMode = new SegmentedControl("选择日期", "选择时长")
        {
            Size = new Size(220, 34),
            Font = Theme.Font(9.2f, FontStyle.Bold),
        };
        _dueDateRow = new Panel { Size = new Size(ContentWidth, 36), BackColor = Color.White };
        _chkDue = new CheckBox
        {
            Text = "指定",
            Location = new Point(0, 6),
            AutoSize = true,
            Font = Theme.Font(9.5f),
            ForeColor = Theme.Text,
            BackColor = Color.White,
        };
        _duePicker = new DateTimePicker
        {
            Location = new Point(70, 2),
            Size = new Size(220, 30),
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy-MM-dd HH:mm:ss",
            Font = Theme.Font(9.5f),
            Enabled = false,
        };
        _dueDateHint = Ui.Label("", 8.6f, Theme.Muted);
        _dueDateHint.Location = new Point(310, 8);
        _dueDateRow.Controls.AddRange([_chkDue, _duePicker, _dueDateHint]);

        _dueDurationRow = new Panel { Size = new Size(ContentWidth, 36), BackColor = Color.White };
        _durDays = DurationSpinner(0, 0, 36500);
        _durHours = DurationSpinner(100, 0, 23);
        _durMinutes = DurationSpinner(200, 0, 59);
        _durSeconds = DurationSpinner(300, 0, 59);
        var durDaysLabel = Ui.Label("天", 9f, Theme.SubText);
        durDaysLabel.Location = new Point(66, 8);
        var durHoursLabel = Ui.Label("时", 9f, Theme.SubText);
        durHoursLabel.Location = new Point(166, 8);
        var durMinutesLabel = Ui.Label("分", 9f, Theme.SubText);
        durMinutesLabel.Location = new Point(266, 8);
        var durSecondsLabel = Ui.Label("秒", 9f, Theme.SubText);
        durSecondsLabel.Location = new Point(366, 8);
        var durHint = Ui.Label("从开始时间起计算，保存时换算为具体截止日期。", 8.6f, Theme.Muted);
        durHint.Location = new Point(406, 8);
        _dueDurationRow.Controls.AddRange([
            _durDays, durDaysLabel, _durHours, durHoursLabel, _durMinutes, durMinutesLabel,
            _durSeconds, durSecondsLabel, durHint,
        ]);

        _previewHost = new Panel
        {
            Size = new Size(ContentWidth, 88),
            BackColor = Color.FromArgb(247, 249, 253),
            BorderStyle = BorderStyle.FixedSingle,
        };
        _preview = new Label
        {
            Location = new Point(12, 8),
            Size = new Size(ContentWidth - 26, 72),
            AutoSize = false,
            Font = Theme.Font(8.9f),
            ForeColor = Theme.SubText,
            BackColor = Color.Transparent,
        };
        _previewHost.Controls.Add(_preview);

        _cancel = new FlatButton
        {
            Text = "取消",
            Style = FlatButtonStyle.Secondary,
            Size = new Size(110, 42),
        };
        _save = new FlatButton
        {
            Text = "保存任务",
            Size = new Size(150, 42),
        };

        Controls.AddRange([
            header, line, titleLabel, _title, importanceLabel, _importance, _importanceBadge,
            importanceHint, typeLabel, _rbOnce, _rbRepeat, _repeatPanel, _repeatDetail,
            _startLabel, _chkStart, _startPicker, _startHint, _dueLabel, _dueMode,
            _dueDateRow, _dueDurationRow, _previewHost, _cancel, _save,
        ]);

        AcceptButton = _save;
        CancelButton = _cancel;

        LoadSource();
        WireEvents();
        _loading = false;
        UpdateMode();
        UpdatePreview();
    }

    NumericUpDown DurationSpinner(int x, int min, int max) => new WheelStepNumericUpDown
    {
        Location = new Point(x, 2),
        Size = new Size(60, 30),
        Minimum = min,
        Maximum = max,
        Increment = 1,
        Value = 0,
        Font = Theme.Font(9.5f),
        BackColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle,
    };

    void LoadSource()
    {
        var now = UtcTime.Now;
        _startPicker.Value = UtcTime.ToLocal(now);
        _duePicker.Value = UtcTime.ToLocal(now.AddDays(1));
        _dueMode.SelectedIndex = 0;
        _durDays.Value = 1;
        _timeline.SetPoints(DefaultTriggers());
        _weekdays.Mask = TaskItem.DefaultWeekDays;
        _monthDays.Mask = TaskItem.DefaultMonthDays;

        if (_source == null) return;

        _title.Text = _source.Title;
        _importance.Value = Math.Clamp(_source.Importance, 0, 10);
        _repeatKind.SelectedIndex = _source.Repeat switch
        {
            RepeatKind.Daily => 0,
            RepeatKind.Weekly => 1,
            RepeatKind.Monthly => 2,
            _ => 0,
        };
        _rbOnce.Checked = !_source.IsRepeating;
        _rbRepeat.Checked = _source.IsRepeating;

        _timeline.SetPoints(_source.TriggerSeconds.Count > 0 ? _source.TriggerSeconds : DefaultTriggers());
        _weekdays.Mask = _source.WeekDays != 0 ? _source.WeekDays : TaskItem.DefaultWeekDays;
        _monthDays.Mask = _source.MonthDays != 0 ? _source.MonthDays : TaskItem.DefaultMonthDays;

        if (_source.StartAt.HasValue)
        {
            _chkStart.Checked = true;
            _startPicker.Value = UtcTime.ToLocal(_source.StartAt.Value);
        }
        if (_source.DueAt.HasValue)
        {
            _chkDue.Checked = true;
            _duePicker.Value = UtcTime.ToLocal(_source.DueAt.Value);
        }
    }

    int[] DefaultTriggers()
    {
        var first = _settings.DefaultResetMinutes * 60;
        var second = first == TaskItem.DefaultSecondTrigger
            ? TaskItem.DefaultTrigger
            : TaskItem.DefaultSecondTrigger;
        return [first, second];
    }

    void WireEvents()
    {
        _importance.ValueChanged += (_, _) => { UpdateImportanceBadge(); UpdatePreview(); };
        _rbOnce.CheckedChanged += (_, _) => { UpdateMode(); UpdatePreview(); };
        _rbRepeat.CheckedChanged += (_, _) => { UpdateMode(); UpdatePreview(); };
        _repeatKind.SelectedIndexChanged += (_, _) => { UpdateMode(); UpdatePreview(); };
        _timeline.SelectionChanged += UpdateCheckpointEditor;
        _timeline.PointsChanged += () => { UpdateCheckpointEditor(); UpdatePreview(); };
        _checkpointTime.ValueChanged += (_, _) =>
        {
            if (_updatingCheckpoint || _loading) return;
            _timeline.SetSelectedSeconds(
                _checkpointTime.Value.Hour * 3600 +
                _checkpointTime.Value.Minute * 60 +
                _checkpointTime.Value.Second);
            UpdatePreview();
        };
        _weekdays.MaskChanged += () => UpdatePreview();
        _monthDays.MaskChanged += () => UpdatePreview();
        _chkStart.CheckedChanged += (_, _) =>
        {
            _startPicker.Enabled = _chkStart.Checked;
            UpdatePreview();
        };
        _startPicker.ValueChanged += (_, _) => UpdatePreview();
        _chkDue.CheckedChanged += (_, _) =>
        {
            _duePicker.Enabled = _chkDue.Checked;
            UpdatePreview();
        };
        _duePicker.ValueChanged += (_, _) => UpdatePreview();
        _dueMode.SelectedIndexChanged += _ => OnDueModeChanged();
        _durDays.ValueChanged += (_, _) => UpdatePreview();
        _durHours.ValueChanged += (_, _) => UpdatePreview();
        _durMinutes.ValueChanged += (_, _) => UpdatePreview();
        _durSeconds.ValueChanged += (_, _) => UpdatePreview();
        _save.Click += (_, _) => SaveTask();
        _cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        Shown += (_, _) =>
        {
            FitToContent();
            _title.Focus();
        };
    }

    RepeatKind SelectedKind =>
        (_repeatKind.SelectedItem as RepeatOption)?.Kind ?? RepeatKind.Daily;

    bool Repeating => _rbRepeat.Checked;

    void UpdateMode()
    {
        var repeating = Repeating;
        _repeatKind.Visible = repeating;
        _repeatHint.Text = repeating
            ? "在下方时间轴设置触发检查点。"
            : "单次任务无需设置触发时间。";

        _repeatDetail.Visible = repeating;
        if (repeating)
        {
            var kind = SelectedKind;
            _timeline.MaxPoints = kind == RepeatKind.Daily ? int.MaxValue : 1;
            _timelineLabel.Text = "触发器";
            _checkpointHint.Text = kind == RepeatKind.Daily
                ? "可添加多个检查点"
                : "可添加一个检查点";
            _weekdayLabel.Visible = kind == RepeatKind.Weekly;
            _weekdays.Visible = kind == RepeatKind.Weekly;
            _monthDays.Visible = kind == RepeatKind.Monthly;
            _repeatDetail.Height = kind switch
            {
                RepeatKind.Weekly => WeeklyDetailHeight,
                RepeatKind.Monthly => MonthlyDetailHeight,
                _ => DailyDetailHeight,
            };
        }
        else
        {
            _repeatDetail.Height = 0;
        }

        _startPicker.Enabled = _chkStart.Checked;
        _duePicker.Enabled = _chkDue.Checked;
        _dueMode.Visible = !repeating;
        _dueDateRow.Visible = repeating || _dueMode.SelectedIndex == 0;
        _dueDurationRow.Visible = !repeating && _dueMode.SelectedIndex == 1;
        _dueLabel.Text = repeating ? "整体截止时间" : "截止时间";
        _dueDateHint.Text = repeating
            ? "不勾选表示永久有效；多次任务可不设整体截止。"
            : "不勾选表示永久有效。";

        UpdateImportanceBadge();
        UpdateCheckpointEditor();
        LayoutSections();
    }

    void LayoutSections()
    {
        var y = DetailTop + _repeatDetail.Height + 22;

        _startLabel.Location = new Point(MarginX, y);
        _chkStart.Location = new Point(MarginX, y + 22);
        _startPicker.Location = new Point(MarginX + 70, y + 20);
        _startHint.Location = new Point(MarginX + 310, y + 26);

        var dueY = y + 60;
        _dueLabel.Location = new Point(MarginX, dueY);
        _dueMode.Location = new Point(MarginX, dueY + 22);
        var dueRowY = dueY + (Repeating ? 24 : 64);
        _dueDateRow.Location = new Point(MarginX, dueRowY);
        _dueDurationRow.Location = new Point(MarginX, dueRowY);

        var previewY = dueRowY + 36 + 18;
        _previewHost.Location = new Point(MarginX, previewY);

        var buttonsY = previewY + _previewHost.Height + 18;
        _save.Location = new Point(MarginX + ContentWidth - _save.Width, buttonsY);
        _cancel.Location = new Point(_save.Left - _cancel.Width - 12, buttonsY);

        AutoScrollMinSize = new Size(0, buttonsY + _save.Height + 28);
        FitToContent();
    }

    /// <summary>Shrinks or grows the dialog to the current content, capping it to the working area.</summary>
    void FitToContent()
    {
        if (!IsHandleCreated || _fitting) return;
        _fitting = true;
        try
        {
            var area = Screen.FromControl(this).WorkingArea;
            var chrome = Math.Max(0, Height - ClientSize.Height);
            var maxClient = Math.Max(480, area.Height - 20 - chrome);
            var target = Math.Min(Math.Max(480, AutoScrollMinSize.Height), maxClient);
            ClientSize = new Size(Math.Max(760, Ui.S(this, 760)), target);
        }
        finally
        {
            _fitting = false;
        }
    }

    void UpdateCheckpointEditor()
    {
        var selected = _timeline.SelectedSeconds;
        var enabled = Repeating && selected >= 0;
        _checkpointLabel.Enabled = enabled;
        _checkpointTime.Enabled = enabled;
        if (!enabled) return;

        _updatingCheckpoint = true;
        _checkpointTime.Value = DateTime.Today.AddSeconds(selected);
        _updatingCheckpoint = false;
    }

    void UpdateImportanceBadge()
    {
        var (text, color, textColor) = Theme.ImportanceStyle((int)_importance.Value);
        _importanceBadge.Text = text;
        _importanceBadge.BadgeColor = color;
        _importanceBadge.TextColor = textColor;
    }

    TimeSpan Duration =>
        TimeSpan.FromDays((double)_durDays.Value) +
        TimeSpan.FromHours((double)_durHours.Value) +
        TimeSpan.FromMinutes((double)_durMinutes.Value) +
        TimeSpan.FromSeconds((double)_durSeconds.Value);

    DateTime? ComputeDue(DateTime now, out string? error)
    {
        error = null;
        if (!Repeating && _dueMode.SelectedIndex == 1)
        {
            var span = Duration;
            if (span <= TimeSpan.Zero)
            {
                error = "时长必须大于 0。";
                return null;
            }
            var start = _chkStart.Checked ? UtcTime.FromLocal(_startPicker.Value) : now;
            return start + span;
        }
        return _chkDue.Checked ? UtcTime.FromLocal(_duePicker.Value) : null;
    }

    void OnDueModeChanged()
    {
        if (_updatingDueMode || _loading) return;
        _updatingDueMode = true;
        var now = UtcTime.Now;
        if (_dueMode.SelectedIndex == 1)
        {
            if (_chkDue.Checked)
            {
                var start = _chkStart.Checked ? UtcTime.FromLocal(_startPicker.Value) : now;
                var span = UtcTime.FromLocal(_duePicker.Value) - start;
                if (span > TimeSpan.Zero) SetDuration(span);
            }
        }
        else
        {
            var span = Duration;
            if (span > TimeSpan.Zero)
            {
                var start = _chkStart.Checked ? UtcTime.FromLocal(_startPicker.Value) : now;
                _chkDue.Checked = true;
                _duePicker.Value = UtcTime.ToLocal(start + span);
            }
        }
        _updatingDueMode = false;
        UpdateMode();
        UpdatePreview();
    }

    void SetDuration(TimeSpan span)
    {
        _durDays.Value = Math.Clamp((decimal)span.Days, _durDays.Minimum, _durDays.Maximum);
        _durHours.Value = Math.Clamp(span.Hours, 0, 23);
        _durMinutes.Value = Math.Clamp(span.Minutes, 0, 59);
        _durSeconds.Value = Math.Clamp(span.Seconds, 0, 59);
    }

    TaskItem BuildPreviewTask(DateTime now, DateTime? due)
    {
        return new TaskItem
        {
            Id = _source?.Id ?? 0,
            Title = NormalizeTitle(_title.Text),
            Importance = (int)_importance.Value,
            Repeat = Repeating ? SelectedKind : RepeatKind.Once,
            TriggerSeconds = [.. _timeline.Points],
            WeekDays = _weekdays.Mask,
            MonthDays = _monthDays.Mask,
            StartAt = _chkStart.Checked ? UtcTime.FromLocal(_startPicker.Value) : null,
            DueAt = due,
            CreatedAt = _source?.CreatedAt ?? now,
            CompletedCount = _source?.CompletedCount ?? 0,
            LastCompletedAt = _source?.LastCompletedAt,
        };
    }

    void UpdatePreview()
    {
        if (_loading) return;
        var now = UtcTime.Now;
        var due = ComputeDue(now, out var error);
        if (error != null)
        {
            _preview.ForeColor = Theme.Danger;
            _preview.Text = error;
            return;
        }

        var task = BuildPreviewTask(now, due);
        var effectiveStart = task.StartAt ?? now;

        if (task.DueAt.HasValue && task.DueAt.Value <= effectiveStart)
        {
            _preview.ForeColor = Theme.Danger;
            _preview.Text = "截止时间必须晚于开始时间。";
            return;
        }
        if (task.DueAt.HasValue && task.DueAt.Value <= now)
        {
            _preview.ForeColor = Theme.Danger;
            _preview.Text = "截止时间已经过去，请调整截止时间。";
            return;
        }

        _preview.ForeColor = Theme.SubText;
        if (!task.IsRepeating)
        {
            var startText = task.StartAt.HasValue ? Fmt(task.StartAt) : Fmt(now);
            var dueText = task.DueAt.HasValue
                ? $"{Fmt(task.DueAt)}（剩余 {TaskService.FormatDuration(task.DueAt.Value - now)}）"
                : "永久（无截止时间）";
            _preview.Text = $"单次任务\r\n{startText}  →  {dueText}";
            return;
        }

        var window = task.StartAt.HasValue && now < task.StartAt.Value
            ? _service.NextWindow(task, task.StartAt.Value)
            : _service.CurrentWindow(task, now) ?? _service.NextWindow(task, now);
        var windowText = window.HasValue
            ? $"当前区间  {Fmt(window.Value.Start)}  →  {Fmt(window.Value.End)}"
            : "尚未生成触发区间（请检查执行日）";
        var overallDueText = task.DueAt.HasValue
            ? $"整体截止 {Fmt(task.DueAt)}"
            : "无整体截止（触发持续）";
        _preview.Text =
            $"多次任务 · {task.RepeatLabel} · {task.TriggerTimesText}\r\n" +
            $"{DayText(task)}\r\n{windowText}\r\n{overallDueText}";
    }

    static string DayText(TaskItem task)
    {
        if (task.Repeat == RepeatKind.Daily) return "每天触发";
        if (task.Repeat == RepeatKind.Weekly)
        {
            var labels = new[] { "一", "二", "三", "四", "五", "六", "日" };
            var days = Enumerable.Range(0, 7)
                .Where(i => (task.WeekDays & (1 << i)) != 0)
                .Select(i => labels[i])
                .ToList();
            return days.Count == 0 ? "未选择执行日" : $"执行日：{string.Join("、", days)}";
        }

        var monthDays = Enumerable.Range(1, 31)
            .Where(day => (task.MonthDays & (1 << day)) != 0)
            .ToList();
        return monthDays.Count == 0 ? "未选择执行日" : $"执行日：{string.Join("、", monthDays)}";
    }

    void SaveTask()
    {
        var title = NormalizeTitle(_title.Text);
        if (title.Length == 0)
        {
            MessageBox.Show(this, "请填写任务内容。", "无法保存", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _title.Focus();
            return;
        }

        var now = UtcTime.Now;
        var kind = Repeating ? SelectedKind : RepeatKind.Once;
        var start = _chkStart.Checked ? UtcTime.FromLocal(_startPicker.Value) : (DateTime?)null;
        var due = ComputeDue(now, out var dueError);
        if (dueError != null)
        {
            MessageBox.Show(this, dueError, "无法保存", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var effectiveStart = start ?? now;
        if (due.HasValue && due.Value <= effectiveStart)
        {
            MessageBox.Show(this, "截止时间必须晚于开始时间。", "无法保存", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (due.HasValue && due.Value <= now)
        {
            MessageBox.Show(this, "截止时间已经过去，请设置一个未来的时间，或取消勾选“指定”。",
                "无法保存", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var triggers = _timeline.Points.Distinct().OrderBy(v => v).ToList();
        if (kind != RepeatKind.Once)
        {
            if (triggers.Count == 0)
            {
                MessageBox.Show(this, "请至少设置一个触发检查点。", "无法保存",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (kind == RepeatKind.Weekly && _weekdays.Mask == 0)
            {
                MessageBox.Show(this, "每周任务至少需要选择一个执行日。", "无法保存",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (kind == RepeatKind.Monthly && _monthDays.Mask == 0)
            {
                MessageBox.Show(this, "每月任务至少需要选择一个执行日。", "无法保存",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        var reuse = _source?.Archived == true;
        TaskItem result;
        if (_source == null || reuse) result = new TaskItem { CreatedAt = now };
        else result = _source.Clone();

        var scheduleChanged = _source == null || reuse ||
                              _source.Repeat != kind ||
                              !_source.EffectiveTriggers.SequenceEqual(triggers) ||
                              _source.WeekDays != _weekdays.Mask ||
                              _source.MonthDays != _monthDays.Mask ||
                              _source.StartAt != start;

        result.Title = title;
        result.Importance = (int)_importance.Value;
        result.Repeat = kind;
        result.TriggerSeconds = triggers;
        result.WeekDays = _weekdays.Mask;
        result.MonthDays = _monthDays.Mask;
        result.StartAt = start;
        result.DueAt = due;
        result.UpdatedAt = now;
        result.Archived = false;
        result.ArchivedAt = null;
        result.ArchiveReason = null;

        if (_source == null || reuse || scheduleChanged)
        {
            result.CompletedCount = 0;
            result.CompletedAt = null;
            result.LastCompletedAt = null;
        }

        Result = result;
        DialogResult = DialogResult.OK;
        Close();
    }

    static string NormalizeTitle(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    static string Fmt(DateTime? value) =>
        value.HasValue ? UtcTime.Format(value.Value, "yyyy-MM-dd HH:mm:ss") : "—";
}
