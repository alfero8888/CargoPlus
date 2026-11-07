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

        using var document = Create(settings.LabelPrinter, "В настройках не выбран принтер этикеток.");
        document.DocumentName = "Cargo Plus " + bag.TrackCode;
        document.PrintPage += (_, e) => DrawLabel(e, settings, session, bag);
        document.Print();
    }

    public static void PrintReceipts(AppSettings settings, SessionInfo session, IReadOnlyList<BagRecord> bags)
    {
        if (PrinterAddress.TryParse(settings.ReceiptPrinter, out var address))
        {
            PrintReceiptsRaw(address, settings, session, bags);
            return;
        }

        using var document = Create(settings.ReceiptPrinter, "В настройках не выбран чековый принтер.");
        document.DocumentName = "Cargo Plus чек " + session.Kod;
        var width = document.DefaultPageSettings.PaperSize.Width;
        if (width < 120)
            width = 315;
        var heightMm = Math.Max(220, 160 + bags.Count * 7);
        document.DefaultPageSettings.PaperSize = new PaperSize(
            "CargoPlusReceipt",
            width,
            (int)Math.Round(heightMm / 25.4 * 100));

        var page = 0;
        document.PrintPage += (_, e) =>
        {
            DrawReceipt(e, settings, session, bags, page == 0);
            page++;
            e.HasMorePages = page < 2;
        };
        document.Print();
    }

    private static PrintDocument Create(string printerName, string missingMessage)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            throw new InvalidOperationException(missingMessage);

        var document = new PrintDocument();
        document.PrinterSettings.PrinterName = printerName;
        if (!document.PrinterSettings.IsValid)
            throw new InvalidOperationException($"Принтер «{printerName}» не найден в Windows.");

        document.PrintController = new StandardPrintController();
        document.DefaultPageSettings.Margins = new Margins(8, 8, 8, 8);
        return document;
    }

    private static void DrawLabel(PrintPageEventArgs e, AppSettings settings, SessionInfo session, BagRecord bag)
    {
        var graphics = e.Graphics ?? throw new InvalidOperationException("Принтер не дал лист для этикетки.");
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
        using var track = new Font("Consolas", 11f, FontStyle.Bold);
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

        var company = string.IsNullOrWhiteSpace(settings.Company) ? "CARGO PLUS" : settings.Company.Trim();
        graphics.DrawString(company, title, ink, new RectangleF(x, y, contentWidth, 5), center);
        y += 4.6f;
        if (!string.IsNullOrWhiteSpace(settings.Branch))
        {
            graphics.DrawString(settings.Branch.Trim(), body, ink, new RectangleF(x, y, contentWidth, 4.5f), center);
            y += 4.4f;
        }

        var barcodeHeight = 16f;
        var bagNumber = bag.KodId.ToString(CultureInfo.InvariantCulture);
        Code128.Draw(graphics, new RectangleF(x, y, contentWidth, barcodeHeight), bagNumber);
        y += barcodeHeight + 1f;
        graphics.DrawString(bagNumber, track, ink, new RectangleF(x, y, contentWidth, 5), center);
        y += 5.2f;
        DrawLine(graphics, body, ink, line, ref y, contentWidth, x, $"{words.Client}: {bag.Kod}", 4.4f);
        DrawLine(graphics, body, ink, line, ref y, contentWidth, x, $"{words.Department}: {words.Bolum(bag.Bolum)}", 4.4f);
        DrawLine(graphics, body, ink, line, ref y, contentWidth, x, $"{words.Kilo}: {bag.Kilo}", 4.4f);
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
        var graphics = e.Graphics ?? throw new InvalidOperationException("Принтер не дал лист для чека.");
        var (width, _) = PageMillimeters(e, graphics);
        Prepare(graphics);
        PaintReceipt(graphics, width, settings, session, bags, storeCopy);
    }

    private static void PaintReceipt(Graphics graphics, float width, AppSettings settings, SessionInfo session, IReadOnlyList<BagRecord> bags, bool storeCopy)
    {
        var words = PrintWords.From(settings);
        var margin = 3f;
        var x = margin;
        var y = margin;
        var contentWidth = Math.Max(30, Math.Min(width - margin * 2, 74));
        using var ink = new SolidBrush(Color.Black);
        using var title = new Font("Segoe UI", 11f, FontStyle.Bold);
        using var head = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var body = new Font("Segoe UI", 8f);
        using var small = new Font("Segoe UI", 7.5f);
        using var center = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        using var line = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        using var pen = new Pen(Color.Black, 0.25f);

        var company = string.IsNullOrWhiteSpace(settings.Company) ? "CARGO PLUS" : settings.Company.Trim();
        graphics.DrawString(company, title, ink, new RectangleF(x, y, contentWidth, 5.5f), center);
        y += 5.2f;
        if (!string.IsNullOrWhiteSpace(settings.Branch))
        {
            graphics.DrawString(settings.Branch.Trim(), head, ink, new RectangleF(x, y, contentWidth, 4.5f), center);
            y += 4.6f;
        }

        graphics.DrawString(storeCopy ? words.CopyStore : words.CopyFirm, head, ink, new RectangleF(x, y, contentWidth, 4.5f), center);
        y += 5;
        var moment = bags.Count > 0 ? bags[0].Tarih : DateTime.Now;
        DrawLine(graphics, body, ink, line, ref y, contentWidth, x, moment.ToString("dd.MM.yyyy HH:mm"), 4.4f);
        DrawLine(graphics, body, ink, line, ref y, contentWidth, x, $"{words.Accepted}: {session.OperatorName}", 4.4f);
        DrawLine(graphics, body, ink, line, ref y, contentWidth, x, $"{words.Client}: {session.Kod}", 4.4f);
        DrawLine(graphics, body, ink, line, ref y, contentWidth, x, $"{words.Person}: {CargoRules.MaskPart(session.ClientName, 3)} {CargoRules.MaskPart(session.ClientSurname, 3)}".Trim(), 4.4f);
        if (!string.IsNullOrWhiteSpace(session.ClientPhone))
            DrawLine(graphics, body, ink, line, ref y, contentWidth, x, $"{words.Phone}: {CargoRules.MaskPhone(session.ClientPhone)}", 4.4f);
        DrawLine(graphics, body, ink, line, ref y, contentWidth, x, $"{words.Store}: {session.StoreName}", 4.4f);
        DrawLine(graphics, body, ink, line, ref y, contentWidth, x, $"{words.StorePhone}: {session.StorePhone}", 4.4f);
        if (!string.IsNullOrWhiteSpace(session.TrackCode))
            DrawLine(graphics, head, ink, line, ref y, contentWidth, x, $"{words.Track}: {session.TrackCode}", 4.6f);
        y += 1.5f;

        var colKilo = x + contentWidth * 0.42f;
        var colBolum = x + contentWidth * 0.66f;
        graphics.DrawLine(pen, x, y, x + contentWidth, y);
        y += 0.6f;
        graphics.DrawString(words.Bag, head, ink, x, y);
        graphics.DrawString(words.Kilo, head, ink, colKilo, y);
        graphics.DrawString(words.Department, head, ink, colBolum, y);
        y += 4.4f;
        graphics.DrawLine(pen, x, y, x + contentWidth, y);

        decimal kilos = 0;
        foreach (var bag in bags)
        {
            kilos += CargoRules.ParseKilo(bag.Kilo);
            y += 0.5f;
            graphics.DrawString(bag.KodId.ToString(CultureInfo.InvariantCulture), small, ink, new RectangleF(x, y, colKilo - x - 1, 4.2f), line);
            graphics.DrawString(bag.Kilo, small, ink, new RectangleF(colKilo, y, colBolum - colKilo - 1, 4.2f), line);
            graphics.DrawString(words.Bolum(bag.Bolum), small, ink, new RectangleF(colBolum, y, x + contentWidth - colBolum, 4.2f), line);
            y += 4.2f;
        }

        graphics.DrawLine(pen, x, y, x + contentWidth, y);
        y += 1.5f;
        DrawLine(graphics, head, ink, line, ref y, contentWidth, x, $"{words.TotalBags}: {bags.Count}", 4.6f);
        DrawLine(graphics, head, ink, line, ref y, contentWidth, x, $"{words.TotalKilo}: {CargoRules.FormatWeight(kilos)}", 4.6f);
        if (!string.IsNullOrWhiteSpace(session.TrackCode))
        {
            y += 2;
            var qr = Math.Min(32f, contentWidth);
            QrMark.Draw(graphics, new RectangleF(x + (contentWidth - qr) / 2, y, qr, qr), CargoRules.TrackingLink(session.TrackCode));
        }
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

        Send(address, Argox(bitmap), "этикеток");
    }

    private static void PrintReceiptsRaw(IPAddress address, AppSettings settings, SessionInfo session, IReadOnlyList<BagRecord> bags)
    {
        var height = 1100 + bags.Count * 40;
        var payload = new List<byte> { 0x1B, 0x40 };
        foreach (var copy in new[] { "store", "firm" })
        {
            using var bitmap = Sheet(576, height);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.White);
                Prepare(graphics);
                PaintReceipt(graphics, WidthMm(bitmap), settings, session, bags, copy == "store");
            }

            payload.AddRange(EscPos(bitmap));
            payload.AddRange([0x1B, 0x64, 0x03, 0x1D, 0x56, 0x42, 0x00]);
        }

        Send(address, payload.ToArray(), "чеков");
    }

    private static Bitmap Sheet(int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        bitmap.SetResolution(203, 203);
        return bitmap;
    }

    private static float WidthMm(Bitmap bitmap) => bitmap.Width / bitmap.HorizontalResolution * 25.4f;

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
            throw new InvalidOperationException($"Принтер {kind} {address} не отвечает.");
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
            throw new InvalidOperationException($"Принтер {kind} {address} не отвечает.");
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
