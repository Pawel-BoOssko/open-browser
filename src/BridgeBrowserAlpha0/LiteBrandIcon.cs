using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace BridgeBrowserAlpha0;

/// <summary>
/// High-contrast, deliberately minimal Open Browser mark. Small 16 px sizes
/// retain the same single white ring + cyan arrow, with no unreadable details.
/// Generated multi-resolution ICO is used by the window and embedded in EXE.
/// </summary>
internal static class LiteBrandIcon
{
    internal static string Destination => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenBrowser", "ChatGPT-Lite", "OpenBrowser.ico");

    private static readonly int[] Sizes = { 16, 20, 24, 32, 48, 64, 128, 256 };

    internal static Icon Create()
    {
        using var stream = new MemoryStream(BuildIcoBytes(), writable: false);
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }

    internal static byte[] BuildIcoBytes()
    {
        var frames = new List<byte[]>(Sizes.Length);
        foreach (int size in Sizes)
        {
            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Transparent);
                g.ScaleTransform(size / 256f, size / 256f);
                var tile = RoundedRectangle(new RectangleF(8, 8, 240, 240), 52);
                using (tile)
                using (var baseColor = new SolidBrush(Color.FromArgb(15, 59, 191)))
                    g.FillPath(baseColor, tile);

                // 'O' remains legible when Windows downsamples to 16 x 16.
                using (var outer = new Pen(Color.White, 41f))
                    g.DrawEllipse(outer, 65, 65, 126, 126);

                // Single accent rather than tiny text or decorative windows.
                PointF[] arrow = {
                    new(114, 98), new(170, 128), new(114, 158)
                };
                using (var accent = new SolidBrush(Color.FromArgb(72, 241, 227)))
                    g.FillPolygon(accent, arrow);
            }
            using var buffer = new MemoryStream();
            bitmap.Save(buffer, ImageFormat.Png);
            frames.Add(buffer.ToArray());
        }
        using var data = new MemoryStream();
        using var writer = new BinaryWriter(data);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)Sizes.Length);
        int offset = 6 + 16 * Sizes.Length;
        for (int i = 0; i < Sizes.Length; i++)
        {
            int size = Sizes[i];
            writer.Write((byte)(size == 256 ? 0 : size));
            writer.Write((byte)(size == 256 ? 0 : size));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write((uint)frames[i].Length);
            writer.Write((uint)offset);
            offset += frames[i].Length;
        }
        foreach (var frame in frames) writer.Write(frame);
        return data.ToArray();
    }

    internal static bool Export() => ExportTo(Destination);

    internal static bool ExportTo(string path)
    {
        try
        {
            string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (dir is not null) Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, BuildIcoBytes());
            LiteEventLog.Write("brand_icon_ready", new { sizes = Sizes.Length });
            return true;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("brand_icon_failed", new { type = ex.GetType().Name });
            return false;
        }
    }

    internal static int SelfTest()
    {
        try
        {
            var ico = BuildIcoBytes();
            using var icon = Create();
            bool valid = ico.Length > 1000 && BitConverter.ToUInt16(ico, 4) == Sizes.Length &&
                icon.Width > 0 && icon.Height > 0;
            LiteEventLog.Write("brand_icon_selftest", new { passed = valid });
            return valid ? 0 : 2;
        }
        catch { return 3; }
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
