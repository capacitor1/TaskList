using System.Drawing.Drawing2D;
using TaskList.Data;
using TaskList.Models;
using TaskList.Services;

namespace TaskList.UI;

/// <summary>Owner-drawn task row: ID, title, importance badge, schedule and complete button.</summary>
public sealed class TaskCard : Control
{
    readonly TaskService _service;
    readonly ToolTip _tooltip = new();
    readonly ContextMenuStrip _menu;

    Rectangle _buttonRect;
    bool _hover;
    bool _hoverButton;
    bool _pressedButton;

    public TaskItem Task { get; }

    public event Action<TaskItem>? CompleteRequested;
    public event Action<TaskItem>? EditRequested;
    public event Action<TaskItem>? DeleteRequested;

    public TaskCard(TaskService service, TaskItem task)
    {
        _service = service;
        Task = task;

        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Margin = new Padding(0, 0, 0, Ui.S(this, 12));
        Cursor = Cursors.Default;

        _tooltip.SetToolTip(this, task.Title);
        _menu = Ui.CreateMenu();
        var edit = new ToolStripMenuItem("修改任务");
        edit.Click += (_, _) => EditRequested?.Invoke(Task);
        var done = new ToolStripMenuItem("标记完成");
        done.Click += (_, _) => CompleteRequested?.Invoke(Task);
        var delete = new ToolStripMenuItem("删除任务");
        delete.Click += (_, _) => DeleteRequested?.Invoke(Task);
        _menu.Items.AddRange([edit, done, new ToolStripSeparator(), delete]);
        ContextMenuStrip = _menu;
    }

    public void Tick() => Invalidate();

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var now = UtcTime.Now;
        var critical = _service.IsCritical(Task, now);

        var cardRect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        var radius = Ui.S(this, 10);
        using (var path = Theme.RoundedRect(cardRect, radius))
        {
            using var fill = new SolidBrush(_hover ? Theme.SurfaceAlt : Theme.Surface);
            g.FillPath(fill, path);

            // Importance strip, clipped by the rounded card.
            using var oldClip = g.Clip;
            g.SetClip(path);
            using (var strip = new SolidBrush(Theme.ImportanceColor(Task.Importance)))
                g.FillRectangle(strip, 0, 0, Ui.S(this, 5), Height);
            g.Clip = oldClip;

            using var border = new Pen(
                critical ? Color.FromArgb(245, 195, 192) : _hover ? Theme.BorderStrong : Theme.Border, 1f);
            g.DrawPath(border, path);
        }

        var pad = Ui.S(this, 18);
        var titleX = pad + Ui.S(this, 44);
        var buttonW = Ui.S(this, 88);
        var buttonH = Ui.S(this, 34);
        _buttonRect = new Rectangle(Width - pad - buttonW, (Height - buttonH) / 2, buttonW, buttonH);
        var contentRight = _buttonRect.X - Ui.S(this, 14);

