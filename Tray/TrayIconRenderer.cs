using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Runtime.InteropServices;

namespace BleListener;

/// <summary>Draws the CO₂ value onto a tray icon: a rounded square coloured by air quality with the number on it.</summary>
public static class TrayIconRenderer
{
    private static readonly System.Drawing.Color Good = System.Drawing.Color.FromArgb(0x2E, 0xC2, 0x7E);
    private static readonly System.Drawing.Color Fair = System.Drawing.Color.FromArgb(0xF5, 0xB9, 0x42);
    private static readonly System.Drawing.Color Poor = System.Drawing.Color.FromArgb(0xEF, 0x5B, 0x5B);
    private static readonly System.Drawing.Color Stale = System.Drawing.Color.FromArgb(0x6B, 0x76, 0x86);
    private static readonly System.Drawing.Color DarkText = System.Drawing.Color.FromArgb(0x0F, 0x1B, 0x2E);

    /// <summary>
    /// What fits on a ~16 px icon: the exact number below 1000 ("820"), otherwise thousands with one decimal ("1.3k").
    /// The tray tooltip always carries the exact value.
    /// </summary>
    public static string FormatLabel(int ppm)
    {
        if (ppm <= 0) return "--";
        if (ppm < 1_000) return ppm.ToString(CultureInfo.InvariantCulture);

        var tenths = (ppm + 50) / 100; // integer rounding avoids floating-point surprises like 1.95 -> 1.9
        return tenths >= 100 ? "10k" : string.Create(CultureInfo.InvariantCulture, $"{tenths / 10}.{tenths % 10}k");
    }

    /// <summary>Creates a new icon. The caller owns it and must dispose it.</summary>
    public static System.Drawing.Icon Create(int ppm, bool stale)
    {
        var size = Math.Max(16, System.Windows.Forms.SystemInformation.SmallIconSize.Width);
        var (background, foreground) = Colours(ppm, stale);
        var label = FormatLabel(ppm);

        using var bitmap = new System.Drawing.Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.Clear(System.Drawing.Color.Transparent);

            using (var path = RoundedRectangle(size, size * 0.24f))
            using (var fill = new System.Drawing.SolidBrush(background))
                graphics.FillPath(fill, path);

            DrawFittedLabel(graphics, label, size, foreground);
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

    private static void DrawFittedLabel(System.Drawing.Graphics graphics, string label, int size, System.Drawing.Color colour)
    {
        // Shrink the font until the text fits with a pixel of padding; tight (typographic) metrics keep it centred.
        using var format = new System.Drawing.StringFormat(System.Drawing.StringFormat.GenericTypographic);
        using var brush = new System.Drawing.SolidBrush(colour);

        for (var fontSize = size * 0.68f; fontSize >= 5f; fontSize -= 0.5f)
        {
            using var font = new System.Drawing.Font("Segoe UI", fontSize, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
            var measured = graphics.MeasureString(label, font, System.Drawing.PointF.Empty, format);
            if (measured.Width > size - 2 && fontSize > 5f) continue;

            var x = (size - measured.Width) / 2f;
            var y = (size - measured.Height) / 2f;
            graphics.DrawString(label, font, brush, x, y, format);
            return;
        }
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
