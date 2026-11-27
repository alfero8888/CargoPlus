using System.Text.Json.Nodes;

namespace CargoPlus;

/// <summary>
/// Чек приёма клиенту: одна запись через API (сайт сам отправит Telegram / e-posta на трёх языках сразу после ответа).
/// Каналы клиента — для ручной кнопки «Fişi müşteriye gönder».
/// </summary>
internal sealed partial class RemoteDb
{
    public async Task WriteNoticeAsync(AppSettings settings, ReceiptNotice notice, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject
        {
            ["n"] = new JsonObject
            {
                ["kod"] = notice.Kod,
                ["session"] = notice.SessionId,
                ["bags"] = notice.Bags,
                ["n"] = notice.Count,
                ["kg"] = notice.Kilo,
                ["store"] = notice.Store,
                ["phone"] = notice.StorePhone,
                ["track"] = notice.Track,
                ["office"] = notice.Office,
                ["operator"] = notice.Operator,
                ["ch"] = notice.Channel,
                ["manual"] = notice.Manual,
                ["receipt"] = notice.Receipt.Length > 0 ? Convert.ToBase64String(notice.Receipt) : "",
                ["qr"] = notice.Qr.Length > 0 ? Convert.ToBase64String(notice.Qr) : "",
                ["created"] = Stamp(notice.Created),
            },
        };
        await CallAsync(settings, "notice", body, cancellationToken);
    }

    /// <summary>Есть ли у клиента подтверждённый личный Telegram и подтверждённая e-posta.</summary>
    public async Task<(bool Telegram, bool Mail)> ClientChannelsAsync(AppSettings settings, uint kod, CancellationToken cancellationToken = default)
    {
        var json = await CallAsync(settings, "channels", new JsonObject { ["kod"] = kod }, cancellationToken);
        return (json["tg"]?.GetValue<bool>() == true, json["mail"]?.GetValue<bool>() == true);
    }
}