        var idFont = Theme.Font(9.5f, FontStyle.Bold);
        TextRenderer.DrawText(g, $"({Task.Id})", idFont,
            new Rectangle(pad, Ui.S(this, 10), Ui.S(this, 40), Ui.S(this, 30)), Theme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

        var titleFont = Theme.Font(12.5f, FontStyle.Bold);
        var (chipText, chipColor, chipTextColor) = Theme.ImportanceStyle(Task.Importance);
        var chipFont = Theme.Font(8.4f, FontStyle.Bold);
        var chipW = Math.Max(Ui.S(this, 56),
            TextRenderer.MeasureText(chipText, chipFont).Width + Ui.S(this, 18));
        var chipH = Ui.S(this, 22);
        var chipRect = new Rectangle(titleX, Ui.S(this, 14), chipW, chipH);
        var titleLeft = chipRect.Right + Ui.S(this, 9);

        var titleRect = new Rectangle(titleLeft, Ui.S(this, 10),
            Math.Max(0, contentRight - titleLeft), Ui.S(this, 30));
        TextRenderer.DrawText(g, Task.Title, titleFont, titleRect,
            critical ? Theme.Danger : Theme.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        using (var chipPath = Theme.RoundedRect(chipRect, chipH / 2f))
        {
            using var brush = new SolidBrush(chipColor);
            g.FillPath(brush, chipPath);
        }
        TextRenderer.DrawText(g, chipText, chipFont, chipRect, chipTextColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

        DrawScheduleLine(g, now, critical, titleX, contentRight);
        DrawMiscLine(g, now, critical, titleX, contentRight);
        DrawCompleteButton(g);
    }

    void DrawScheduleLine(Graphics g, DateTime now, bool critical, int x, int right)
    {
        var y = Ui.S(this, 43);
        var h = Ui.S(this, 20);
        var font = Theme.Font(9.1f);
        var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;

        string startText, dueText, typeText;
        if (Task.IsRepeating)
        {
            var window = _service.CurrentWindow(Task, now);
            startText = window.HasValue ? Fmt(window.Value.Start) : "尚未开始";
            dueText = window.HasValue ? Fmt(window.Value.End) : "—";
            typeText = $"多次 · {Task.RepeatLabel}";
        }
        else
        {
            startText = Fmt(Task.StartAt ?? Task.CreatedAt);
            dueText = Task.DueAt.HasValue ? Fmt(Task.DueAt.Value) : "永久";
            typeText = "单次";
        }

        var prefix = $"{startText}  →  ";
        var suffix = $"   ·   {typeText}";
        var countdown = "";
        if (critical && _service.EffectiveDeadline(Task, now) is { } deadline)
            countdown = $"  （剩余 {TaskService.FormatDuration(deadline - now)}）";

        var available = Math.Max(0, right - x);
        var prefixW = Measure(prefix);
        var dueW = Measure(dueText);
        var suffixW = Measure(suffix);
        var countdownW = Measure(countdown);
        if (critical && prefixW + dueW + suffixW + countdownW > available)
            suffix = "";

        var lineRect = new Rectangle(x, y, available, h);
        using var oldClip = g.Clip;
        g.SetClip(lineRect);
        var cx = x;
        cx += DrawSegment(g, prefix, font, cx, y, h, Theme.SubText, flags);
        cx += DrawSegment(g, dueText, font, cx, y, h, critical ? Theme.Danger : Theme.SubText, flags);
        if (suffix.Length > 0)
            cx += DrawSegment(g, suffix, font, cx, y, h, Theme.SubText, flags);
        if (countdown.Length > 0)
            DrawSegment(g, countdown, font, cx, y, h, Theme.Danger, flags);
        g.Clip = oldClip;

        int Measure(string text) => text.Length == 0
            ? 0
            : TextRenderer.MeasureText(g, text, font, Size.Empty, flags).Width;
    }

    static int DrawSegment(Graphics g, string text, Font font, int x, int y, int h, Color color,
        TextFormatFlags flags)
    {
        if (text.Length == 0) return 0;
        var width = TextRenderer.MeasureText(g, text, font, Size.Empty, flags).Width;
        TextRenderer.DrawText(g, text, font, new Rectangle(x, y, width + 2, h), color, flags);
        return width;
    }

    void DrawMiscLine(Graphics g, DateTime now, bool critical, int x, int right)
    {
        var font = Theme.Font(8.6f);
        var startAt = Task.IsRepeating
            ? _service.CurrentWindow(Task, now)?.Start ?? Task.StartAt ?? Task.CreatedAt
            : Task.StartAt ?? Task.CreatedAt;
        var started = UtcTime.Format(startAt, "yyyy-MM-dd");
        var deadline = _service.EffectiveDeadline(Task, now);
        string text;
        if (!deadline.HasValue)
            text = "无截止时间（永久）";
        else if (critical)
            text = $"开始于 {started}";
        else
            text = $"剩余 {TaskService.FormatDuration(deadline.Value - now)} · 开始于 {started}";

        TextRenderer.DrawText(g, text, font,
            new Rectangle(x, Ui.S(this, 70), Math.Max(0, right - x), Ui.S(this, 20)), Theme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    void DrawCompleteButton(Graphics g)
    {
        var radius = Ui.S(this, 8);
        using var path = Theme.RoundedRect(_buttonRect, radius);
        var active = _hoverButton || _pressedButton;
        var bg = active ? (_pressedButton ? Theme.AccentHover : Theme.Accent) : Theme.AccentSoft;
        var fg = active ? Color.White : Theme.Accent;
        using (var brush = new SolidBrush(bg))
            g.FillPath(brush, path);
        if (!active)
        {
            using var pen = new Pen(Theme.Blend(Theme.Accent, Color.White, 0.7f), 1f);
            g.DrawPath(pen, path);
        }
        TextRenderer.DrawText(g, "✓  完成", Theme.Font(9.2f, FontStyle.Bold), _buttonRect, fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
    }

    static string Fmt(DateTime value) => UtcTime.Format(value, "yyyy-MM-dd HH:mm:ss");

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var overButton = _buttonRect.Contains(e.Location);
        if (overButton != _hoverButton || !_hover)
        {
            _hoverButton = overButton;
            Cursor = overButton ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
        _hover = true;
        base.OnMouseMove(e);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        _hoverButton = false;
        _pressedButton = false;
        Cursor = Cursors.Default;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && _buttonRect.Contains(e.Location))
        {
            _pressedButton = true;
            Invalidate();
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_pressedButton && e.Button == MouseButtons.Left)
        {
            _pressedButton = false;
            Invalidate();
            if (_buttonRect.Contains(e.Location))
                CompleteRequested?.Invoke(Task);
        }
        base.OnMouseUp(e);
    }

    protected override void OnDoubleClick(EventArgs e)
    {
        if (!_buttonRect.Contains(PointToClient(Cursor.Position)))
            EditRequested?.Invoke(Task);
        base.OnDoubleClick(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tooltip.Dispose();
            _menu.Dispose();
        }
        base.Dispose(disposing);
    }
}
