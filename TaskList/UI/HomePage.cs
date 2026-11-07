using System.Globalization;
using TaskList.Data;
using TaskList.Models;
using TaskList.Services;

namespace TaskList.UI;

public sealed class HomePage : UserControl
{
    readonly TaskService _service;
    readonly Label _subtitle;
    readonly SmoothFlowPanel _flow;
    readonly FlatButton _addButton;

    public event Action? AddRequested;
    public event Action<TaskItem>? EditRequested;
    public event Action<TaskItem>? DeleteRequested;
    public event Action<TaskItem>? CompleteRequested;

    public HomePage(TaskService service)
    {
        _service = service;
        Dock = DockStyle.Fill;
        BackColor = Theme.Bg;
        AutoScaleMode = AutoScaleMode.Inherit;

        var header = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = Theme.Bg };
        var title = Ui.Label("现在该做什么？", 18f, Theme.Text, FontStyle.Bold);
        title.Location = new Point(32, 16);
        _subtitle = Ui.Label("", 9.5f, Theme.SubText);
        _subtitle.Location = new Point(34, 51);

        _addButton = new FlatButton
        {
            Text = "+  新建任务",
            Size = new Size(124, 40),
        };
        _addButton.Click += (_, _) => AddRequested?.Invoke();
        header.Resize += (_, _) =>
            _addButton.Location = new Point(header.ClientSize.Width - 32 - _addButton.Width, 20);

        header.Controls.AddRange([title, _subtitle, _addButton]);
        _addButton.Location = new Point(header.ClientSize.Width - 32 - _addButton.Width, 20);

        var listHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(32, 0, 32, 22),
            BackColor = Theme.Bg,
        };
        _flow = new SmoothFlowPanel { Dock = DockStyle.Fill, Padding = new Padding(0) };
        _flow.HorizontalScroll.Enabled = false;
        _flow.ClientSizeChanged += (_, _) => ResizeItems();
        listHost.Controls.Add(_flow);

        Controls.Add(listHost);
        Controls.Add(header);
    }

    public void RefreshData()
    {
        var now = UtcTime.Now;
        _service.Reload();
        _service.ArchiveExpired(now);
        var tasks = _service.HomeTasks(now);
        var date = UtcTime.ToLocal(now).ToString("yyyy年M月d日 dddd",
            CultureInfo.GetCultureInfo("zh-CN"));
        _subtitle.Text = tasks.Count > 0
            ? $"{date} · {tasks.Count} 项待处理"
            : $"{date} · 暂无待处理任务";
        Rebuild(tasks);
    }

    void Rebuild(List<TaskItem> tasks)
    {
        var scroll = _flow.AutoScrollPosition;
        _flow.SuspendLayout();
        var old = _flow.Controls.Cast<Control>().ToList();
        _flow.Controls.Clear();
        foreach (var control in old) control.Dispose();

        if (tasks.Count == 0)
        {
            var empty = new Label
            {
                AutoSize = false,
                Height = Ui.S(_flow, 230),
                Margin = new Padding(0, 24, 0, 0),
                Text = "当前没有需要处理的任务\r\n\r\n点击右上角「+ 新建任务」创建第一条任务",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = Theme.Font(10.5f),
                ForeColor = Theme.SubText,
                BackColor = Theme.Bg,
            };
            _flow.Controls.Add(empty);
        }
        else
        {
            foreach (var task in tasks)
            {
                var card = new TaskCard(_service, task)
                {
                    Height = Ui.S(_flow, 106),
                };
                card.CompleteRequested += t => CompleteRequested?.Invoke(t);
                card.EditRequested += t => EditRequested?.Invoke(t);
                card.DeleteRequested += t => DeleteRequested?.Invoke(t);
                _flow.Controls.Add(card);
            }
        }

        _flow.ResumeLayout(true);
        ResizeItems();
        _flow.AutoScrollPosition = new Point(Math.Abs(scroll.X), Math.Abs(scroll.Y));
        if (IsHandleCreated && !IsDisposed)
            BeginInvoke((Action)ResizeItems);
    }

    void ResizeItems()
    {
        if (IsDisposed) return;
        var width = Math.Max(200, _flow.ClientSize.Width - _flow.Padding.Horizontal - 2);
        foreach (Control control in _flow.Controls)
            if (control.Width != width) control.Width = width;
        _flow.PerformLayout();
    }

    public void Tick()
    {
        foreach (Control control in _flow.Controls)
            if (control is TaskCard card)
                card.Tick();
    }
}
