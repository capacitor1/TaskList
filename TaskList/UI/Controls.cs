using System.Drawing.Drawing2D;

namespace TaskList.UI;

public enum FlatButtonStyle
{
    Primary,
    Secondary,
    Ghost,
    Danger,
}

/// <summary>Flat, rounded, owner-drawn button used throughout the app.</summary>
public sealed class FlatButton : Control, IButtonControl
{
    FlatButtonStyle _style = FlatButtonStyle.Primary;
    bool _hover;
    bool _pressed;
    bool _isDefault;

    public FlatButtonStyle Style
    {
        get => _style;
        set { _style = value; Invalidate(); }
    }

    public int Radius { get; set; } = 8;
    public DialogResult DialogResult { get; set; } = DialogResult.None;

    public FlatButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Theme.Font(10f, FontStyle.Bold);
        Cursor = Cursors.Hand;
        TabStop = true;
        Height = 38;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var (bg, fg, border) = Colors();
        if (!Enabled)
        {
            bg = Color.FromArgb(236, 238, 244);
            fg = Theme.Muted;
            border = Color.Transparent;
        }
        else if (_pressed)
        {
            bg = Darken(bg, 0.10f);
        }
        else if (_hover)
        {
            bg = Darken(bg, 0.05f);
        }

        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        var radius = Ui.S(this, Radius);
        using (var path = Theme.RoundedRect(rect, radius))
        {
            using var brush = new SolidBrush(bg);
            g.FillPath(brush, path);
            if (border != Color.Transparent)
            {
                using var pen = new Pen(border, 1f);
                g.DrawPath(pen, path);
            }
            if (Focused || _isDefault)
            {
                using var focus = new Pen(Color.FromArgb(120, fg), 1f) { DashStyle = DashStyle.Dot };
                g.DrawPath(focus, path);
            }
        }

        TextRenderer.DrawText(g, Text, Font, ClientRectangle, fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPadding);
    }

    (Color Bg, Color Fg, Color Border) Colors() => _style switch
    {
        FlatButtonStyle.Primary => (Theme.Accent, Color.White, Color.Transparent),
        FlatButtonStyle.Secondary => (Theme.Surface, Theme.Text, Theme.BorderStrong),
        FlatButtonStyle.Ghost => (Color.Transparent, Theme.SubText, Color.Transparent),
        FlatButtonStyle.Danger => (Theme.Danger, Color.White, Color.Transparent),
        _ => (Theme.Accent, Color.White, Color.Transparent),
    };

    static Color Darken(Color color, float amount) => Color.FromArgb(
        color.A,
        (int)(color.R * (1 - amount)),
        (int)(color.G * (1 - amount)),
        (int)(color.B * (1 - amount)));

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); }
        base.OnMouseDown(e);
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }
        base.OnKeyDown(e);
    }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    public void NotifyDefault(bool value)
    {
        _isDefault = value;
        Invalidate();
    }

    public void PerformClick()
    {
        if (Enabled) OnClick(EventArgs.Empty);
    }
}

/// <summary>Sidebar navigation item with a glyph and a selected pill.</summary>
public sealed class NavButton : Control
{
    bool _hover;
    bool _selected;
    bool _compact;

    public string Icon { get; set; } = "";
    public Font IconFont { get; set; } = Theme.IconFont;
    public bool Selected
    {
        get => _selected;
        set { _selected = value; Invalidate(); }
    }

    /// <summary>Shows only the glyph, centered, for the collapsed sidebar.</summary>
    public bool Compact
    {
        get => _compact;
        set { _compact = value; Invalidate(); }
    }

    public NavButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Theme.Font(10.2f, FontStyle.Bold);
        Cursor = Cursors.Hand;
        Height = 44;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var bg = _selected ? Theme.AccentSoft : _hover ? Theme.SurfaceAlt : Color.Transparent;
        if (bg != Color.Transparent)
            Theme.FillRounded(g, new RectangleF(0, 0, Width, Height), Ui.S(this, 9), bg);

        var fg = _selected ? Theme.Accent : Theme.SubText;
        if (_compact)
        {
            TextRenderer.DrawText(g, Icon, IconFont, ClientRectangle, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);
            return;
        }

        var iconRect = new Rectangle(Ui.S(this, 14), 0, Ui.S(this, 24), Height);
        TextRenderer.DrawText(g, Icon, IconFont, iconRect, fg,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

        var textRect = new Rectangle(Ui.S(this, 44), 0, Width - Ui.S(this, 50), Height);
        TextRenderer.DrawText(g, Text, Font, textRect, fg,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }
        base.OnKeyDown(e);
    }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
}

/// <summary>White rounded panel used for settings groups.</summary>
public sealed class CardPanel : Panel
{
    public int Radius { get; set; } = 14;

    public CardPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        using var path = Theme.RoundedRect(new RectangleF(0, 0, Width, Height), Ui.S(this, Radius));
        var old = Region;
        Region = new Region(path);
        old?.Dispose();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.RoundedRect(new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f), Ui.S(this, Radius));
        using var pen = new Pen(Theme.Border, 1f);
        e.Graphics.DrawPath(pen, path);
    }
}

