using System.Globalization;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace CargoPlus;

internal static class CargoPrinters
{
    public static void PrintLabel(AppSettings settings, SessionInfo session, BagRecord bag)
    {
        if (PrinterAddress.TryParse(settings.LabelPrinter, out var address))
        {
            PrintLabelRaw(address, settings, session, bag);
            return;
        }

        using var document = Create(settings.LabelPrinter, UiText.NoLabelPrinter);
        document.DocumentName = "Cargo Plus " + bag.TrackCode;
        document.PrintPage += (_, e) => DrawLabel(e, settings, session, bag);
        document.Print();
    }

    public static void PrintReceipts(AppSettings settings, SessionInfo session, IReadOnlyList<BagRecord> bags, bool includeFirmCopy = true)
    {
        if (PrinterAddress.TryParse(settings.ReceiptPrinter, out var address))
        {
            PrintReceiptsRaw(address, settings, session, bags, includeFirmCopy);
            return;
        }

        PrintReceiptCopy(settings, session, bags, storeCopy: true);
        if (includeFirmCopy)
            PrintReceiptCopy(settings, session, bags, storeCopy: false);
    }

    private static void PrintReceiptCopy(AppSettings settings, SessionInfo session, IReadOnlyList<BagRecord> bags, bool storeCopy)
    {
        using var document = Create(settings.ReceiptPrinter, UiText.NoReceiptPrinter);
        document.DocumentName = "Cargo Plus " + session.Kod;
        var width = document.DefaultPageSettings.PaperSize.Width;
        if (width < 120)
            width = 315;
        var widthMm = width * 25.4f / 100f;
        var used = MeasureReceipt(widthMm, settings, session, bags, storeCopy);
        var height = (int)Math.Ceiling((used + 6f) / 25.4f * 100f);
        document.DefaultPageSettings.PaperSize = new PaperSize("CargoPlusReceipt", width, Math.Max(height, 80));
        document.PrintPage += (_, e) => DrawReceipt(e, settings, session, bags, storeCopy);
        document.Print();
    }

    private static PrintDocument Create(string printerName, string missingMessage)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            throw new InvalidOperationException(missingMessage);

        var document = new PrintDocument();
        document.PrinterSettings.PrinterName = printerName;
        if (!document.PrinterSettings.IsValid)
            throw new InvalidOperationException(UiText.PrinterMissing(printerName));

