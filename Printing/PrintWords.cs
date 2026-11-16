namespace CargoPlus;

internal sealed class PrintWords
{
    public required bool Turkish { get; init; }

    public string CopyStore => Turkish ? "MAĞAZA NÜSHASI" : "ЭКЗЕМПЛЯР МАГАЗИНА";
    public string CopyFirm => Turkish ? "FİRMA NÜSHASI" : "ЭКЗЕМПЛЯР ФИРМЫ";
    public string Client => Turkish ? "Müşteri" : "Код";
    public string Department => Turkish ? "Bölüm" : "Отдел";
    public string Kilo => Turkish ? "Kilo" : "Кг";
    public string Person => "Müşteri Adı";
    public string Phone => "Müşteri Telefon";
    public string CustomerBlock => "MÜŞTERİ BİLGİLERİ";
    public string CustomerCode => "MÜŞTERİ KODU";
    public string Store => Turkish ? "Mağaza" : "Магазин";
    public string Accepted => Turkish ? "Kabul" : "Принял";
    public string Track => Turkish ? "Takip" : "Трекинг";
    public string Bag => Turkish ? "Çuval" : "Мешок";
    public string TotalBags => Turkish ? "Toplam çuval" : "Итого мешков";
    public string TotalKilo => Turkish ? "Toplam kilo" : "Итого кг";
    public string ShipmentKind => Turkish ? "GÖNDERİ TÜRÜ" : "ТИП ОТПРАВКИ";
    public string Note => Turkish ? "NOT" : "ЗАМЕТКА";

    public string BolumMark(string code) => code switch
    {
        "1" => "A",
        "2" => "T",
        "3" => "E",
        "4" => "P",
        "6" => "I",
        "7" => "X",
        _ => code,
    };

    public string BolumBanner(string code) => code switch
    {
        "1" => Turkish ? "AVIA" : "АВИА",
        "2" => Turkish ? "TIR" : "ТИР",
        "3" => Turkish ? "EMANET" : "ЭМАНЕТ",
        "4" => Turkish ? "PAKET" : "ПАКЕТ",
        "6" => Turkish ? "İHRACAT" : "ИГРАДЖАТ",
        "7" => Turkish ? "BELIRSIZ" : "НЕИЗВЕСТНЫЙ",
        _ => code,
    };

    public string Bolum(string code) => code switch
    {
        "1" => Turkish ? "Avia" : "Авиа",
        "2" => Turkish ? "Tır" : "Тир",
        "3" => Turkish ? "Emanet" : "Эманет",
        "4" => Turkish ? "Paket" : "Пакет",
        "6" => Turkish ? "İhracat" : "Играджат",
        "7" => Turkish ? "Belirsiz" : "Неизвестный",
        _ => code,
    };

    public static PrintWords From(AppSettings settings) =>
        new() { Turkish = !string.Equals(settings.Language, "ru", StringComparison.OrdinalIgnoreCase) };
}
