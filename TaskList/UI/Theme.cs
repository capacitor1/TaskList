using System.Drawing.Drawing2D;

namespace TaskList.UI;

public static class Theme
{
    public const string Family = "Microsoft YaHei UI";

    public static readonly Color Bg = Color.FromArgb(245, 246, 250);
    public static readonly Color Surface = Color.White;
    public static readonly Color SurfaceAlt = Color.FromArgb(248, 249, 253);
    public static readonly Color Border = Color.FromArgb(229, 233, 242);
    public static readonly Color BorderStrong = Color.FromArgb(211, 219, 234);
    public static readonly Color Text = Color.FromArgb(31, 36, 48);
    public static readonly Color SubText = Color.FromArgb(116, 125, 145);
    public static readonly Color Muted = Color.FromArgb(154, 162, 181);
    static Color _accent = Color.FromArgb(76, 102, 229);

    public static Color Accent => _accent;
    public static Color AccentHover => Blend(_accent, Color.Black, 0.18f);
    public static Color AccentSoft => Blend(_accent, Color.White, 0.88f);
    public static readonly Color Danger = Color.FromArgb(217, 45, 32);
    public static readonly Color Success = Color.FromArgb(22, 163, 74);
    public static readonly Color Warning = Color.FromArgb(214, 158, 46);

    static readonly Dictionary<(float, FontStyle), Font> FontCache = new();
    static Font? _iconFont;

    public static Font IconFont => _iconFont ??=
        new Font("Segoe MDL2 Assets", 12f, FontStyle.Regular, GraphicsUnit.Point);

    public static void SetAccent(Color color) => _accent = color;

    public static Color Blend(Color from, Color to, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            (int)Math.Round(from.R + (to.R - from.R) * amount),
            (int)Math.Round(from.G + (to.G - from.G) * amount),
            (int)Math.Round(from.B + (to.B - from.B) * amount));
    }

    public static bool TryParseColor(string? text, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var value = text.Trim().TrimStart('#');
        if (value.Length != 6 ||
            !int.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            return false;
        color = Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        return true;
    }

    public static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    public static Font Font(float size, FontStyle style = FontStyle.Regular)
    {
        var key = (size, style);
        if (!FontCache.TryGetValue(key, out var font))
        {
            font = new Font(Family, size, style, GraphicsUnit.Point);
            FontCache[key] = font;
        }
        return font;
    }

    public static (string Label, Color Color, Color Text) ImportanceStyle(int importance)
    {
        importance = Math.Clamp(importance, 0, 10);
        return importance switch
        {
            0 => ("紧急 0", Color.FromArgb(139, 29, 44), Color.White),
            <= 3 => ($"重要 {importance}", Color.FromArgb(229, 72, 77), Color.White),
            <= 6 => ($"次要 {importance}", Color.FromArgb(242, 201, 76), Color.FromArgb(58, 46, 0)),
            <= 9 => ($"普通 {importance}", Color.FromArgb(62, 99, 221), Color.White),
            _ => ("不重要 10", Color.FromArgb(43, 47, 54), Color.White),
        };
    }

    public static Color ImportanceColor(int importance) => ImportanceStyle(importance).Color;

    public static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0 || rect.Width <= 0 || rect.Height <= 0)
        {
            path.AddRectangle(rect);
            return path;
        }
        var d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRounded(Graphics g, RectangleF rect, float radius, Color color)
    {
        using var path = RoundedRect(rect, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }
}

public static class Ui
{
    public static int S(Control control, float value) =>
        (int)Math.Round(value * control.DeviceDpi / 96f);

    public static ContextMenuStrip CreateMenu() => new()
    {
        Font = Theme.Font(9.5f),
        BackColor = Color.White,
        Renderer = new ToolStripProfessionalRenderer(new MenuColors()) { RoundedEdges = false },
    };

    public static Label Label(string text, float size, Color color, FontStyle style = FontStyle.Regular) => new()
    {
        Text = text,
        AutoSize = true,
        Font = Theme.Font(size, style),
        ForeColor = color,
        BackColor = Color.Transparent,
        Margin = Padding.Empty,
    };
}
