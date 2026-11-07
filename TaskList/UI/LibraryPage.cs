using TaskList.Data;
using TaskList.Models;
using TaskList.Services;

namespace TaskList.UI;

public sealed class LibraryPage : UserControl
{
    readonly TaskService _service;
    readonly Label _subtitle;
    readonly SegmentedControl _tabs;
    readonly SmoothDataGridView _grid;
    readonly ContextMenuStrip _menu;
    readonly ToolStripMenuItem _editItem;
    readonly ToolStripMenuItem _deleteItem;

    bool _showArchived;
    TaskItem? _contextTask;

    DataGridViewColumn _colImp = null!;
    DataGridViewColumn _colTitle = null!;
    DataGridViewColumn _colStatus = null!;
    DataGridViewColumn _colTime = null!;

    public event Action<TaskItem>? EditRequested;
    public event Action<TaskItem>? DeleteRequested;

    public LibraryPage(TaskService service)
    {
        _service = service;
        Dock = DockStyle.Fill;
        BackColor = Theme.Bg;
        AutoScaleMode = AutoScaleMode.Inherit;

        var header = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = Theme.Bg };
        var title = Ui.Label("任务库", 18f, Theme.Text, FontStyle.Bold);
        title.Location = new Point(32, 16);
        _subtitle = new Label
        {
            AutoSize = false,
            Location = new Point(title.Right + 14, 24),
            Height = 22,
            Font = Theme.Font(9.2f),
            ForeColor = Theme.SubText,
            BackColor = Theme.Bg,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };
        _tabs = new SegmentedControl("未结束", "已结束")
        {
            Size = new Size(220, 36),
            Location = new Point(0, 18),
        };
        _tabs.SelectedIndexChanged += index => SetTab(index == 1);
        header.Resize += (_, _) =>
        {
            _tabs.Location = new Point(header.ClientSize.Width - 32 - _tabs.Width, 18);
            _subtitle.Width = Math.Max(0, _tabs.Left - 20 - _subtitle.Left);
        };
        header.Controls.AddRange([title, _subtitle, _tabs]);

        _grid = new SmoothDataGridView { Dock = DockStyle.Fill };
        ConfigureGrid();
        BuildColumns();

