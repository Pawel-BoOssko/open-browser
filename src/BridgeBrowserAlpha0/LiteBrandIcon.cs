using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Branded ChatGPT Lite icon. The ICO asset is generated locally without any
/// network dependency and is also assigned to the native WinForms window.
/// </summary>
internal static class LiteBrandIcon
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    internal static string Destination => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenBrowser", "ChatGPT-Lite", "OpenBrowser.ico");

    internal static Icon Create()
    {
        using var bitmap = new Bitmap(128, 128);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            var bounds = new RectangleF(5, 5, 118, 118);
            using var tile = RoundedRectangle(bounds, 26);
            using var background = new LinearGradientBrush(
                bounds, Color.FromArgb(13, 35, 91), Color.FromArgb(0, 152, 228),
                LinearGradientMode.ForwardDiagonal);
            graphics.FillPath(background, tile);
            using var border = new Pen(Color.FromArgb(86, 226, 255), 3f);
            graphics.DrawPath(border, tile);

            using var back = RoundedRectangle(new RectangleF(35, 27, 78, 69), 11);
            using var backBrush = new SolidBrush(Color.FromArgb(35, 85, 196));
            graphics.FillPath(backBrush, back);
            using var front = RoundedRectangle(new RectangleF(15, 42, 87, 67), 13);
            using var frontBrush = new LinearGradientBrush(
                new Rectangle(15, 42, 87, 67), Color.FromArgb(0, 205, 228),
                Color.FromArgb(19, 67, 185), LinearGradientMode.ForwardDiagonal);
            graphics.FillPath(frontBrush, front);
            using var highlight = new Pen(Color.FromArgb(210, 245, 255), 3f);
            graphics.DrawPath(highlight, front);

            using var lightBubble = RoundedRectangle(new RectangleF(29, 57, 57, 30), 10);
            using var bubbleBrush = new SolidBrush(Color.FromArgb(222, 255, 255));
            graphics.FillPath(bubbleBrush, lightBubble);
            using var blue = new Pen(Color.FromArgb(19, 104, 196), 4f);
            graphics.DrawLine(blue, 39, 67, 74, 67);
            graphics.DrawLine(blue, 39, 77, 65, 77);
            using var dotA = new SolidBrush(Color.FromArgb(255, 111, 117));
            using var dotB = new SolidBrush(Color.FromArgb(255, 203, 85));
            using var dotC = new SolidBrush(Color.FromArgb(117, 237, 170));
            graphics.FillEllipse(dotA, 25, 48, 6, 6);
            graphics.FillEllipse(dotB, 34, 48, 6, 6);
            graphics.FillEllipse(dotC, 43, 48, 6, 6);
        }
        IntPtr handle = bitmap.GetHicon();
        try
        {
            using var raw = Icon.FromHandle(handle);
            return (Icon)raw.Clone();
        }
        finally { DestroyIcon(handle); }
    }

    internal static bool Export()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Destination)!);
            using var icon = Create();
            using var file = new FileStream(Destination, FileMode.Create,
                FileAccess.Write, FileShare.Read);
            icon.Save(file);
            LiteEventLog.Write("brand_icon_ready");
            return true;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("brand_icon_failed", new { type = ex.GetType().Name });
            return false;
        }
    }

    private static GraphicsPath RoundedRectangle(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float diameter = radius * 2;
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
