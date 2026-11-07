using System.Drawing.Drawing2D;

namespace TaskList.UI;

/// <summary>
/// 24-hour trigger timeline. Click empty space to add a checkpoint, drag a marker to move it,
/// and right-click a marker to remove it. The time editor provides second-level precision.
/// </summary>
public sealed class TimelineControl : Control
{
    readonly List<int> _points = [];
    int _selected = -1;
    int _hover = -1;
    int _dragIndex = -1;
    int _dragStartX;
    int _dragStartSeconds;
    int _maxPoints = int.MaxValue;
    bool _pressEmpty;
    bool _moved;

    public event Action? PointsChanged;
    public event Action? SelectionChanged;

    public IReadOnlyList<int> Points => _points;
    public int SelectedSeconds => _selected >= 0 && _selected < _points.Count ? _points[_selected] : -1;

    public TimelineControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
        Height = 80;
        Cursor = Cursors.Cross;
    }

    public int MaxPoints
    {
        get => _maxPoints;
        set
        {
            _maxPoints = Math.Max(1, value);
            if (_points.Count <= _maxPoints) return;
            var keep = _selected >= 0 && _selected < _points.Count ? _points[_selected] : _points[0];
            _points.Clear();
            _points.Add(keep);
            _selected = 0;
            Invalidate();
            PointsChanged?.Invoke();
            SelectionChanged?.Invoke();
        }
    }

    public void SetPoints(IEnumerable<int> values)
    {
        _points.Clear();
        _points.AddRange(values.Select(v => Math.Clamp(v, 0, 86399)).Distinct().OrderBy(v => v));
        if (_points.Count == 0) _points.Add(TaskList.Models.TaskItem.DefaultTrigger);
        if (_points.Count > _maxPoints) _points.RemoveRange(_maxPoints, _points.Count - _maxPoints);
        _selected = Math.Clamp(_selected, 0, _points.Count - 1);
        Invalidate();
        PointsChanged?.Invoke();
        SelectionChanged?.Invoke();
    }

    public void SetSelectedSeconds(int seconds)
    {
        if (_selected < 0 || _selected >= _points.Count) return;
        var value = FreeSecond(Math.Clamp(seconds, 0, 86399), _selected);
        if (_points[_selected] == value) return;
        _points[_selected] = value;
        NormalizeKeeping(value);
        Invalidate();
        PointsChanged?.Invoke();
    }

    public bool RemoveSelected()
    {
        if (_points.Count <= 1 || _selected < 0) return false;
        _points.RemoveAt(_selected);
        _selected = Math.Min(_selected, _points.Count - 1);
        Invalidate();
        PointsChanged?.Invoke();
        SelectionChanged?.Invoke();
        return true;
    }

    void NormalizeKeeping(int value)
    {
        var sorted = _points.OrderBy(v => v).ToList();
        var index = sorted.IndexOf(value);
        _points.Clear();
        _points.AddRange(sorted);
        _selected = index >= 0 ? index : Math.Clamp(_selected, 0, _points.Count - 1);
    }

    /// <summary>Returns the closest second not occupied by another checkpoint.</summary>
    int FreeSecond(int desired, int movingIndex)
    {
        bool Used(int value)
        {
            for (var i = 0; i < _points.Count; i++)
                if (i != movingIndex && _points[i] == value) return true;
            return false;
        }

        desired = Math.Clamp(desired, 0, 86399);
        if (!Used(desired)) return desired;
        for (var offset = 1; offset < 86400; offset++)
        {
            if (desired + offset <= 86399 && !Used(desired + offset)) return desired + offset;
            if (desired - offset >= 0 && !Used(desired - offset)) return desired - offset;
        }
        return desired;
    }

    int TrackLeft => Ui.S(this, 16);
    int TrackRight => Width - Ui.S(this, 16);
    int TrackY => Ui.S(this, 28);

    int TimeToX(int seconds)
    {
        var span = Math.Max(1, TrackRight - TrackLeft);
        return TrackLeft + (int)Math.Round(seconds / 86400.0 * span);
    }

    int XToTime(int x)
    {
        var span = Math.Max(1, TrackRight - TrackLeft);
        var raw = (x - TrackLeft) / (double)span * 86400.0;
        return Math.Clamp((int)Math.Round(raw), 0, 86399);
    }

    int HitTest(Point point)
    {
        for (var i = 0; i < _points.Count; i++)
            if (Math.Abs(TimeToX(_points[i]) - point.X) <= Ui.S(this, 10) &&
                Math.Abs(TrackY - point.Y) <= Ui.S(this, 14))
                return i;
        return -1;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var trackH = Ui.S(this, 6);
        var trackRect = new RectangleF(TrackLeft, TrackY - trackH / 2f, Math.Max(1, TrackRight - TrackLeft), trackH);

        using (var path = Theme.RoundedRect(trackRect, trackH / 2f))
        using (var brush = new SolidBrush(Theme.SurfaceAlt))
            g.FillPath(brush, path);

        using (var pen = new Pen(Theme.BorderStrong, 1f))
            g.DrawLine(pen, TrackLeft, TrackY, TrackRight, TrackY);

        var labelFont = Theme.Font(7.8f);
        foreach (var hour in new[] { 0, 6, 12, 18, 24 })
        {
            var x = TrackLeft + (int)Math.Round(hour / 24.0 * (TrackRight - TrackLeft));
            using var pen = new Pen(Theme.BorderStrong, 1f);
            g.DrawLine(pen, x, TrackY + Ui.S(this, 8), x, TrackY + Ui.S(this, 12));
            var labelX = hour == 0 ? TrackLeft : hour == 24 ? TrackRight - Ui.S(this, 40) : x - Ui.S(this, 20);
            TextRenderer.DrawText(g, $"{hour:00}:00", labelFont,
                new Rectangle(labelX, TrackY + Ui.S(this, 13), Ui.S(this, 40), Ui.S(this, 16)),
                Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }

        for (var i = 0; i < _points.Count; i++)
        {
            var x = TimeToX(_points[i]);
            var radius = Ui.S(this, i == _selected ? 8 : 6);
            var rect = new RectangleF(x - radius, TrackY - radius, radius * 2f, radius * 2f);
            using var fill = new SolidBrush(i == _selected || i == _hover ? Theme.Accent : Theme.BorderStrong);
            g.FillEllipse(fill, rect);
            if (i == _selected)
            {
                using var pen = new Pen(Color.White, Ui.S(this, 2));
                g.DrawEllipse(pen, rect);
            }
        }

        if (_points.Count == 0)
            TextRenderer.DrawText(g, "点击时间轴添加检查点", Theme.Font(8.6f),
                new Rectangle(TrackLeft, TrackY - Ui.S(this, 30), TrackRight - TrackLeft, Ui.S(this, 20)),
                Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            var hit = HitTest(e.Location);
            _moved = false;
            if (hit >= 0)
            {
                _selected = hit;
                _dragIndex = hit;
                _dragStartX = e.X;
                _dragStartSeconds = _points[hit];
                _pressEmpty = false;
                Cursor = Cursors.SizeWE;
                Invalidate();
                SelectionChanged?.Invoke();
            }
            else
            {
                _pressEmpty = true;
            }
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragIndex >= 0)
        {
            _moved = true;
            var span = Math.Max(1, TrackRight - TrackLeft);
            var delta = (int)Math.Round((e.X - _dragStartX) / (double)span * 86400.0);
            var seconds = FreeSecond(_dragStartSeconds + delta, _dragIndex);
            if (_points[_dragIndex] != seconds)
            {
                _points[_dragIndex] = seconds;
                NormalizeKeeping(seconds);
                _dragIndex = _selected;
                Invalidate();
                PointsChanged?.Invoke();
            }
        }
        else
        {
            var hit = HitTest(e.Location);
            if (hit != _hover)
            {
                _hover = hit;
                Cursor = hit >= 0 ? Cursors.SizeWE : Cursors.Cross;
                Invalidate();
            }
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            var hit = HitTest(e.Location);
            if (hit >= 0 && _points.Count > 1)
            {
                _points.RemoveAt(hit);
                _selected = Math.Min(hit, _points.Count - 1);
                _hover = -1;
                Invalidate();
                PointsChanged?.Invoke();
                SelectionChanged?.Invoke();
            }
            base.OnMouseUp(e);
            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            if (_dragIndex >= 0)
            {
                _dragIndex = -1;
                Cursor = Cursors.Cross;
                if (_moved) PointsChanged?.Invoke();
            }
            else if (_pressEmpty)
            {
                AddPoint(XToTime(e.X));
            }
        }
        _pressEmpty = false;
        _dragIndex = -1;
        base.OnMouseUp(e);
    }

    void AddPoint(int seconds)
    {
        if (_points.Count >= _maxPoints) return;
        var value = FreeSecond(seconds, -1);
        _points.Add(value);
        NormalizeKeeping(value);
        Invalidate();
        PointsChanged?.Invoke();
        SelectionChanged?.Invoke();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (_dragIndex < 0)
        {
            _hover = -1;
            Cursor = Cursors.Cross;
            Invalidate();
        }
        base.OnMouseLeave(e);
    }
}

