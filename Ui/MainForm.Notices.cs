namespace CargoPlus;

/// <summary>
/// Чек приёма клиенту (Telegram / e-posta, тексты TR / UZ / RU делает сайт):
///  • автоматически — после печати чека в «Bitir ve fiş yazdır» (один раз на приём);
///  • вручную — кнопка «Fişi müşteriye gönder» в отчётах (по выбранному чеку, выбранный канал).
/// Всё через локальную очередь: без интернета чек ждёт и уходит, когда мешки дойдут до сервера.
/// </summary>
internal sealed partial class MainForm
{
    /// <summary>Поставить чек клиенту в очередь (после печати). Ошибка не мешает приёму.</summary>
    private void QueueClientReceipt(SessionInfo session, IReadOnlyList<BagRecord> bags, string channel, bool manual)
    {
        var settings = _store.GetSettings();
        var receipt = CargoPrinters.ReceiptPng(settings, session, bags);
        var track = string.IsNullOrWhiteSpace(session.TrackCode) ? bags[0].TrackCode : session.TrackCode;
        var qr = CargoPrinters.TrackQrPng(track);
        _store.QueueNotice(session, bags, channel, manual, receipt, qr);
        _sync.Kick();
    }

    private void TryQueueClientReceipt(SessionInfo session, IReadOnlyList<BagRecord> bags)
    {
        try
        {
            QueueClientReceipt(session, bags, "all", manual: false);
        }
        catch (Exception)
        {
        }
    }

    /// <summary>Отчёты → «Fişi müşteriye gönder».</summary>
    private async void SendReceiptToClient()
    {
        if (_reportGrid.CurrentRow?.Tag is not BagRecord picked)
        {
            MessageBox.Show(this, UiText.ReportPick, UiText.SendReceiptTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var bags = _store.GetBags(picked.SessionId);
        if (bags.Count == 0)
            bags = [picked];
        var first = bags[0];
        var session = _store.GetSession(first.SessionId) ?? new SessionInfo
        {
            Id = first.SessionId,
            Kod = first.Kod,
            ClientName = "",
            ClientSurname = "",
            ClientPhone = "",
            StorePhone = first.StorePhone,
            StoreName = first.StoreName,
            OperatorName = first.OperatorName,
            TrackCode = first.TrackCode,
        };

        bool telegram, mail;
        UseWaitCursor = true;
        try
        {
            (telegram, mail) = await _remote.ClientChannelsAsync(_store.GetSettings(), first.Kod);
        }
        catch (Exception)
        {
            // нет интернета — не мешаем работе: чек ждёт в очереди и уйдёт, когда появится связь (сайт сам выберет каналы клиента)
            UseWaitCursor = false;
            try
            {
                QueueClientReceipt(session, bags, "all", manual: true);
                MessageBox.Show(this, UiText.SendReceiptOffline, UiText.SendReceiptTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, UiText.SendReceiptTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return;
        }
        finally
        {
            UseWaitCursor = false;
        }

        if (!telegram && !mail)
        {
            MessageBox.Show(this, UiText.SendReceiptNoChannel(first.Kod), UiText.SendReceiptTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var channel = AskReceiptChannel(first.Kod, bags.Count, telegram, mail);
        if (channel is null)
            return;
        try
        {
            QueueClientReceipt(session, bags, channel, manual: true);
            MessageBox.Show(this, UiText.SendReceiptQueued, UiText.SendReceiptTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, UiText.SendReceiptTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>Окно выбора канала: кнопки неактивны, если у клиента такого канала нет. null — отмена.</summary>
    private string? AskReceiptChannel(uint kod, int count, bool telegram, bool mail)
    {
        using var dialog = new Form
        {
            Text = UiText.SendReceiptTitle,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(560, 210),
            BackColor = Theme.Bg,
            Font = Theme.Input,
        };
        var info = new Label
        {
            Left = 18,
            Top = 16,
            Width = 524,
            Height = 70,
            Text = UiText.SendReceiptAsk(kod, count),
            ForeColor = Theme.Ink,
        };
        string? result = null;
        Button Make(string text, Color color, int left, bool enabled, string value)
        {
            var button = Theme.Command(text, color, 165);
            button.Left = left;
            button.Top = 100;
            button.Enabled = enabled;
            button.Click += (_, _) =>
            {
                result = value;
                dialog.DialogResult = DialogResult.OK;
            };
            return button;
        }

        var tg = Make(telegram ? "Telegram ✓" : "Telegram ✗", Color.FromArgb(34, 158, 217), 18, telegram, "tg");
        var em = Make(mail ? UiText.SendReceiptMail + " ✓" : UiText.SendReceiptMail + " ✗", Color.FromArgb(112, 72, 232), 197, mail, "mail");
        var both = Make(UiText.SendReceiptBoth, Theme.Ok, 376, telegram && mail, "all");
        var cancel = new Button
        {
            Text = UiText.Cancel,
            Left = 432,
            Top = 160,
            Width = 110,
            Height = 34,
            DialogResult = DialogResult.Cancel,
        };
        dialog.Controls.AddRange([info, tg, em, both, cancel]);
        dialog.CancelButton = cancel;
        return dialog.ShowDialog(this) == DialogResult.OK ? result : null;
    }
}
