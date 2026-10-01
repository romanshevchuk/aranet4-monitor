using Aranet4Monitor.Models;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Aranet4Monitor.Presentation.Tray;

/// <summary>
/// Draws the CO₂ value onto a tray icon: a colour-coded tile that fills the whole icon, with the number
/// stretched to use every available pixel (or, optionally, just a coloured disc).
/// </summary>
public static class TrayIconRenderer
{
    private static readonly System.Drawing.Color Good = System.Drawing.Color.FromArgb(0x22, 0xC5, 0x6E);
    private static readonly System.Drawing.Color Fair = System.Drawing.Color.FromArgb(0xFF, 0xB8, 0x2E);
    private static readonly System.Drawing.Color Poor = System.Drawing.Color.FromArgb(0xEF, 0x44, 0x44);
    private static readonly System.Drawing.Color Stale = System.Drawing.Color.FromArgb(0x6B, 0x76, 0x86);
    private static readonly System.Drawing.Color DarkText = System.Drawing.Color.FromArgb(0x0B, 0x14, 0x24);

    /// <summary>
    /// What fits on a ~16–24 px icon: the exact number below 1000 ("820"), otherwise thousands with one decimal
    /// ("1.3" means 1,300 ppm; there is no room for a "k"). The tray tooltip always carries the exact value.
    /// </summary>
    public static string FormatLabel(int ppm)
    {
        if (ppm <= 0) return "--";
        if (ppm < 1_000) return ppm.ToString(CultureInfo.InvariantCulture);

        var tenths = (ppm + 50) / 100; // integer rounding avoids floating-point surprises like 1.95 -> 1.9
        return tenths >= 100 ? "10" : string.Create(CultureInfo.InvariantCulture, $"{tenths / 10}.{tenths % 10}");
    }

    /// <summary>Creates a new icon. The caller owns it and must dispose it.</summary>
    public static System.Drawing.Icon Create(int ppm, bool stale, bool showNumber = true)
    {
        var size = Math.Max(16, System.Windows.Forms.SystemInformation.SmallIconSize.Width);
        var (background, foreground) = Colours(ppm, stale);
        var label = FormatLabel(ppm);

        using var bitmap = new System.Drawing.Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(System.Drawing.Color.Transparent);

            using var fill = new System.Drawing.SolidBrush(background);
            if (showNumber)
            {
                // Full-bleed tile: no padding, so the digits get as much room as possible.
                using var path = RoundedRectangle(size, size * 0.2f);
                graphics.FillPath(fill, path);
                DrawFittedLabel(graphics, label, size, foreground);
            }
            else
            {
                graphics.FillEllipse(fill, 0.5f, 0.5f, size - 1f, size - 1f);
            }
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var native = System.Drawing.Icon.FromHandle(handle);
            return (System.Drawing.Icon)native.Clone(); // the clone owns its own copy, so the handle can be freed
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static (System.Drawing.Color Background, System.Drawing.Color Foreground) Colours(int ppm, bool stale)
    {
        if (stale || ppm <= 0) return (Stale, System.Drawing.Color.White);
        return Co2Quality.Classify(ppm) switch
        {
            Co2Level.Good => (Good, DarkText),
            Co2Level.Fair => (Fair, DarkText),
            _ => (Poor, System.Drawing.Color.White),
        };
    }

    /// <summary>
    /// Turns the text into an outline, measures its real ink bounds, and scales it to fill the tile:
    /// as tall as the icon allows and, if the label is wide, condensed horizontally rather than shrunk.
    /// </summary>
    private static void DrawFittedLabel(System.Drawing.Graphics graphics, string label, int size, System.Drawing.Color colour)
    {
        using var family = new System.Drawing.FontFamily("Segoe UI");
        using var format = new System.Drawing.StringFormat(System.Drawing.StringFormat.GenericTypographic);
        using var path = new GraphicsPath();
        path.AddString(label, family, (int)System.Drawing.FontStyle.Bold, 100f, System.Drawing.PointF.Empty, format);

        var bounds = path.GetBounds();
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var margin = Math.Max(1f, size / 16f);
        var maxWidth = size - 2 * margin;
        var maxHeight = size - 2 * margin;

        var scaleY = maxHeight / bounds.Height;
        var scaleX = Math.Min(scaleY, maxWidth / bounds.Width);
        // Don't squash glyphs beyond ~55% of their natural width; shrink the height instead.
        if (scaleX < scaleY * 0.55f)
        {
            scaleX = maxWidth / bounds.Width;
            scaleY = scaleX / 0.55f;
        }

        var drawnWidth = bounds.Width * scaleX;
        var drawnHeight = bounds.Height * scaleY;
        var offsetX = (size - drawnWidth) / 2f - bounds.X * scaleX;
        var offsetY = (size - drawnHeight) / 2f - bounds.Y * scaleY;

        using var matrix = new Matrix(scaleX, 0, 0, scaleY, offsetX, offsetY);
        path.Transform(matrix);
        using var brush = new System.Drawing.SolidBrush(colour);
        graphics.FillPath(brush, path);
    }

    private static GraphicsPath RoundedRectangle(float size, float radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(0, 0, d, d, 180, 90);
        path.AddArc(size - d, 0, d, d, 270, 90);
        path.AddArc(size - d, size - d, d, d, 0, 90);
        path.AddArc(0, size - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
