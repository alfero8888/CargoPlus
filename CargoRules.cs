using System.Globalization;
using System.Security.Cryptography;
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
    ];

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
    public const int TeslimMax = 25;
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
        var text = input.Trim().Replace('.', ',');
        if (!KiloPattern().IsMatch(text))
        {
            error = UiText.KiloFormat;
            return false;
        }

        var parts = text.Split(',');
        var whole = parts[0].TrimStart('0');
        if (whole.Length == 0)
            whole = "0";
        normalized = parts.Length == 1 ? whole : whole + "," + parts[1];
        if (normalized.Length > 11)
        {
            error = UiText.KiloLong;
            return false;
        }

        var value = decimal.Parse(normalized.Replace(',', '.'), CultureInfo.InvariantCulture);
        if (value <= 0)
        {
            error = UiText.KiloZero;
            return false;
        }

        error = "";
        return true;
    }

    public static int StoreLength(string phone, string name)
    {
        phone = Compact(phone);
        name = Compact(name);
        if (phone.Length == 0 && name.Length == 0)
            return 0;
        if (phone.Length == 0 || name.Length == 0)
            return phone.Length + name.Length;
        return phone.Length + 1 + name.Length;
    }

    public static bool TryStore(string phone, string name, out string joined, out string error)
    {
        phone = Compact(phone);
        name = Compact(name);
        joined = "";
        if (phone.Length == 0)
        {
            error = UiText.NeedStorePhone;
            return false;
        }

        if (name.Length == 0)
        {
            error = UiText.NeedStoreName;
            return false;
        }

        joined = phone + " " + name;
        if (joined.Length > TeslimMax)
        {
            error = UiText.StoreTooLong(joined.Length);
            return false;
        }

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

    public static bool TryRange(int start, int end, int next, out string error)
    {
        if (start < 0 || end < 0 || next < 0)
        {
            error = UiText.RangeNegative;
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

    public static string FormatWeight(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');

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

    [GeneratedRegex(@"^\d{1,8}(,\d{1,2})?$")]
    private static partial Regex KiloPattern();
}