        document.PrintController = new StandardPrintController();
        document.DefaultPageSettings.Margins = new Margins(8, 8, 8, 8);
        return document;
    }

    private static void DrawLabel(PrintPageEventArgs e, AppSettings settings, SessionInfo session, BagRecord bag)
    {
        var graphics = e.Graphics ?? throw new InvalidOperationException(UiText.NoLabelPage);
        var (width, _) = PageMillimeters(e, graphics);
        Prepare(graphics);
        PaintLabel(graphics, width, settings, session, bag);
        e.HasMorePages = false;
    }

    private static void PaintLabel(Graphics graphics, float width, AppSettings settings, SessionInfo session, BagRecord bag)
    {
        var words = PrintWords.From(settings);
        var margin = 2.5f;
        var x = margin;
        var y = margin;
        var contentWidth = Math.Max(20, width - margin * 2);
        using var ink = new SolidBrush(Color.Black);
        using var title = new Font("Segoe UI", 10f, FontStyle.Bold);
        using var track = new Font("Segoe UI", 16f, FontStyle.Bold);
        using var strong = new Font("Segoe UI", 13f, FontStyle.Bold);
        using var body = new Font("Segoe UI", 8f);
        using var center = new StringFormat
        {
            Alignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
        };
        using var line = new StringFormat
        {
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
        };

        y = DrawBrand(graphics, settings, x, y, contentWidth, 12f, title, body, ink, center);

        var barcodeHeight = 16f;
        var bagNumber = bag.KodId.ToString(CultureInfo.InvariantCulture);
        Code128.Draw(graphics, new RectangleF(x, y, contentWidth, barcodeHeight), bagNumber);
        y += barcodeHeight + 1f;
        graphics.DrawString(bagNumber, track, ink, new RectangleF(x, y, contentWidth, 7), center);
        y += 6.4f;
        DrawLine(graphics, strong, ink, line, ref y, contentWidth, x, words.BolumBanner(bag.Bolum), 6.2f);
        DrawLine(graphics, body, ink, line, ref y, contentWidth, x, $"{words.Client}: {bag.Kod}", 4.4f);
        DrawLine(graphics, strong, ink, line, ref y, contentWidth, x, $"{words.Kilo}: {bag.Kilo}", 6.2f);
        DrawLine(graphics, body, ink, line, ref y, contentWidth, x, $"{words.Person}: {CargoRules.MaskPart(session.ClientName, 3)} {CargoRules.MaskPart(session.ClientSurname, 3)}".Trim(), 4.4f);
        if (!string.IsNullOrWhiteSpace(session.ClientPhone))
            DrawLine(graphics, body, ink, line, ref y, contentWidth, x, $"{words.Phone}: {CargoRules.MaskPhone(session.ClientPhone)}", 4.4f);
        DrawLine(graphics, body, ink, line, ref y, contentWidth, x, $"{words.Store}: {bag.StoreName}", 4.4f);
        DrawLine(graphics, body, ink, line, ref y, contentWidth, x, $"{words.StorePhone}: {bag.StorePhone}", 4.4f);
        DrawLine(graphics, body, ink, line, ref y, contentWidth, x, $"{words.Accepted}: {session.OperatorName}", 4.4f);
        DrawLine(graphics, body, ink, line, ref y, contentWidth, x, bag.Tarih.ToString("dd.MM.yyyy HH:mm"), 4.4f);
    }

    private static void DrawReceipt(PrintPageEventArgs e, AppSettings settings, SessionInfo session, IReadOnlyList<BagRecord> bags, bool storeCopy)
    {
        var graphics = e.Graphics ?? throw new InvalidOperationException(UiText.NoReceiptPage);
        var (width, _) = PageMillimeters(e, graphics);
        Prepare(graphics);
        PaintReceipt(graphics, width, settings, session, bags, storeCopy);
    }

    private static float PaintReceipt(Graphics graphics, float width, AppSettings settings, SessionInfo session, IReadOnlyList<BagRecord> bags, bool storeCopy)
    {
        var words = PrintWords.From(settings);
        var margin = 3f;
        var x = margin;
        var y = margin;
        var contentWidth = Math.Max(30, Math.Min(width - margin * 2, 74));
        using var ink = new SolidBrush(Color.Black);
        using var title = new Font("Segoe UI", 11f, FontStyle.Bold);
        using var head = new Font("Segoe UI", 11f, FontStyle.Bold);
        using var body = new Font("Segoe UI", 8f);
        using var bagFont = new Font("Segoe UI", 13f, FontStyle.Bold);
        using var center = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        using var line = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        using var wrap = new StringFormat { Trimming = StringTrimming.Word };
        using var pen = new Pen(Color.Black, 0.35f);

        y = DrawBrand(graphics, settings, x, y, contentWidth, 14f, title, body, ink, center);
        var bolum = bags.Count > 0 ? bags[0].Bolum : "";
        if (bolum.Length > 0)
        {
            DrawLine(graphics, body, ink, center, ref y, contentWidth, x, words.ShipmentKind, 4.2f);
            using var banner = FitFont(graphics, words.BolumBanner(bolum), 26f, contentWidth);
            var bannerHeight = Math.Max(8f, graphics.MeasureString(words.BolumBanner(bolum), banner, new SizeF(contentWidth, 40f)).Height);
            graphics.DrawString(words.BolumBanner(bolum), banner, ink, new RectangleF(x, y, contentWidth, bannerHeight + 1f), center);
            y += bannerHeight + 1.2f;
        }

        var copyFont = storeCopy
            ? new Font("Segoe UI", 16f, FontStyle.Bold)
            : new Font("Segoe UI", 11f, FontStyle.Bold);
        using (copyFont)
        {
            var copy = storeCopy ? words.CopyStore : words.CopyFirm;
            var copyHeight = Math.Max(storeCopy ? 7f : 5f, graphics.MeasureString(copy, copyFont, new SizeF(contentWidth, 30f)).Height);
            graphics.DrawString(copy, copyFont, ink, new RectangleF(x, y, contentWidth, copyHeight + 1f), center);
            y += copyHeight + 1.4f;
        }

        if (!string.IsNullOrWhiteSpace(session.TrackCode))
            DrawLine(graphics, head, ink, center, ref y, contentWidth, x, $"{words.Track}: {session.TrackCode}", 5.4f);

        var moment = bags.Count > 0 ? bags[0].Tarih : DateTime.Now;
        DrawLine(graphics, body, ink, line, ref y, contentWidth, x, moment.ToString("dd.MM.yyyy HH:mm"), 4.4f);
        DrawLine(graphics, body, ink, line, ref y, contentWidth, x, $"{words.Accepted}: {session.OperatorName}", 4.4f);
        DrawLine(graphics, body, ink, line, ref y, contentWidth, x, $"{words.Client}: {session.Kod}", 4.4f);
        DrawLine(graphics, body, ink, line, ref y, contentWidth, x, $"{words.Person}: {CargoRules.MaskPart(session.ClientName, 3)} {CargoRules.MaskPart(session.ClientSurname, 3)}".Trim(), 4.4f);
        if (!string.IsNullOrWhiteSpace(session.ClientPhone))
            DrawLine(graphics, body, ink, line, ref y, contentWidth, x, $"{words.Phone}: {CargoRules.MaskPhone(session.ClientPhone)}", 4.4f);
        DrawLine(graphics, body, ink, line, ref y, contentWidth, x, $"{words.Store}: {session.StoreName}", 4.4f);
        DrawLine(graphics, body, ink, line, ref y, contentWidth, x, $"{words.StorePhone}: {session.StorePhone}", 4.4f);
        y += 1.2f;

        var colKilo = x + contentWidth * 0.56f;
        graphics.DrawLine(pen, x, y, x + contentWidth, y);
        y += 0.8f;
        graphics.DrawString(words.Bag, head, ink, x, y);
        graphics.DrawString(words.Kilo, head, ink, colKilo, y);
        y += 5.6f;
        graphics.DrawLine(pen, x, y, x + contentWidth, y);

        decimal kilos = 0;
        foreach (var bag in bags)
        {
            kilos += CargoRules.ParseKilo(bag.Kilo);
            y += 0.7f;
            var number = bag.KodId.ToString(CultureInfo.InvariantCulture);
            graphics.DrawString(number, bagFont, ink, new RectangleF(x, y, colKilo - x - 1, 6.4f), line);
            graphics.DrawString(bag.Kilo, bagFont, ink, new RectangleF(colKilo, y, x + contentWidth - colKilo, 6.4f), line);
            y += 6.4f;
        }

        graphics.DrawLine(pen, x, y, x + contentWidth, y);
        y += 1.4f;
        DrawLine(graphics, head, ink, line, ref y, contentWidth, x, $"{words.TotalBags}: {bags.Count}", 5.2f);
        DrawLine(graphics, head, ink, line, ref y, contentWidth, x, $"{words.TotalKilo}: {CargoRules.FormatWeight(kilos)}", 5.2f);
        if (!string.IsNullOrWhiteSpace(session.TrackCode))
        {
            y += 2f;
            var qr = Math.Min(64f, contentWidth);
            QrMark.Draw(graphics, new RectangleF(x + (contentWidth - qr) / 2f, y, qr, qr), CargoRules.TrackingLink(session.TrackCode));
            y += qr;
        }

        if (!storeCopy)
        {
            var notes = bags.Where(HasNote).ToList();
            if (notes.Count > 0)
            {
                y += 2f;
                graphics.DrawLine(pen, x, y, x + contentWidth, y);
                y += 1.2f;
                DrawLine(graphics, head, ink, line, ref y, contentWidth, x, words.Note, 5f);
                foreach (var bag in notes)
                {
                    var text = $"{bag.KodId}  {bag.Not1.Trim()}";
                    var size = graphics.MeasureString(text, body, new SizeF(contentWidth, 40f));
                    graphics.DrawString(text, body, ink, new RectangleF(x, y, contentWidth, size.Height + 0.4f), wrap);
                    y += size.Height + 0.6f;
                }
            }
        }

        return y + margin;
    }

    private static float DrawBrand(Graphics graphics, AppSettings settings, float x, float y, float contentWidth, float logoHeight, Font title, Font body, Brush ink, StringFormat center)
    {
        var company = string.IsNullOrWhiteSpace(settings.Company) ? "CARGO PLUS" : settings.Company.Trim();
        using var logo = BrandLogo.TryLoad(settings.LogoPath);
        var textX = x;
        var textWidth = contentWidth;
        var block = 0f;
        if (logo is not null)
        {
            var logoWidth = logoHeight * logo.Width / Math.Max(1, logo.Height);
            if (logoWidth > contentWidth * 0.38f)
                logoWidth = contentWidth * 0.38f;
            var drawnHeight = logoWidth * logo.Height / Math.Max(1, logo.Width);
            graphics.DrawImage(logo, x, y, logoWidth, drawnHeight);
            textX = x + logoWidth + 1.6f;
            textWidth = Math.Max(12f, contentWidth - logoWidth - 1.6f);
            block = drawnHeight;
        }

        graphics.DrawString(company, title, ink, new RectangleF(textX, y, textWidth, 5.6f), center);
        var textBlock = 5.2f;
        if (!string.IsNullOrWhiteSpace(settings.Branch))
        {
            graphics.DrawString(settings.Branch.Trim(), body, ink, new RectangleF(textX, y + 5.2f, textWidth, 4.4f), center);
            textBlock = 9.6f;
        }

        return y + Math.Max(block, textBlock) + 1.2f;
    }

    private static Font FitFont(Graphics graphics, string text, float maxPoint, float width)
    {
        for (var size = maxPoint; size >= 12f; size -= 1f)
        {
            var font = new Font("Segoe UI", size, FontStyle.Bold);
            if (graphics.MeasureString(text, font).Width <= width)
                return font;
            font.Dispose();
        }

        return new Font("Segoe UI", 12f, FontStyle.Bold);
    }

    private static bool HasNote(BagRecord bag) =>
        !string.IsNullOrWhiteSpace(bag.Not1) && bag.Not1.Trim() != "0";

    private static float MeasureReceipt(float widthMm, AppSettings settings, SessionInfo session, IReadOnlyList<BagRecord> bags, bool storeCopy)
    {
        using var bitmap = Sheet(800, 4800);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        Prepare(graphics);
        return PaintReceipt(graphics, widthMm, settings, session, bags, storeCopy);
    }

    private static void Prepare(Graphics graphics)
    {
        graphics.PageUnit = GraphicsUnit.Millimeter;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
    }

    private static void PrintLabelRaw(IPAddress address, AppSettings settings, SessionInfo session, BagRecord bag)
    {
        using var bitmap = Sheet(800, 900);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.White);
            Prepare(graphics);
            PaintLabel(graphics, WidthMm(bitmap), settings, session, bag);
        }

        Send(address, Argox(bitmap), UiText.KindLabels);
    }

    private static void PrintReceiptsRaw(IPAddress address, AppSettings settings, SessionInfo session, IReadOnlyList<BagRecord> bags, bool includeFirmCopy)
    {
        var payload = new List<byte> { 0x1B, 0x40 };
        foreach (var storeCopy in includeFirmCopy ? new[] { true, false } : new[] { true })
        {
            using var tall = Sheet(576, 4800);
            float used;
            using (var graphics = Graphics.FromImage(tall))
            {
                graphics.Clear(Color.White);
                Prepare(graphics);
                used = PaintReceipt(graphics, WidthMm(tall), settings, session, bags, storeCopy);
            }

            var pixels = (int)Math.Ceiling(used / 25.4f * tall.VerticalResolution) + 8;
            using var bitmap = CropTop(tall, pixels);
            payload.AddRange(EscPos(bitmap));
            payload.AddRange([0x1B, 0x64, 0x02, 0x1D, 0x56, 0x42, 0x00]);
        }

        Send(address, payload.ToArray(), UiText.KindReceipts);
    }

    private static Bitmap Sheet(int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        bitmap.SetResolution(203, 203);
        return bitmap;
    }

    private static float WidthMm(Bitmap bitmap) => bitmap.Width / bitmap.HorizontalResolution * 25.4f;

    private static Bitmap CropTop(Bitmap source, int height)
    {
        height = Math.Clamp(height, 1, source.Height);
        var target = new Bitmap(source.Width, height, PixelFormat.Format24bppRgb);
        target.SetResolution(source.HorizontalResolution, source.VerticalResolution);
        using var graphics = Graphics.FromImage(target);
        graphics.DrawImage(
            source,
            new Rectangle(0, 0, source.Width, height),
            new Rectangle(0, 0, source.Width, height),
            GraphicsUnit.Pixel);
        return target;
    }

    private static byte[] Argox(Bitmap bitmap)
    {
        var rows = Mono(bitmap, out var stride, out var height);
        var header = System.Text.Encoding.ASCII.GetBytes(
            $"\r\nN\r\nq{bitmap.Width}\r\nQ{height},24\r\nGW0,0,{stride},{height},");
        var footer = "\r\nP1\r\n"u8.ToArray();
        var payload = new byte[header.Length + rows.Length + footer.Length];
        header.CopyTo(payload, 0);
        rows.CopyTo(payload, header.Length);
        footer.CopyTo(payload, header.Length + rows.Length);
        return payload;
    }

    private static byte[] EscPos(Bitmap bitmap)
    {
        var rows = Mono(bitmap, out var stride, out var height);
        var payload = new List<byte>();
        const int chunk = 256;
        for (var y = 0; y < height; y += chunk)
        {
            var part = Math.Min(chunk, height - y);
            payload.AddRange([0x1D, 0x76, 0x30, 0x00, (byte)stride, (byte)(stride >> 8), (byte)part, (byte)(part >> 8)]);
            payload.AddRange(new ArraySegment<byte>(rows, y * stride, part * stride));
        }

        return payload.ToArray();
    }

    private static byte[] Mono(Bitmap bitmap, out int stride, out int height)
    {
        height = bitmap.Height;
        stride = (bitmap.Width + 7) / 8;
        var data = new byte[stride * height];
        var bits = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            var rowBytes = Math.Abs(bits.Stride);
            var buffer = new byte[rowBytes * height];
            Marshal.Copy(bits.Scan0, buffer, 0, buffer.Length);
            for (var y = 0; y < height; y++)
            {
                var source = y * rowBytes;
                var target = y * stride;
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var pixel = source + x * 3;
                    if (buffer[pixel] + buffer[pixel + 1] + buffer[pixel + 2] < 520)
                        data[target + (x >> 3)] |= (byte)(0x80 >> (x & 7));
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(bits);
        }

        return data;
    }

    private static void Send(IPAddress address, byte[] payload, string kind)
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var connect = socket.BeginConnect(address, 9100, null, null);
        if (!connect.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5)))
        {
            socket.Close();
            throw new InvalidOperationException(UiText.PrinterSilent(kind, address.ToString()));
        }

        try
        {
            socket.EndConnect(connect);
            var sent = 0;
            while (sent < payload.Length)
                sent += socket.Send(payload, sent, payload.Length - sent, SocketFlags.None);
        }
        catch (Exception ex) when (ex is IOException or SocketException)
        {
            throw new InvalidOperationException(UiText.PrinterSilent(kind, address.ToString()));
        }
    }

    private static void DrawLine(Graphics graphics, Font font, Brush brush, StringFormat format, ref float y, float width, float x, string text, float step = 5.2f)
    {
        graphics.DrawString(text, font, brush, new RectangleF(x, y, width, step), format);
        y += step;
    }

    private static (float Width, float Height) PageMillimeters(PrintPageEventArgs e, Graphics graphics)
    {
        var width = e.PageBounds.Width * 25.4f / 100f;
        var height = e.PageBounds.Height * 25.4f / 100f;
        if (width < 20)
            width = graphics.VisibleClipBounds.Width;
        if (height < 20)
            height = graphics.VisibleClipBounds.Height;
        return (width, height);
    }
}
