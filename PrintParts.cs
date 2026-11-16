namespace CargoPlus;

internal static class PrintParts
{
    public const string LabelNumber = "L.num";
    public const string LabelDate = "L.date";
    public const string LabelBarcode = "L.bar";
    public const string LabelStore = "L.store";
    public const string LabelMark = "L.mark";
    public const string LabelFirm = "L.firm";
    public const string LabelKilo = "L.kg";
    public const string LabelCode = "L.kod";

    public const string ReceiptFirm = "R.firm";
    public const string ReceiptBranch = "R.branch";
    public const string ReceiptCopy = "R.copy";
    public const string ReceiptKind = "R.kind";
    public const string ReceiptTrack = "R.track";
    public const string ReceiptDate = "R.date";
    public const string ReceiptWho = "R.who";
    public const string ReceiptCode = "R.code";
    public const string ReceiptName = "R.name";
    public const string ReceiptPhone = "R.phone";
    public const string ReceiptStore = "R.store";
    public const string ReceiptStorePhone = "R.storephone";
    public const string ReceiptTable = "R.table";
    public const string ReceiptTotal = "R.total";
    public const string ReceiptQr = "R.qr";
    public const string ReceiptNote = "R.note";

    public static readonly (string Key, Func<string> Title)[] LabelItems =
    [
        (LabelNumber, () => UiText.PartLabelNumber),
        (LabelDate, () => UiText.PartLabelDate),
        (LabelBarcode, () => UiText.PartLabelBarcode),
        (LabelStore, () => UiText.PartLabelStore),
        (LabelMark, () => UiText.PartLabelMark),
        (LabelFirm, () => UiText.PartLabelFirm),
        (LabelKilo, () => UiText.PartLabelKilo),
        (LabelCode, () => UiText.PartLabelCode),
    ];

    public static readonly (string Key, Func<string> Title)[] ReceiptItems =
    [
        (ReceiptFirm, () => UiText.PartReceiptFirm),
        (ReceiptBranch, () => UiText.PartReceiptBranch),
        (ReceiptCopy, () => UiText.PartReceiptCopy),
        (ReceiptKind, () => UiText.PartReceiptKind),
        (ReceiptTrack, () => UiText.PartReceiptTrack),
        (ReceiptDate, () => UiText.PartReceiptDate),
        (ReceiptWho, () => UiText.PartReceiptWho),
        (ReceiptCode, () => UiText.PartReceiptCode),
        (ReceiptName, () => UiText.PartReceiptName),
        (ReceiptPhone, () => UiText.PartReceiptPhone),
        (ReceiptStore, () => UiText.PartReceiptStore),
        (ReceiptTable, () => UiText.PartReceiptTable),
        (ReceiptTotal, () => UiText.PartReceiptTotal),
        (ReceiptQr, () => UiText.PartReceiptQr),
        (ReceiptNote, () => UiText.PartReceiptNote),
    ];

    public static bool Visible(AppSettings settings, string key)
    {
        var hide = settings.PrintHide;
        if (string.IsNullOrWhiteSpace(hide))
            return true;

        foreach (var part in hide.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part == key)
                return false;
        }

        return true;
    }

    public static float Scale(AppSettings settings, string key)
    {
        if (string.IsNullOrWhiteSpace(settings.PrintSizes))
            return 1f;

        foreach (var part in settings.PrintSizes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var cut = part.IndexOf('=');
            if (cut <= 0 || part[..cut] != key)
                continue;
            if (float.TryParse(part[(cut + 1)..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
                return Math.Clamp(value, 0.7f, 1.6f);
        }

        return 1f;
    }
}

internal static class OperatorRights
{
    public const string Firm = "firm";
    public const string Numbers = "numbers";
    public const string Operators = "operators";
    public const string Preview = "preview";

    public static bool Allows(AppSettings settings, string key)
    {
        var rights = settings.OperatorRights;
        if (string.IsNullOrWhiteSpace(rights))
            return false;

        foreach (var part in rights.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part == key)
                return true;
        }

        return false;
    }
}
