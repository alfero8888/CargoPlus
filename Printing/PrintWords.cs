namespace CargoPlus;

internal sealed class PrintWords
{
    public required bool Turkish { get; init; }

    public string CopyStore => Turkish ? "MAĞAZA NÜSHASI" : "ЭКЗЕМПЛЯР МАГАЗИНА";
    public string CopyFirm => Turkish ? "FİRMA NÜSHASI" : "ЭКЗЕМПЛЯР ФИРМЫ";
    public string Client => Turkish ? "Müşteri" : "Код";
    public string Department => Turkish ? "Bölüm" : "Отдел";
    public string Kilo => Turkish ? "Kilo" : "Кг";
    public string Person => Turkish ? "Ad" : "Имя";
    public string Phone => Turkish ? "Tel" : "Тел";
    public string Store => Turkish ? "Mağaza" : "Магазин";
    public string StorePhone => Turkish ? "Mağaza tel" : "Тел. магазина";
    public string Accepted => Turkish ? "Kabul" : "Принял";
    public string Track => Turkish ? "Takip" : "Трекинг";
    public string Bag => Turkish ? "Çuval" : "Мешок";
    public string TotalBags => Turkish ? "Toplam çuval" : "Итого мешков";
    public string TotalKilo => Turkish ? "Toplam kilo" : "Итого кг";

    public string Bolum(string code) => code switch
    {
        "1" => Turkish ? "Avia" : "Авиа",
        "2" => Turkish ? "Tır" : "Тир",
        "3" => Turkish ? "Emanet" : "Эманет",
        "4" => Turkish ? "Paket" : "Пакет",
        "6" => Turkish ? "İhracat" : "Играджат",
        _ => code,
    };

    public static PrintWords From(AppSettings settings) =>
        new() { Turkish = !string.Equals(settings.Language, "ru", StringComparison.OrdinalIgnoreCase) };
}
