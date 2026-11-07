using System.Drawing.Drawing2D;
using QRCoder;

namespace CargoPlus;

internal static class QrMark
{
    public static void Draw(Graphics graphics, RectangleF area, string text)
    {
        if (string.IsNullOrWhiteSpace(text) || area.Width <= 0 || area.Height <= 0)
            return;

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        using var code = new QRCode(data);
        using var bitmap = code.GetGraphic(8, Color.Black, Color.White, true);
        var mode = graphics.InterpolationMode;
        var offset = graphics.PixelOffsetMode;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.DrawImage(bitmap, area);
        graphics.InterpolationMode = mode;
        graphics.PixelOffsetMode = offset;
    }
}
