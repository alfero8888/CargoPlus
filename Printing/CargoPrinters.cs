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
    private const float LabelWidthMm = 100f;
    private const float LabelHeightMm = 72f;
    private const float LabelStripeMm = 10f;
    private const float LabelDpi = 203f;

    private static int LabelDots(float millimeters) => (int)Math.Round(millimeters / 25.4f * LabelDpi);

    private static int LabelHundredths(float millimeters) => (int)Math.Round(millimeters / 25.4f * 100f);
    public static void PrintLabel(AppSettings settings, SessionInfo session, BagRecord bag)
    {
        if (PrinterAddress.TryParse(settings.LabelPrinter, out var address))
        {
            PrintLabelRaw(address, settings, session, bag);
            return;
        }

        using var document = Create(settings.LabelPrinter, UiText.NoLabelPrinter);
        document.DocumentName = "Cargo Plus " + bag.KodId;
        var paper = new PaperSize("CargoPlusLabel", LabelHundredths(LabelWidthMm), LabelHundredths(LabelHeightMm));
        document.DefaultPageSettings.PaperSize = paper;
        document.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        document.PrinterSettings.DefaultPageSettings.PaperSize = paper;
        document.PrintPage += (_, e) => DrawLabel(e, settings, session, bag);
        document.Print();
    }

    public static void PrintReceipts(AppSettings settings, SessionInfo session, IReadOnlyList<BagRecord> bags, bool includeFirmCopy = true, bool includeStoreCopy = true)
    {
        if (!includeStoreCopy && !includeFirmCopy)
            return;
        if (PrinterAddress.TryParse(settings.ReceiptPrinter, out var address))
        {
            PrintReceiptsRaw(address, settings, session, bags, includeFirmCopy, includeStoreCopy);
            return;
        }

        if (includeStoreCopy)
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
        _ = width;
        var words = PrintWords.From(settings);
        var bagNumber = bag.KodId;
        var code = bag.Kod.ToString(CultureInfo.InvariantCulture);
        var storeName = string.IsNullOrWhiteSpace(bag.StoreName) ? session.StoreName.Trim() : bag.StoreName.Trim();
        var company = (string.IsNullOrWhiteSpace(settings.Company) ? "BARAKA LOGISTIC" : settings.Company.Trim())
            .ToUpper(CultureInfo.GetCultureInfo("tr-TR"));
        if (company.EndsWith(" CARGO", StringComparison.Ordinal))
            company = company[..^6].TrimEnd();
        const float edge = LabelStripeMm + 3.6f;
        var contentLeft = edge;
        var contentWidth = LabelWidthMm - edge * 2f;
        var show = (string key) => PrintParts.Visible(settings, key);
        float Part(string key, float size) => size * PrintParts.Scale(settings, key);
        var footerParts = new List<string>();
        if (show(PrintParts.LabelStore) && storeName.Length > 0)
            footerParts.Add(storeName);
        if (show(PrintParts.LabelDate))
            footerParts.Add(bag.Tarih.ToString("dd.MM.yyyy"));
        var footer = string.Join("   ", footerParts);
        const float footerBand = 6.4f;
        var limit = LabelHeightMm - 2f - (footer.Length > 0 ? footerBand + 0.8f : 0f);
        using var ink = new SolidBrush(Color.Black);
        using var paper = new SolidBrush(Color.White);
        using var center = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
        };

        var y = 2f;
        if (show(PrintParts.LabelNumber))
        {
            const float numberHeight = 12f;
            var numberBox = new RectangleF(contentLeft, y, contentWidth, numberHeight);
            using var numberFont = FitBox(graphics, bagNumber, Part(PrintParts.LabelNumber, 33.2f), contentWidth - 1.2f, numberHeight);
            graphics.DrawString(bagNumber, numberFont, ink, numberBox, center);
            graphics.DrawString(bagNumber, numberFont, ink, new RectangleF(numberBox.X + 0.25f, numberBox.Y, numberBox.Width, numberBox.Height), center);
            graphics.DrawString(bagNumber, numberFont, ink, new RectangleF(numberBox.X + 0.12f, numberBox.Y + 0.1f, numberBox.Width, numberBox.Height), center);
            y += numberHeight + 0.3f;
        }

        var showPanel = show(PrintParts.LabelMark) || show(PrintParts.LabelFirm) || show(PrintParts.LabelKilo) || show(PrintParts.LabelCode);
        const float baseBarcode = 12.5f;
        const float barcodeGap = 0.9f;
        var naturalPanel = showPanel ? Math.Min(36f, limit - y - (baseBarcode + barcodeGap) - 1.2f) : 0f;
        var headGuess = Math.Min(5.2f, Math.Max(0f, naturalPanel) * 0.2f);
        var boxGuess = Math.Max(0f, naturalPanel - headGuess - 3f);
        var saved = boxGuess * 0.3f;
        if (show(PrintParts.LabelBarcode))
        {
            var barcodeWidth = contentWidth - 2f;
            var barcodeLeft = contentLeft + (contentWidth - barcodeWidth) / 2f;
            var barcodeHeight = baseBarcode + (showPanel ? saved : 0f);
            Code128.Draw(graphics, new RectangleF(barcodeLeft, y, barcodeWidth, barcodeHeight), bagNumber);
            y += barcodeHeight + barcodeGap;
        }

        if (showPanel && naturalPanel > 8f)
        {
            var panelHeight = naturalPanel - saved;
            var panel = new RectangleF(contentLeft, y + 0.6f, contentWidth, panelHeight);
            graphics.FillRectangle(ink, panel);
            var headHeight = Math.Min(5.2f, panel.Height * 0.2f);
            var head = new RectangleF(panel.X + 1f, panel.Y + 1f, panel.Width - 2f, headHeight);
            if (show(PrintParts.LabelMark))
            {
                var markBox = new RectangleF(head.X, head.Y, 16f, head.Height);
                using var markFont = FitLabel(graphics, words.BolumMark(bag.Bolum), Part(PrintParts.LabelMark, 22f), markBox.Width - 1f);
                graphics.DrawString(words.BolumMark(bag.Bolum), markFont, paper, markBox, center);
            }
            if (show(PrintParts.LabelFirm))
            {
                var nameBox = new RectangleF(head.X + 16f, head.Y, head.Width - 16f, head.Height);
                using var nameFont = FitLabel(graphics, company, Part(PrintParts.LabelFirm, 12f), nameBox.Width - 1f);
                graphics.DrawString(company, nameFont, paper, nameBox, center);
            }

            var gap = 1f;
            var boxTop = head.Bottom + gap;
            var boxHeight = panel.Bottom - boxTop - gap;
            var showKilo = show(PrintParts.LabelKilo);
            var showCode = show(PrintParts.LabelCode);
            var inner = panel.Width - gap * 3f;
            var kiloWidth = showKilo && showCode ? inner * 0.36f : inner;
            var codeWidth = showKilo && showCode ? inner - kiloWidth : inner;
            if (showKilo)
                DrawValueBox(graphics, new RectangleF(panel.X + gap, boxTop, kiloWidth, boxHeight), "KG", bag.Kilo, ink, paper, center, Part(PrintParts.LabelKilo, 28f));
            if (showCode)
            {
                var codeMax = code.Length switch
                {
                    <= 4 => 56f,
                    <= 6 => 48f,
                    <= 8 => 40f,
                    _ => 32f,
                };
                var codeLeft = panel.X + gap + (showKilo ? kiloWidth + gap : 0f);
                DrawValueBox(graphics, new RectangleF(codeLeft, boxTop, codeWidth, boxHeight), "KOD", code, ink, paper, center, Part(PrintParts.LabelCode, codeMax), thick: true);
            }

            y = panel.Bottom + 0.8f;
        }

        if (footer.Length > 0)
        {
            using var footerFont = FitBox(graphics, footer, Part(PrintParts.LabelStore, 11f), contentWidth - 1f, footerBand - 0.4f, 6f);
            graphics.DrawString(footer, footerFont, ink, new RectangleF(contentLeft, y, contentWidth, footerBand), center);
        }
    }

    private static void DrawValueBox(Graphics graphics, RectangleF box, string caption, string value, Brush ink, Brush paper, StringFormat center, float maxPoint, bool thick = false)
    {
        graphics.FillRectangle(paper, box);
        using var captionFont = new Font("Arial", 6f, FontStyle.Bold, GraphicsUnit.Point);
        var valueBox = new RectangleF(box.X + 0.4f, box.Y + 2.2f, box.Width - 0.8f, Math.Max(4f, box.Height - 2.5f));
        var fitWidth = thick ? valueBox.Width - 0.5f : valueBox.Width;
        using var valueFont = FitBox(graphics, value, maxPoint, fitWidth, valueBox.Height);
        graphics.DrawString(caption, captionFont, ink, new RectangleF(box.X, box.Y + 0.05f, box.Width, 2.15f), center);
        graphics.DrawString(value, valueFont, ink, valueBox, center);
        if (thick)
        {
            graphics.DrawString(value, valueFont, ink, new RectangleF(valueBox.X + 0.32f, valueBox.Y, valueBox.Width, valueBox.Height), center);
            graphics.DrawString(value, valueFont, ink, new RectangleF(valueBox.X + 0.16f, valueBox.Y + 0.12f, valueBox.Width, valueBox.Height), center);
        }
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
        var scale = settings.ReceiptScale is < 0.75f or > 1.45f ? 1f : settings.ReceiptScale;
        float Pt(float size) => Math.Clamp(size * scale, 6f, 42f);
        float Part(string key, float size) => Pt(size * PrintParts.Scale(settings, key));
        var margin = 3f;
        var x = margin;
        var y = margin;
        var contentWidth = Math.Max(30, Math.Min(width - margin * 2, 74));
        using var ink = new SolidBrush(Color.Black);
        using var title = new Font("Segoe UI", Pt(11f), FontStyle.Bold);
        using var head = new Font("Segoe UI", Pt(11f), FontStyle.Bold);
        using var body = new Font("Segoe UI", Pt(8f));
        using var bagFont = new Font("Segoe UI", Pt(13f), FontStyle.Bold);
        using var center = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        using var line = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        using var wrap = new StringFormat { Trimming = StringTrimming.Word };
        using var pen = new Pen(Color.Black, 0.35f);

        y = DrawBrand(graphics, settings, x, y, contentWidth, 14f, title, body, ink, center);
        var bolum = bags.Count > 0 ? bags[0].Bolum : "";
        if (bolum.Length > 0 && PrintParts.Visible(settings, PrintParts.ReceiptKind))
        {
            DrawLine(graphics, body, ink, center, ref y, contentWidth, x, words.ShipmentKind, 4.2f);
            using var banner = FitFont(graphics, words.BolumBanner(bolum), Pt(26f), contentWidth);
            var bannerHeight = Math.Max(8f, graphics.MeasureString(words.BolumBanner(bolum), banner, new SizeF(contentWidth, 40f)).Height);
            graphics.DrawString(words.BolumBanner(bolum), banner, ink, new RectangleF(x, y, contentWidth, bannerHeight + 1f), center);
            y += bannerHeight + 1.2f;
        }

        if (PrintParts.Visible(settings, PrintParts.ReceiptCopy))
        {
            var copyFont = storeCopy
                ? new Font("Segoe UI", Pt(16f), FontStyle.Bold)
                : new Font("Segoe UI", Pt(11f), FontStyle.Bold);
            using (copyFont)
            {
                var copy = storeCopy ? words.CopyStore : words.CopyFirm;
                var copyHeight = Math.Max(storeCopy ? 7f : 5f, graphics.MeasureString(copy, copyFont, new SizeF(contentWidth, 30f)).Height);
                graphics.DrawString(copy, copyFont, ink, new RectangleF(x, y, contentWidth, copyHeight + 1f), center);
                y += copyHeight + 1.4f;
            }
        }

        if (PrintParts.Visible(settings, PrintParts.ReceiptTrack) && !string.IsNullOrWhiteSpace(session.TrackCode))
            DrawLine(graphics, head, ink, center, ref y, contentWidth, x, $"{words.Track}: {session.TrackCode}", 5.4f);

        var moment = bags.Count > 0 ? bags[0].Tarih : DateTime.Now;
        if (PrintParts.Visible(settings, PrintParts.ReceiptDate))
            DrawLine(graphics, body, ink, line, ref y, contentWidth, x, moment.ToString("dd.MM.yyyy HH:mm"), 4.4f);
        if (PrintParts.Visible(settings, PrintParts.ReceiptWho))
            DrawLine(graphics, body, ink, line, ref y, contentWidth, x, $"{words.Accepted}: {session.OperatorName}", 4.4f);
        if (PrintParts.Visible(settings, PrintParts.ReceiptCode))
        {
            using var customerHead = new Font("Segoe UI", Part(PrintParts.ReceiptCode, 13f), FontStyle.Bold);
            DrawLine(graphics, customerHead, ink, center, ref y, contentWidth, x, words.CustomerBlock, 6.2f);
            DrawLine(graphics, customerHead, ink, center, ref y, contentWidth, x, $"{words.CustomerCode}: {session.Kod}", 6.2f);
        }
        if (PrintParts.Visible(settings, PrintParts.ReceiptName))
        {
            using var nameFont = new Font("Segoe UI", Part(PrintParts.ReceiptName, 8f));
            var person = $"{CargoRules.MaskPart(session.ClientName, 3)} {CargoRules.MaskPart(session.ClientSurname, 3)}".Trim();
            DrawLine(graphics, nameFont, ink, line, ref y, contentWidth, x, $"{words.Person}: {person}", 4.4f);
        }
        if (PrintParts.Visible(settings, PrintParts.ReceiptPhone) && !string.IsNullOrWhiteSpace(session.ClientPhone))
        {
            using var phoneFont = new Font("Segoe UI", Part(PrintParts.ReceiptPhone, 8f));
            DrawLine(graphics, phoneFont, ink, line, ref y, contentWidth, x, $"{words.Phone}: {LastFour(session.ClientPhone)}", 4.4f);
        }
        y += 1.2f;

        decimal kilos = 0;
        foreach (var bag in bags)
            kilos += CargoRules.ParseKilo(bag.Kilo);

        if (PrintParts.Visible(settings, PrintParts.ReceiptTable))
        {
            var colKilo = x + contentWidth * 0.56f;
            graphics.DrawLine(pen, x, y, x + contentWidth, y);
            y += 0.8f;
            graphics.DrawString(words.Bag, head, ink, x, y);
            graphics.DrawString(words.Kilo, head, ink, colKilo, y);
            y += 5.6f;
            graphics.DrawLine(pen, x, y, x + contentWidth, y);

            foreach (var bag in bags)
            {
                y += 0.7f;
                var number = bag.KodId.ToString(CultureInfo.InvariantCulture);
                graphics.DrawString(number, bagFont, ink, new RectangleF(x, y, colKilo - x - 1, 6.4f), line);
                graphics.DrawString(bag.Kilo, bagFont, ink, new RectangleF(colKilo, y, x + contentWidth - colKilo, 6.4f), line);
                y += 6.4f;
                if (CargoRules.NeedsContents(bag.Bolum) && bag.Items.Count > 0)
                {
                    using var itemFont = new Font("Segoe UI", Pt(6.5f));
                    var text = CargoRules.ContentsText(bag.Items);
                    var size = graphics.MeasureString(text, itemFont, new SizeF(contentWidth, 30f));
                    graphics.DrawString(text, itemFont, ink, new RectangleF(x, y, contentWidth, size.Height + 0.3f), wrap);
                    y += size.Height + 0.4f;
                }
            }

            graphics.DrawLine(pen, x, y, x + contentWidth, y);
            y += 1.4f;
        }

        if (PrintParts.Visible(settings, PrintParts.ReceiptTotal))
        {
            DrawLine(graphics, head, ink, line, ref y, contentWidth, x, $"{words.TotalBags}: {bags.Count}", 5.2f);
            DrawLine(graphics, head, ink, line, ref y, contentWidth, x, $"{words.TotalKilo}: {CargoRules.FormatWeight(kilos)}", 5.2f);
        }
        if (PrintParts.Visible(settings, PrintParts.ReceiptStore))
        {
            var storeLine = string.Join("   ", new[] { session.StoreName, session.StorePhone }.Where(part => !string.IsNullOrWhiteSpace(part)));
            if (storeLine.Length > 0)
            {
                y += 1.6f;
                using var storeFont = new Font("Segoe UI", Part(PrintParts.ReceiptStore, 8f), FontStyle.Bold);
                DrawLine(graphics, storeFont, ink, center, ref y, contentWidth, x, storeLine, 4.8f);
            }
        }
        if (storeCopy && PrintParts.Visible(settings, PrintParts.ReceiptQr) && !string.IsNullOrWhiteSpace(session.TrackCode))
        {
            y += 2f;
            var qr = Math.Min(64f, contentWidth) * 0.7f;
            QrMark.Draw(graphics, new RectangleF(x + (contentWidth - qr) / 2f, y, qr, qr), CargoRules.TrackingLink(session.TrackCode));
            y += qr;
        }

        if (!storeCopy && PrintParts.Visible(settings, PrintParts.ReceiptNote))
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
        var showCompany = PrintParts.Visible(settings, PrintParts.ReceiptFirm);
        var showBranch = PrintParts.Visible(settings, PrintParts.ReceiptBranch) && !string.IsNullOrWhiteSpace(settings.Branch);
        var company = string.IsNullOrWhiteSpace(settings.Company) ? "CARGO PLUS" : settings.Company.Trim();
        using var logo = showCompany ? BrandLogo.TryLoad(settings.LogoPath) : null;
        if (!showCompany && !showBranch)
            return y;

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

        var textBlock = 0f;
        if (showCompany)
        {
            graphics.DrawString(company, title, ink, new RectangleF(textX, y, textWidth, 5.6f), center);
            textBlock = 5.2f;
        }
        if (showBranch)
        {
            graphics.DrawString(settings.Branch.Trim(), body, ink, new RectangleF(textX, y + textBlock, textWidth, 4.4f), center);
            textBlock += 4.4f;
        }

        return y + Math.Max(block, textBlock) + 1.2f;
    }

    private static Font FitLabel(Graphics graphics, string text, float maxPoint, float width) =>
        FitBox(graphics, text, maxPoint, width, float.MaxValue);

    private static Font FitBox(Graphics graphics, string text, float maxPoint, float width, float height, float minPoint = 8f)
    {
        var min = Math.Min(minPoint, maxPoint);
        for (var size = maxPoint; size >= min; size -= 0.5f)
        {
            var font = new Font("Arial", size, FontStyle.Bold, GraphicsUnit.Point);
            var measured = graphics.MeasureString(text, font);
            if (measured.Width <= width && measured.Height <= height)
                return font;
            font.Dispose();
        }

        return new Font("Arial", min, FontStyle.Bold, GraphicsUnit.Point);
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

    private static string LastFour(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length <= 4 ? digits : digits[^4..];
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
        using var bitmap = Sheet(LabelDots(LabelWidthMm), LabelDots(LabelHeightMm));
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.White);
            Prepare(graphics);
            PaintLabel(graphics, WidthMm(bitmap), settings, session, bag);
        }

        Send(address, Argox(bitmap), UiText.KindLabels);
    }

    private static void PrintReceiptsRaw(IPAddress address, AppSettings settings, SessionInfo session, IReadOnlyList<BagRecord> bags, bool includeFirmCopy, bool includeStoreCopy)
    {
        var copies = new List<bool>();
        if (includeStoreCopy)
            copies.Add(true);
        if (includeFirmCopy)
            copies.Add(false);
        if (copies.Count == 0)
            return;
        var payload = new List<byte> { 0x1B, 0x40 };
        foreach (var storeCopy in copies)
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

    public static Bitmap PreviewLabel(AppSettings settings)
    {
        var sample = Sample(settings);
        var bitmap = Sheet(LabelDots(LabelWidthMm), LabelDots(LabelHeightMm));
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        Prepare(graphics);
        PaintLabel(graphics, WidthMm(bitmap), settings, sample.Session, sample.Bag);
        return bitmap;
    }

    public static Bitmap PreviewReceipt(AppSettings settings, bool storeCopy)
    {
        var sample = Sample(settings);
        var bags = new[] { sample.Bag, sample.Second };
        var tall = Sheet(576, 4800);
        float used;
        using (var graphics = Graphics.FromImage(tall))
        {
            graphics.Clear(Color.White);
            Prepare(graphics);
            used = PaintReceipt(graphics, WidthMm(tall), settings, sample.Session, bags, storeCopy);
        }

        var height = (int)Math.Ceiling(used / 25.4f * tall.HorizontalResolution) + 12;
        var cropped = CropTop(tall, height);
        tall.Dispose();
        return cropped;
    }

    private static (SessionInfo Session, BagRecord Bag, BagRecord Second) Sample(AppSettings settings)
    {
        var station = (settings.StationLetter ?? "").Trim().ToUpperInvariant();
        if (station.Length is < 1 or > 2)
            station = "B";
        var session = new SessionInfo
        {
            Id = "preview",
            Kod = 123456,
            ClientName = "Ahmet",
            ClientSurname = "Yilmaz",
            ClientPhone = "05551234567",
            StorePhone = "05520009009",
            StoreName = "Test Market",
            OperatorName = "Operator",
            TrackCode = station + "OA11CND7CK",
        };
        return (session, SampleBag(session, station + "OA11", "12.5", "0"), SampleBag(session, station + "OA12", "8.0", "not"));
    }

    private static BagRecord SampleBag(SessionInfo session, string kodId, string kilo, string note) => new()
    {
        LocalId = 1,
        SessionId = session.Id,
        KodId = kodId,
        Kod = session.Kod,
        Kilo = kilo,
        TeslimEden = session.StorePhone + " " + session.StoreName,
        Sigorta = 10,
        Not1 = note,
        Tarih = new DateTime(2026, 11, 8, 14, 32, 0),
        TrackCode = session.TrackCode,
        Bolum = "1",
        StorePhone = session.StorePhone,
        StoreName = session.StoreName,
        OperatorName = session.OperatorName,
        Synced = false,
    };

    private static void DrawLine(Graphics graphics, Font font, Brush brush, StringFormat format, ref float y, float width, float x, string text, float step = 5.2f)
    {
        var measured = graphics.MeasureString(text, font, new SizeF(width, 80f));
        var height = Math.Max(step, measured.Height + 0.4f);
        graphics.DrawString(text, font, brush, new RectangleF(x, y, width, height), format);
        y += height;
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