/// <summary>Seven-toggle weekday picker. Bit 0 is Monday; selected cells use the accent color.</summary>
public sealed class WeekdayPicker : Control
{
    static readonly string[] Labels = ["一", "二", "三", "四", "五", "六", "日"];
    int _mask = TaskList.Models.TaskItem.DefaultWeekDays;
    int _hover = -1;

    public event Action? MaskChanged;

    public WeekdayPicker()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
        Height = 40;
        Cursor = Cursors.Hand;
        Font = Theme.Font(9.2f, FontStyle.Bold);
    }

    public int Mask
    {
        get => _mask;
        set { _mask = value & 0b1111111; Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        for (var i = 0; i < 7; i++)
        {
            var rect = CellRect(i);
            var selected = (_mask & (1 << i)) != 0;
            var bg = selected ? Theme.Accent : i == _hover ? Theme.AccentSoft : Color.FromArgb(238, 240, 245);
            Theme.FillRounded(g, rect, Ui.S(this, 7), bg);
            TextRenderer.DrawText(g, Labels[i], Font, Rectangle.Round(rect),
                selected ? Color.White : Theme.SubText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }
    }

    RectangleF CellRect(int index)
    {
        var gap = Ui.S(this, 6);
        var width = Ui.S(this, 56);
        var height = Math.Max(1, Height - 1);
        return new RectangleF(index * (width + gap), 0.5f, width, height);
    }

    int HitTest(Point point)
    {
        for (var i = 0; i < 7; i++)
            if (CellRect(i).Contains(point)) return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var hit = HitTest(e.Location);
        if (hit != _hover)
        {
            _hover = hit;
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = -1;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            var hit = HitTest(e.Location);
            if (hit >= 0)
            {
                _mask ^= 1 << hit;
                Invalidate();
                MaskChanged?.Invoke();
            }
        }
        base.OnMouseUp(e);
    }
}

/// <summary>
/// Monthly day picker. Days 1-28 are always available; 29-31 are grouped separately because
/// those dates do not exist in every month.
/// </summary>
public sealed class MonthDayPicker : Control
{
    int _mask = TaskList.Models.TaskItem.DefaultMonthDays;
    int _hover;
    bool _hovering;

    public event Action? MaskChanged;

    public MonthDayPicker()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
        Height = Ui.S(this, 228);
        Cursor = Cursors.Hand;
        Font = Theme.Font(9.2f, FontStyle.Bold);
    }

    public int Mask
    {
        get => _mask;
        set { _mask = value; Invalidate(); }
    }

    int CellWidth => Ui.S(this, 56);
    int CellHeight => Ui.S(this, 30);
    int Gap => Ui.S(this, 5);

    RectangleF DayRect(int day)
    {
        if (day <= 28)
        {
            var index = day - 1;
            var row = index / 7;
            var col = index % 7;
            return new RectangleF(col * (CellWidth + Gap), Ui.S(this, 20) + row * (CellHeight + Gap),
                CellWidth, CellHeight);
        }

        var groupIndex = day - 29;
        return new RectangleF(groupIndex * (CellWidth + Gap), Ui.S(this, 196),
            CellWidth, CellHeight);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var small = Theme.Font(8.3f);

        TextRenderer.DrawText(g, "1-28（每月都有）", small,
            new Rectangle(0, 0, Width, Ui.S(this, 18)), Theme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        for (var day = 1; day <= 31; day++)
        {
            var rect = DayRect(day);
            var selected = (_mask & (1 << day)) != 0;
            var hovered = _hovering && _hover == day;
            var bg = selected ? Theme.Accent : hovered ? Theme.AccentSoft : Color.FromArgb(238, 240, 245);
            Theme.FillRounded(g, rect, Ui.S(this, 6), bg);
            TextRenderer.DrawText(g, day.ToString(), Font, Rectangle.Round(rect),
                selected ? Color.White : Theme.SubText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }

        TextRenderer.DrawText(g, "29-31（短月不触发）", small,
            new Rectangle(0, Ui.S(this, 166), Width, Ui.S(this, 20)),
            Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
    }

    int HitTest(Point point)
    {
        for (var day = 1; day <= 31; day++)
            if (DayRect(day).Contains(point)) return day;
        return 0;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var hit = HitTest(e.Location);
        if (hit != _hover || !_hovering)
        {
            _hover = hit;
            _hovering = hit > 0;
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovering = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            var day = HitTest(e.Location);
            if (day > 0)
            {
                _mask ^= 1 << day;
                Invalidate();
                MaskChanged?.Invoke();
            }
        }
        base.OnMouseUp(e);
    }
}
