using System.Drawing.Drawing2D;
using System.Reflection;

namespace TaskList.UI;

/// <summary>Loads the multi-resolution Icon.ico embedded in the executable.</summary>
public static class AppIcon
{
    static Icon? _icon;

    public static Icon Create()
    {
        if (_icon == null)
        {
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("TaskList.Icon.ico");
            _icon = stream != null ? new Icon(stream) : CreateFallback();
        }
        return (Icon)_icon.Clone();
    }

    public static Bitmap Bitmap(int size)
    {
        using var source = Create();
        using var icon = new Icon(source, size, size);
        return icon.ToBitmap();
    }

    static Icon CreateFallback()
    {
        using var bmp = new Bitmap(64, 64);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            Theme.FillRounded(g, new RectangleF(2, 2, 60, 60), 16, Theme.Accent);
            using var pen = new Pen(Color.White, 6f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round,
            };
            g.DrawLines(pen, [new PointF(18, 33), new PointF(28, 43), new PointF(47, 21)]);
        }

        var handle = bmp.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    static extern bool DestroyIcon(IntPtr handle);
}