        _menu = Ui.CreateMenu();
        _editItem = new ToolStripMenuItem("修改任务");
        _editItem.Click += (_, _) => { if (_contextTask != null) EditRequested?.Invoke(_contextTask); };
        _deleteItem = new ToolStripMenuItem("删除任务");
        _deleteItem.Click += (_, _) => { if (_contextTask != null) DeleteRequested?.Invoke(_contextTask); };
        _menu.Items.AddRange([_editItem, _deleteItem]);
        _menu.Opening += (_, e) =>
        {
            if (_contextTask == null)
            {
                e.Cancel = true;
                return;
            }
            _editItem.Text = _contextTask.Archived ? "复用并修改（创建副本）" : "修改任务";
            _deleteItem.Text = _contextTask.Archived ? "永久删除" : "删除任务";
        };
        _grid.ContextMenuStrip = _menu;
        _grid.CellMouseDown += GridOnCellMouseDown;
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && _grid.Rows[e.RowIndex].Tag is TaskItem task)
                EditRequested?.Invoke(task);
        };
        _grid.CellFormatting += GridOnCellFormatting;
        _grid.CellToolTipTextNeeded += GridOnCellToolTipTextNeeded;

        var gridHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(32, 0, 32, 22),
            BackColor = Theme.Bg,
        };
        gridHost.Controls.Add(_grid);

        Controls.Add(gridHost);
        Controls.Add(header);
    }

    void ConfigureGrid()
    {
        _grid.BackgroundColor = Theme.Surface;
        _grid.BorderStyle = BorderStyle.None;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.GridColor = Theme.Border;
        _grid.RowHeadersVisible = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.AllowUserToOrderColumns = false;
        _grid.AllowUserToResizeColumns = false;
        _grid.ReadOnly = true;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoGenerateColumns = false;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.ShowCellToolTips = true;
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.SurfaceAlt,
            ForeColor = Theme.SubText,
            Font = Theme.Font(9f, FontStyle.Bold),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            SelectionBackColor = Theme.SurfaceAlt,
            SelectionForeColor = Theme.SubText,
        };
        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            Font = Theme.Font(9.5f),
            SelectionBackColor = Theme.AccentSoft,
            SelectionForeColor = Theme.Text,
            Padding = new Padding(8, 0, 0, 0),
            WrapMode = DataGridViewTriState.False,
        };
        _grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(252, 253, 255),
            SelectionBackColor = Theme.AccentSoft,
            SelectionForeColor = Theme.Text,
        };
    }

    void BuildColumns()
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colId",
            HeaderText = "ID",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 5,
            MinimumWidth = 40,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });
        _colTitle = new DataGridViewTextBoxColumn
        {
            Name = "colTitle",
            HeaderText = "任务内容",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 28,
            MinimumWidth = 160,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        };
        _grid.Columns.Add(_colTitle);
        _colImp = new DataGridViewTextBoxColumn
        {
            Name = "colImp",
            HeaderText = "重要",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
            MinimumWidth = 80,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        };
        _grid.Columns.Add(_colImp);
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colKind",
            HeaderText = "类型",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
            MinimumWidth = 60,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });
        _colTime = new DataGridViewTextBoxColumn
        {
            Name = "colTime",
            HeaderText = "时间",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 45,
            MinimumWidth = 220,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        };
        _grid.Columns.Add(_colTime);
        _colStatus = new DataGridViewTextBoxColumn
        {
            Name = "colStatus",
            HeaderText = "状态",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
            MinimumWidth = 90,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        };
        _grid.Columns.Add(_colStatus);
    }

    void SetTab(bool archived)
    {
        if (_showArchived == archived) return;
        _showArchived = archived;
        RefreshData();
    }

    public void RefreshData()
    {
        var now = UtcTime.Now;
        _service.Reload();
        _service.ArchiveExpired(now);

        var open = _service.All.Where(t => !t.Archived).OrderByDescending(t => t.Id).ToList();
        var done = _service.All.Where(t => t.Archived)
            .OrderByDescending(t => t.ArchivedAt ?? t.UpdatedAt).ToList();
        _tabs.SetLabel(0, $"未结束  {open.Count}");
        _tabs.SetLabel(1, $"已结束  {done.Count}");
        _subtitle.Text = _showArchived
            ? "已完成和已过期的任务会保留在这里，右键可复用或永久删除。"
            : "所有未结束的任务，包括正在进行和等待下次触发的任务。";

        var selectedId = (_grid.CurrentRow?.Tag as TaskItem)?.Id;
        _contextTask = null;
        _grid.SuspendLayout();
        _grid.Rows.Clear();
        foreach (var task in _showArchived ? done : open)
        {
            var rowIndex = _grid.Rows.Add(
                task.Id,
                task.Title,
                Theme.ImportanceStyle(task.Importance).Label,
                task.RepeatLabel,
                TimeText(task, now),
                StatusText(task, now));
            _grid.Rows[rowIndex].Tag = task;
        }
        _grid.ResumeLayout();

        var restored = false;
        if (selectedId.HasValue)
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Tag is TaskItem t && t.Id == selectedId.Value)
                {
                    row.Selected = true;
                    _grid.CurrentCell = row.Cells[0];
                    restored = true;
                    break;
                }
            }
        }
        if (!restored)
        {
            _grid.ClearSelection();
            _grid.CurrentCell = null;
        }
    }

    string TimeText(TaskItem t, DateTime now)
    {
        if (t.Archived)
        {
            var start = Fmt(t.StartAt ?? t.CreatedAt, "yyyy-MM-dd HH:mm");
            var end = Fmt(t.DueAt ?? t.CompletedAt ?? t.ArchivedAt ?? t.UpdatedAt, "yyyy-MM-dd HH:mm");
            var range = $"{start}  →  {end}";
            return t.IsRepeating ? $"{range}  ·  {t.RepeatLabel}" : range;
        }

        if (t.IsRepeating)
        {
            if (t.StartAt.HasValue && now < t.StartAt.Value)
                return $"{Fmt(t.StartAt.Value, "yyyy-MM-dd HH:mm")} 开始  ·  {t.RepeatLabel}";
            var window = _service.CurrentWindow(t, now);
            return window.HasValue
                ? $"{Fmt(window.Value.Start, "yyyy-MM-dd HH:mm")}  →  {Fmt(window.Value.End, "yyyy-MM-dd HH:mm")}  ·  {t.RepeatLabel}"
                : $"{Fmt(t.StartAt ?? t.CreatedAt, "yyyy-MM-dd HH:mm")}  ·  {t.RepeatLabel}";
        }

        var startAt = Fmt(t.StartAt ?? t.CreatedAt, "yyyy-MM-dd HH:mm");
        var due = t.DueAt.HasValue ? Fmt(t.DueAt.Value, "yyyy-MM-dd HH:mm") : "永久";
        return $"{startAt}  →  {due}";
    }

    string StatusText(TaskItem t, DateTime now)
    {
        if (t.Archived)
        {
            if (t.ArchiveReason == "done")
                return t.CompletedAt.HasValue
                    ? $"已完成  {Fmt(t.CompletedAt.Value, "MM-dd HH:mm")}"
                    : "已完成";
            return t.DueAt.HasValue
                ? $"已过期  {Fmt(t.DueAt.Value, "MM-dd HH:mm")}"
                : "已过期";
        }
        return _service.StatusText(t, now);
    }

    static string Fmt(DateTime utc, string format) => UtcTime.Format(utc, format);

    /// <summary>Re-applies accent-dependent grid colors after the theme changes.</summary>
    public void ApplyTheme()
    {
        _grid.DefaultCellStyle.SelectionBackColor = Theme.AccentSoft;
        _grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = Theme.AccentSoft;
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.SurfaceAlt;
        _grid.Invalidate();
        Invalidate(true);
    }

    void GridOnCellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;
        if (e.RowIndex >= 0 && e.RowIndex < _grid.Rows.Count)
        {
            _grid.CurrentCell = _grid.Rows[e.RowIndex].Cells[Math.Max(0, e.ColumnIndex)];
            _grid.Rows[e.RowIndex].Selected = true;
            _contextTask = _grid.Rows[e.RowIndex].Tag as TaskItem;
        }
        else
        {
            _contextTask = null;
        }
    }

    void GridOnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
        if (_grid.Rows[e.RowIndex].Tag is not TaskItem task) return;
        if (e.CellStyle is not { } style) return;

        if (e.ColumnIndex == _colImp.Index)
        {
            style.ForeColor = Theme.ImportanceColor(task.Importance);
            style.Font = Theme.Font(9.2f, FontStyle.Bold);
        }
        else if (e.ColumnIndex == _colStatus.Index)
        {
            style.ForeColor = task.Archived
                ? task.ArchiveReason == "done" ? Theme.Success : Theme.Danger
                : _service.StatusText(task, UtcTime.Now) switch
                {
                    "进行中" => Theme.Accent,
                    "本次已完成" => Theme.Warning,
                    _ => Theme.SubText,
                };
        }
        else if (e.ColumnIndex == _colTitle.Index && !task.Archived &&
                 _service.IsCritical(task, UtcTime.Now))
        {
            style.ForeColor = Theme.Danger;
        }
    }

    void GridOnCellToolTipTextNeeded(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
        if (_grid.Rows[e.RowIndex].Tag is not TaskItem task) return;
        if (e.ColumnIndex == _colTitle.Index || e.ColumnIndex == _colTime.Index)
            e.ToolTipText = $"{task.Title}\n{_grid.Rows[e.RowIndex].Cells[_colTime.Index].Value}";
    }
}
