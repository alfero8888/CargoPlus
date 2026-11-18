using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CargoPlus;

internal static class AppLanguage
{
    public static bool Turkish { get; private set; } = true;

    public static void Use(AppSettings settings) =>
        Turkish = !string.Equals(settings.Language, "ru", StringComparison.OrdinalIgnoreCase);
}

internal sealed record BolumChoice(string Code, string Title, string TurkishTitle)
{
    public override string ToString() => AppLanguage.Turkish ? TurkishTitle : Title;

    public static readonly BolumChoice[] All =
    [
        new("1", "Авиа", "Avia"),
        new("2", "Тир", "Tır"),
        new("3", "Эманет", "Emanet"),
        new("4", "Пакет", "Paket"),
        new("6", "Играджат", "İhracat"),
        new("7", "Неизвестный", "Belirsiz"),
    ];

    public static string Letter(string code) => code switch
    {
        "1" => "A",
        "2" => "T",
        "3" => "E",
        "4" => "P",
        "6" => "I",
        "7" => "X",
        _ => "X",
    };

    public static string TitleOf(string code)
    {
        var item = All.FirstOrDefault(choice => choice.Code == code);
        if (item is null)
            return code;
        return AppLanguage.Turkish ? item.TurkishTitle : item.Title;
    }
}

internal static partial class CargoRules
{
    public const int LowBagWarning = 1000;
    public const int StoreNameMax = 50;
    public const int NoteMax = 100;

    public static bool IsPin(string pin) => pin.Length == 4 && pin.All(char.IsDigit);

    public static bool InsuranceLocked(string? bolum) => bolum is "2" or "4";