/// <summary>Small rounded pill used for importance previews.</summary>
public sealed class Badge : Control
{
    Color _badgeColor = Theme.AccentSoft;
    Color _textColor = Theme.Accent;

    public Color BadgeColor
    {
        get => _badgeColor;
        set { _badgeColor = value; Invalidate(); }
    }

    public Color TextColor
    {
        get => _textColor;
        set { _textColor = value; Invalidate(); }
    }

    public Badge()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Font = Theme.Font(8.4f, FontStyle.Bold);
        Height = 26;
        Width = 90;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new RectangleF(0, 0, Width, Height);
        Theme.FillRounded(g, rect, Height / 2f, _badgeColor);
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, _textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
    }

    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
}

public sealed class SmoothFlowPanel : FlowLayoutPanel
{
    public SmoothFlowPanel()
    {
        DoubleBuffered = true;
        AutoScroll = true;
        WrapContents = false;
        FlowDirection = FlowDirection.TopDown;
        BackColor = Theme.Bg;
    }
}

public sealed class SmoothDataGridView : DataGridView
{
    public SmoothDataGridView() => DoubleBuffered = true;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyMetrics();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ApplyMetrics();
    }

    void ApplyMetrics()
    {
        RowTemplate.Height = (int)Math.Round(40 * DeviceDpi / 96f);
        ColumnHeadersHeight = (int)Math.Round(42 * DeviceDpi / 96f);
        foreach (DataGridViewRow row in Rows) row.Height = RowTemplate.Height;
    }
}

/// <summary>Two-state pill switch used for the task library tabs.</summary>
public sealed class SegmentedControl : Control
{
    readonly string[] _items;
    int _selected;
    int _hover = -1;

    public event Action<int>? SelectedIndexChanged;

    public SegmentedControl(params string[] items)
    {
        _items = items;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Font = Theme.Font(9.2f, FontStyle.Bold);
        Cursor = Cursors.Hand;
        Height = 36;
        Width = 200;
    }

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            var index = Math.Clamp(value, 0, _items.Length - 1);
            if (_selected == index) return;
            _selected = index;
            Invalidate();
            SelectedIndexChanged?.Invoke(index);
        }
    }

    public void SetLabel(int index, string text)
    {
        if (index < 0 || index >= _items.Length || _items[index] == text) return;
        _items[index] = text;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var radius = Ui.S(this, 9);
        var outer = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        using (var path = Theme.RoundedRect(outer, radius))
        {
            using var fill = new SolidBrush(Theme.SurfaceAlt);
            g.FillPath(fill, path);
            using var pen = new Pen(Theme.BorderStrong);
            g.DrawPath(pen, path);
        }

        var segmentWidth = (Width - 2) / (float)_items.Length;
        for (var i = 0; i < _items.Length; i++)
        {
            var rect = new RectangleF(2 + i * segmentWidth, 2, segmentWidth, Height - 4);
            if (i == _selected)
                Theme.FillRounded(g, rect, radius - 2, Theme.Accent);
            else if (i == _hover)
                Theme.FillRounded(g, rect, radius - 2, Theme.AccentSoft);
            TextRenderer.DrawText(g, _items[i], Font,
                Rectangle.Round(rect), i == _selected ? Color.White : Theme.SubText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }

    int HitTest(Point point)
    {
        if (point.X < 0 || point.X >= Width) return -1;
        return Math.Min(_items.Length - 1, point.X * _items.Length / Math.Max(1, Width));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var index = HitTest(e.Location);
        if (index != _hover)
        {
            _hover = index;
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
            var index = HitTest(e.Location);
            if (index >= 0) SelectedIndex = index;
        }
        base.OnMouseUp(e);
    }
}

/// <summary>
/// NumericUpDown whose wheel advances one <see cref="NumericUpDown.Increment"/> per wheel notch.
/// The stock control multiplies each notch by the system's wheel scroll lines (usually 3).
/// </summary>
sealed class WheelStepNumericUpDown : NumericUpDown
{
    int _wheelDelta;

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        // Keep the form from scrolling underneath the spinner as well.
        if (e is HandledMouseEventArgs handled) handled.Handled = true;

        _wheelDelta += e.Delta;
        var notches = _wheelDelta / 120;
        if (notches == 0) return;
        _wheelDelta -= notches * 120;
        Value = Math.Clamp(Value + notches * Increment, Minimum, Maximum);
    }
}

sealed class MenuColors : ProfessionalColorTable
{
    public override Color MenuItemSelected => Theme.AccentSoft;
    public override Color MenuItemSelectedGradientBegin => Theme.AccentSoft;
    public override Color MenuItemSelectedGradientEnd => Theme.AccentSoft;
    public override Color MenuItemBorder => Theme.AccentSoft;
    public override Color MenuBorder => Theme.Border;
    public override Color ToolStripDropDownBackground => Color.White;
    public override Color ImageMarginGradientBegin => Color.White;
    public override Color ImageMarginGradientMiddle => Color.White;
    public override Color ImageMarginGradientEnd => Color.White;
    public override Color SeparatorDark => Theme.Border;
    public override Color SeparatorLight => Theme.Border;
}