    public static string HashPin(string pin, out string salt)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        salt = Convert.ToHexString(saltBytes);
        return Hash(pin, saltBytes);
    }

    public static bool PinEquals(string pin, string saltHex, string hashHex)
    {
        byte[] actual;
        byte[] expected;
        try
        {
            actual = Convert.FromHexString(Hash(pin, Convert.FromHexString(saltHex)));
            expected = Convert.FromHexString(hashHex);
        }
        catch (FormatException)
        {
            return false;
        }

        return actual.Length == expected.Length && CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public static bool TryKilo(string input, out string normalized, out string error)
    {
        normalized = "";
        var text = input.Trim().Replace(',', '.');
        if (!KiloPattern().IsMatch(text))
        {
            error = UiText.KiloFormat;
            return false;
        }

        var parts = text.Split('.');
        var whole = parts[0].TrimStart('0');
        if (whole.Length == 0)
            whole = "0";
        normalized = parts.Length == 1 ? whole : whole + "." + parts[1];
        if (normalized.Length > 11)
        {
            error = UiText.KiloLong;
            return false;
        }

        var value = decimal.Parse(normalized, CultureInfo.InvariantCulture);
        if (value <= 0)
        {
            error = UiText.KiloZero;
            return false;
        }

        error = "";
        return true;
    }

    public static bool TryStore(string phone, string name, out string joined, out string error)
    {
        phone = Compact(phone);
        name = Compact(name);
        joined = "";
        if (name.Length == 0)
        {
            error = UiText.NeedStoreName;
            return false;
        }

        if (name.Length > StoreNameMax)
        {
            error = UiText.StoreTooLong(name.Length);
            return false;
        }

        joined = phone.Length == 0 ? name : phone + " " + name;

        error = "";
        return true;
    }

    public static bool TryInsurance(string? bolum, string insuranceText, string note, out int sigorta, out string not1, out string error)
    {
        sigorta = 0;
        not1 = "0";
        if (string.IsNullOrEmpty(bolum))
        {
            error = UiText.NeedType;
            return false;
        }

        if (!InsuranceLocked(bolum))
        {
            var text = insuranceText.Trim();
            if (text.Length == 0)
                sigorta = 0;
            else if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out sigorta) || sigorta < 0)
            {
                error = UiText.InsuranceNumber;
                return false;
            }
        }

        var reason = Compact(note);
        if (reason.Length > NoteMax)
        {
            error = UiText.NoteTooLong;
            return false;
        }

        if (reason.Length > 0 && reason != "0")
            not1 = reason;

        error = "";
        return true;
    }

    public static string WithOfflineNote(string not1, bool offline)
    {
        if (!offline)
            return not1;
        var mark = UiText.AcceptedOffline;
        var current = string.IsNullOrWhiteSpace(not1) || not1.Trim() == "0" ? "" : not1.Trim();
        if (current.Length == 0 || current.Contains(mark, StringComparison.Ordinal))
            return current.Length == 0 ? mark : current;
        var joined = current + " | " + mark;
        return joined.Length <= NoteMax ? joined : mark;
    }

    public static bool TryRange(int start, int end, int next, out string error)
    {
        if (start < 0 || end < 0 || next < 0)
        {
            error = UiText.RangeNegative;
            return false;
        }

        if (start < 1 || next < 1)
        {
            error = UiText.ZeroForbidden;
            return false;
        }

        if (end < start)
        {
            error = UiText.RangeEndSmall;
            return false;
        }

        if (next < start)
        {
            error = UiText.RangeNextSmall;
            return false;
        }

        if (next > end + 1)
        {
            error = UiText.RangeNextFar;
            return false;
        }

        error = "";
        return true;
    }

    public static string MaskPart(string value, int keep)
    {
        var text = value.Trim();
        if (text.Length <= keep)
            return text;
        return text[..keep] + new string('*', text.Length - keep);
    }

    public static string MaskPhone(string phone)
    {
        var text = phone.Trim();
        var keep = new bool[text.Length];
        var digits = 0;
        for (var i = text.Length - 1; i >= 0 && digits < 4; i--)
        {
            if (!char.IsDigit(text[i]))
                continue;
            keep[i] = true;
            digits++;
        }

        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (!keep[i])
                chars[i] = '*';
        }

        return new string(chars);
    }

    public static string TrackingLink(string track) =>
        "https://baraka-logistic.com/track.php?track=" + Uri.EscapeDataString(track ?? "");

    public static string Track(int kodId)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var head = kodId.ToString(CultureInfo.InvariantCulture);
        var tailLen = Math.Min(8, 20 - head.Length);
        if (tailLen <= 0)
            return head.Length <= 20 ? head : head[..20];

        var bytes = RandomNumberGenerator.GetBytes(tailLen);
        var tail = new char[tailLen];
        for (var i = 0; i < tailLen; i++)
            tail[i] = alphabet[bytes[i] % alphabet.Length];
        return head + new string(tail);
    }

    public static string Track(string kodId)
    {
        var text = (kodId ?? "").Trim().ToUpperInvariant();
        if (text.Length > 0
            && text.All(char.IsDigit)
            && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            && number > 0)
            return Track(number);

        var split = text.Length;
        while (split > 0 && char.IsDigit(text[split - 1]))
            split--;
        var prefix = text[..split];
        var digits = split == text.Length ? "0" : text[split..];
        var room = 20 - prefix.Length - digits.Length;
        var tailLen = room <= 0 ? 0 : Math.Min(8, room);
        var combined = prefix + digits + Tail(prefix + digits, tailLen);
        return combined.Length <= 20 ? combined : combined[..20];
    }

    public static string NumberPrefix(string? station, string? operatorName, string bolum)
    {
        var office = (station ?? "").Trim().ToUpperInvariant();
        if (office.Length is < 1 or > 2)
            office = FirstStationLetter(office);
        return office + FirstStationLetter(operatorName) + BolumChoice.Letter(bolum);
    }

    public static string FirstStationLetter(string? branchName)
    {
        foreach (var ch in (branchName ?? "").Trim())
        {
            var mapped = MapStation(ch);
            if (mapped is >= 'A' and <= 'Z')
                return mapped.ToString();
        }

        return "A";
    }

    public static bool TryStationLetter(string input, out string letter, out string error)
    {
        letter = "";
        error = "";
        var text = input.Trim();
        if (text.Length == 0)
            return true;
        if (text.Length is < 1 or > 2)
        {
            error = UiText.StationLetterBad;
            return false;
        }

        var chars = new char[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            var ch = MapStation(text[i]);
            if (ch is < 'A' or > 'Z')
            {
                error = UiText.StationLetterBad;
                return false;
            }

            chars[i] = ch;
        }

        letter = new string(chars);
        return true;
    }

    private static char MapStation(char ch)
    {
        if (ch is >= 'a' and <= 'z')
            return char.ToUpperInvariant(ch);
        return ch switch
        {
            'А' or 'а' => 'A',
            'Б' or 'б' => 'B',
            'В' or 'в' => 'B',
            'Г' or 'г' => 'G',
            'Д' or 'д' => 'D',
            'Е' or 'е' or 'Ё' or 'ё' => 'E',
            'Ж' or 'ж' => 'Z',
            'З' or 'з' => 'Z',
            'И' or 'и' => 'I',
            'Й' or 'й' => 'Y',
            'К' or 'к' => 'K',
            'Л' or 'л' => 'L',
            'М' or 'м' => 'M',
            'Н' or 'н' => 'H',
            'О' or 'о' => 'O',
            'П' or 'п' => 'P',
            'Р' or 'р' => 'P',
            'С' or 'с' => 'C',
            'Т' or 'т' => 'T',
            'У' or 'у' => 'U',
            'Ф' or 'ф' => 'F',
            'Х' or 'х' => 'H',
            'Ц' or 'ц' => 'C',
            'Ч' or 'ч' => 'C',
            'Ш' or 'ш' => 'S',
            'Щ' or 'щ' => 'S',
            'Ы' or 'ы' => 'Y',
            'Э' or 'э' => 'E',
            'Ю' or 'ю' => 'Y',
            'Я' or 'я' => 'Y',
            'İ' or 'I' or 'ı' => 'I',
            'Ş' or 'ş' => 'S',
            'Ğ' or 'ğ' => 'G',
            'Ö' or 'ö' => 'O',
            'Ü' or 'ü' => 'U',
            'Ç' or 'ç' => 'C',
            _ => char.ToUpperInvariant(ch),
        };
    }

    private static string Tail(string seed, int length)
    {
        if (length <= 0)
            return "";
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(seed));
        var tail = new char[length];
        for (var i = 0; i < length; i++)
            tail[i] = alphabet[hash[i % hash.Length] % alphabet.Length];
        return new string(tail);
    }

    public const int ItemNameMax = 120;
    public const int InvoiceNoMax = 40;
    public const int ItemLineMax = 50;

    public static bool NeedsContents(string? bolum) => bolum is "2" or "6";

    public static string ItemUnit(string? unit) => unit?.Trim().ToLowerInvariant() switch
    {
        "kg" => "kg",
        "litre" or "lt" or "l" => "litre",
        "mt" or "m" or "metre" or "meter" or "метр" => "mt",
        "cift" or "çift" or "cft" or "çft" or "pair" or "пара" => "cift",
        _ => "adet",
    };

    public static bool TryItemQty(string text, out string qty, out string error)
    {
        qty = "";
        var normalized = text.Trim().Replace(',', '.');
        var parts = normalized.Split('.');
        if (parts.Length > 2
            || (parts.Length == 2 && parts[1].Length > 3)
            || !decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            || value <= 0)
        {
            error = UiText.ItemQtyBad;
            return false;
        }

        qty = value.ToString("0.###", CultureInfo.InvariantCulture);
        error = "";
        return true;
    }

    public static bool TryInvoice(string number, string date, out string invoiceNo, out string invoiceDate, out string error)
    {
        invoiceNo = Compact(number);
        invoiceDate = "";
        if (invoiceNo.Length > InvoiceNoMax)
        {
            error = UiText.InvoiceNoLong;
            return false;
        }

        var text = date.Trim();
        if (text.Length == 0)
        {
            error = "";
            return true;
        }

        string[] formats = ["dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "yyyy-MM-dd"];
        if (!DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            error = UiText.InvoiceDateBad;
            return false;
        }

        invoiceDate = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        error = "";
        return true;
    }

    public static string InvoiceDateText(string stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return "";
        return DateTime.TryParseExact(stored.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
            : stored.Trim();
    }

    public static string ContentsText(IReadOnlyList<BagItem> items) =>
        string.Join("; ", items.Select(item => $"{item.Name} {item.Qty} {UiText.UnitLabel(item.Unit)}"));

    public static string ContentsBrief(IReadOnlyList<BagItem> items)
    {
        var text = ContentsText(items);
        return text.Length <= 60 ? text : text[..57] + "...";
    }

    public static string FormatWeight(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    public static decimal ParseKilo(string kilo) =>
        decimal.Parse(kilo.Replace(',', '.'), CultureInfo.InvariantCulture);

    private static string Hash(string pin, byte[] salt)
    {
        var bytes = Rfc2898DeriveBytes.Pbkdf2(pin, salt, 50_000, HashAlgorithmName.SHA256, 32);
        return Convert.ToHexString(bytes);
    }

    private static string Compact(string value)
    {
        var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts);
    }

    [GeneratedRegex(@"^\d{1,8}(\.\d{1,2})?$")]
    private static partial Regex KiloPattern();
}
