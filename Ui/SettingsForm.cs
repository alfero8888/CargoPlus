using System.Drawing.Printing;
using System.Globalization;

namespace CargoPlus;

internal sealed class SettingsForm : Form
{
    private readonly LocalStore _store;
    private readonly RemoteDb _remote;
    private readonly AppUser _user;
    private readonly TextBox _company = new() { Font = Theme.Input, BorderStyle = BorderStyle.FixedSingle };
    private readonly TextBox _branch = new() { Font = Theme.Input, BorderStyle = BorderStyle.FixedSingle };
    private readonly ComboBox _language = new() { DropDownStyle = ComboBoxStyle.DropDownList, Font = Theme.Input };
    private readonly TextBox _host = new() { Font = Theme.Input, BorderStyle = BorderStyle.FixedSingle };
    private readonly TextBox _port = new() { Font = Theme.Input, BorderStyle = BorderStyle.FixedSingle };
    private readonly TextBox _database = new() { Font = Theme.Input, BorderStyle = BorderStyle.FixedSingle };
    private readonly TextBox _login = new() { Font = Theme.Input, BorderStyle = BorderStyle.FixedSingle };
    private readonly TextBox _password = new() { Font = Theme.Input, BorderStyle = BorderStyle.FixedSingle, UseSystemPasswordChar = true };
    private readonly CheckBox _ssl = new() { Text = "Использовать SSL", AutoSize = true, Font = Theme.Ui };
    private readonly Label _testResult = new() { AutoSize = false, Height = 72, Font = Theme.Ui, ForeColor = Theme.Ink };
    private readonly ComboBox _labelPrinter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Font = Theme.Input };
    private readonly ComboBox _receiptPrinter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Font = Theme.Input };
    private readonly ListBox _foundPrinters = new() { Font = Theme.Ui, IntegralHeight = false, Height = 140 };
    private readonly TextBox _manualIp = new() { Font = Theme.Input, BorderStyle = BorderStyle.FixedSingle };
    private readonly Label _scanStatus = new() { AutoSize = false, Height = 44, Font = Theme.Ui, ForeColor = Theme.Ink };
    private CancellationTokenSource? _scan;
    private readonly TextBox _start = new() { Font = Theme.Input, BorderStyle = BorderStyle.FixedSingle };
    private readonly TextBox _end = new() { Font = Theme.Input, BorderStyle = BorderStyle.FixedSingle };
    private readonly TextBox _next = new() { Font = Theme.Input, BorderStyle = BorderStyle.FixedSingle };
    private readonly Label _rangeHint = new() { AutoSize = false, Height = 48, Font = Theme.UiBold };
    private readonly Label _serverHint = new() { AutoSize = false, Height = 48, Font = Theme.Ui, ForeColor = Theme.Muted };
    private readonly ListBox _operators = new() { Font = Theme.Ui, IntegralHeight = false };

    public SettingsForm(LocalStore store, RemoteDb remote, AppUser user)
    {
        _store = store;
        _remote = remote;
        _user = user;
        Text = "Настройки Cargo Plus";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(920, 760);
        Font = Theme.Ui;
        BackColor = Theme.Bg;

        var tabs = new TabControl { Dock = DockStyle.Fill, Font = Theme.Ui };
        tabs.TabPages.Add(BuildFirm());
        tabs.TabPages.Add(BuildConnection());
        tabs.TabPages.Add(BuildPrinters());
        tabs.TabPages.Add(BuildNumbers());
        tabs.TabPages.Add(BuildOperators());

        var save = Theme.Command("Сохранить", Theme.Accent, 160);
        var close = Theme.Command("Закрыть", Color.FromArgb(90, 104, 116), 140);
        save.Click += (_, _) => Save();
        close.Click += (_, _) => Close();
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 68,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12),
            BackColor = Color.White,
        };
        bar.Controls.Add(close);
        bar.Controls.Add(save);

        Controls.Add(tabs);
        Controls.Add(bar);
        LoadValues();
    }

    private TabPage BuildFirm()
    {
        var page = new TabPage("Фирма") { Padding = new Padding(16), BackColor = Theme.Bg };
        _language.Items.Add(new LanguageItem("tr", "Türkçe"));
        _language.Items.Add(new LanguageItem("ru", "Русский"));
        var hint = new Label
        {
            Height = 72,
            Font = Theme.Ui,
            ForeColor = Theme.Muted,
            Text = "Название и отделение печатаются сверху на бирке и на чеке. Türkçe стоит по умолчанию: бирка, чек и список отделов. Русский выбирается здесь.",
        };
        Theme.Stack(
            page,
            Theme.Field("Название фирмы", _company),
            Theme.Field("Отделение", _branch),
            Theme.Field("Язык бирки и чека", _language),
            hint);
        return page;
    }

    private TabPage BuildConnection()
    {
        var page = new TabPage("База") { Padding = new Padding(16), BackColor = Theme.Bg };
        var test = Theme.Command("Проверить подключение", Theme.Header, 260);
        test.Click += async (_, _) => await TestAsync(test);
        var host = Theme.Field("Адрес сервера", _host);
        var port = Theme.Field("Порт", _port, 68);
        port.Width = 160;
        port.Dock = DockStyle.Left;
        var database = Theme.Field("База", _database);
        var row = new Panel { Height = 68 };
        row.Controls.Add(database);
        row.Controls.Add(port);
        database.Dock = DockStyle.Fill;
        var buttons = new Panel { Height = 64, Padding = new Padding(0, 12, 0, 0) };
        test.Dock = DockStyle.Left;
        buttons.Controls.Add(test);
        var sslRow = new Panel { Height = 32 };
        _ssl.Dock = DockStyle.Left;
        sslRow.Controls.Add(_ssl);
        Theme.Stack(
            page,
            host,
            row,
            Theme.Field("Логин", _login),
            Theme.Field("Пароль", _password),
            sslRow,
            buttons,
            _testResult);
        return page;
    }

    private TabPage BuildPrinters()
    {
        var page = new TabPage("Принтеры") { Padding = new Padding(16), BackColor = Theme.Bg };
        var scan = Theme.Command("Найти в сети", Theme.Accent, 180);
        var asLabel = Theme.Command("Это этикетки", Theme.Header, 170);
        var asReceipt = Theme.Command("Это чеки", Theme.Ok, 150);
        scan.Click += async (_, _) => await ScanAsync(scan);
        asLabel.Click += (_, _) => AssignFound(true);
        asReceipt.Click += (_, _) => AssignFound(false);
        var actions = new FlowLayoutPanel { Height = 58, WrapContents = false, Padding = new Padding(0, 6, 0, 0) };
        actions.Controls.Add(scan);
        actions.Controls.Add(asLabel);
        actions.Controls.Add(asReceipt);

        var addLabel = Theme.Command("IP в этикетки", Theme.Header, 180);
        var addReceipt = Theme.Command("IP в чеки", Theme.Ok, 160);
        addLabel.Click += (_, _) => AddManual(true);
        addReceipt.Click += (_, _) => AddManual(false);
        var manual = new FlowLayoutPanel { Height = 58, WrapContents = false, Padding = new Padding(0, 6, 0, 0) };
        manual.Controls.Add(addLabel);
        manual.Controls.Add(addReceipt);

        var hint = new Label
        {
            Height = 52,
            Font = Theme.Ui,
            ForeColor = Theme.Muted,
            Text = "Сначала список Windows. «Найти в сети» ищет Argox 214 Plus и чековые принтеры по IP. Найденный адрес можно назначить кнопкой. Потом нажмите Сохранить.",
        };
        Theme.Stack(
            page,
            Theme.Field("Принтер этикеток", _labelPrinter),
            Theme.Field("Чековый принтер", _receiptPrinter),
            actions,
            _foundPrinters,
            _scanStatus,
            Theme.Field("IP вручную", _manualIp),
            manual,
            hint);
        return page;
    }

    private TabPage BuildNumbers()
    {
        var page = new TabPage("Номера мешков") { Padding = new Padding(16), BackColor = Theme.Bg };
        var restart = Theme.Command("Начать сначала", Theme.Header, 200);
        var lookup = Theme.Command("Последний номер на сервере", Theme.Accent, 280);
        restart.Click += (_, _) =>
        {
            _next.Text = _start.Text.Trim();
            RefreshRangeHint();
        };
        lookup.Click += async (_, _) => await LookupServerAsync(lookup);
        foreach (var box in new[] { _start, _end, _next })
        {
            box.KeyPress += DigitsOnly;
            box.TextChanged += (_, _) => RefreshRangeHint();
        }

        var actions = new FlowLayoutPanel { Height = 64, Padding = new Padding(0, 12, 0, 0), WrapContents = false };
        actions.Controls.Add(restart);
        actions.Controls.Add(lookup);
        var explain = new Label
        {
            Height = 48,
            Font = Theme.Ui,
            ForeColor = Theme.Muted,
            Text = "Каждый принятый мешок получает следующий номер. Когда до конца остаётся 1000 номеров, программа предупреждает. В начале года здесь можно задать диапазон заново.",
        };
        Theme.Stack(
            page,
            Theme.Field("Начало", _start),
            Theme.Field("Конец", _end),
            Theme.Field("Следующий номер", _next),
            _rangeHint,
            actions,
            _serverHint,
            explain);
        return page;
    }

    private TabPage BuildOperators()
    {
        var page = new TabPage("Операторы") { Padding = new Padding(16), BackColor = Theme.Bg };
        var add = Theme.Command("Добавить", Theme.Accent, 150);
        var pin = Theme.Command("Сменить PIN", Theme.Header, 160);
        var delete = Theme.Command("Удалить", Theme.Danger, 140);
        add.Click += (_, _) => AddOperator();
        pin.Click += (_, _) => ChangePin();
        delete.Click += (_, _) => DeleteOperator();
        var actions = new FlowLayoutPanel { Height = 64, Dock = DockStyle.Top, WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
        actions.Controls.Add(add);
        actions.Controls.Add(pin);
        actions.Controls.Add(delete);
        _operators.Dock = DockStyle.Fill;
        page.Controls.Add(_operators);
        page.Controls.Add(actions);
        return page;
    }

    private void LoadValues()
    {
        var settings = _store.GetSettings();
        _company.Text = settings.Company;
        _branch.Text = settings.Branch;
        _language.SelectedIndex = settings.Language == "ru" ? 1 : 0;
        _host.Text = settings.Host;
        _port.Text = settings.Port.ToString(CultureInfo.InvariantCulture);
        _database.Text = settings.Database;
        _login.Text = settings.User;
        _password.Text = settings.Password;
        _ssl.Checked = settings.UseSsl;
        _start.Text = settings.BagStart.ToString(CultureInfo.InvariantCulture);
        _end.Text = settings.BagEnd.ToString(CultureInfo.InvariantCulture);
        _next.Text = settings.BagNext.ToString(CultureInfo.InvariantCulture);

        FillPrinters(_labelPrinter, settings.LabelPrinter);
        FillPrinters(_receiptPrinter, settings.ReceiptPrinter);
        ReloadOperators();
        RefreshRangeHint();
    }

    private static void FillPrinters(ComboBox box, string selected)
    {
        box.Items.Clear();
        box.Items.Add(new PrinterItem("", "— не выбран —"));
        foreach (string printer in PrinterSettings.InstalledPrinters)
            box.Items.Add(new PrinterItem(printer, printer));
        if (PrinterAddress.TryParse(selected, out var address))
            box.Items.Add(new PrinterItem(selected, "Сеть · " + address));
        var index = IndexOfPrinter(box, selected);
        box.SelectedIndex = index >= 0 ? index : 0;
    }

    private async Task ScanAsync(Button button)
    {
        _scan?.Cancel();
        _scan = new CancellationTokenSource();
        var token = _scan.Token;
        button.Enabled = false;
        _foundPrinters.Items.Clear();
        _scanStatus.Text = "Ищу принтеры в локальной сети…";
        try
        {
            var progress = new Progress<string>(text => _scanStatus.Text = text);
            var found = await NetworkPrinters.ScanAsync(progress, token);
            foreach (var printer in found)
                _foundPrinters.Items.Add(printer);
            Remember(found);
            _scanStatus.Text = found.Count == 0
                ? "В этой сети никто не ответил на порт печати. Впишите IP вручную."
                : $"Найдено: {found.Count}. Если роль угадана верно, нажмите Сохранить.";
        }
        catch (OperationCanceledException)
        {
            _scanStatus.Text = "Поиск остановлен.";
        }
        finally
        {
            button.Enabled = true;
        }
    }

    private void Remember(IReadOnlyList<NetworkPrinter> found)
    {
        foreach (var printer in found)
        {
            AddNetwork(_labelPrinter, printer, false);
            AddNetwork(_receiptPrinter, printer, false);
        }

        var label = found.FirstOrDefault(printer => printer.LooksLikeLabel);
        if (label is not null)
            AddNetwork(_labelPrinter, label, true);

        var receipts = found.Where(printer => !printer.LooksLikeLabel).ToList();
        if (receipts.Count == 1)
            AddNetwork(_receiptPrinter, receipts[0], true);
    }

    private void AssignFound(bool label)
    {
        if (_foundPrinters.SelectedItem is not NetworkPrinter printer)
        {
            MessageBox.Show(this, "Выберите найденный принтер в списке.", "Принтеры", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        AddNetwork(label ? _labelPrinter : _receiptPrinter, printer, true);
        _scanStatus.Text = (label ? "Этикетки: " : "Чеки: ") + printer.Address + ". Нажмите Сохранить.";
    }

    private void AddManual(bool label)
    {
        var text = _manualIp.Text.Trim();
        if (!System.Net.IPAddress.TryParse(text, out var address) || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            MessageBox.Show(this, "Введите IP, например 192.168.1.50.", "Принтеры", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        AddNetwork(label ? _labelPrinter : _receiptPrinter, new NetworkPrinter { Address = address }, true);
        _scanStatus.Text = (label ? "Этикетки: " : "Чеки: ") + address + ". Нажмите Сохранить.";
    }

    private static void AddNetwork(ComboBox box, NetworkPrinter printer, bool select)
    {
        var value = PrinterAddress.Format(printer.Address);
        var caption = string.IsNullOrWhiteSpace(printer.Name)
            ? "Сеть · " + printer.Address
            : $"Сеть · {printer.Address} · {printer.Name.Trim()}";
        var index = IndexOfPrinter(box, value);
        if (index < 0)
        {
            box.Items.Add(new PrinterItem(value, caption));
            index = box.Items.Count - 1;
        }

        if (select)
            box.SelectedIndex = index;
    }

    private static int IndexOfPrinter(ComboBox box, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;
        for (var i = 0; i < box.Items.Count; i++)
        {
            if (box.Items[i] is PrinterItem item && item.Value == value)
                return i;
        }

        return -1;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _scan?.Cancel();
        base.OnFormClosing(e);
    }

    private void ReloadOperators()
    {
        _operators.Items.Clear();
        foreach (var user in _store.Operators())
            _operators.Items.Add(new OperatorRow(user));
    }

    private void Save()
    {
        if (!TryRead(out var settings, out var error))
        {
            MessageBox.Show(this, error, "Настройки", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _store.SaveSettings(settings);
        MessageBox.Show(this, "Настройки сохранены.", "Настройки", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private bool TryRead(out AppSettings settings, out string error)
    {
        settings = new AppSettings();
        var host = _host.Text.Trim();
        var database = _database.Text.Trim();
        var user = _login.Text.Trim();
        var anyConnection = host.Length > 0 || database.Length > 0 || user.Length > 0 || _password.Text.Length > 0;
        if (anyConnection && (host.Length == 0 || database.Length == 0 || user.Length == 0))
        {
            error = "Для базы заполните адрес, имя базы и логин.";
            return false;
        }

        if (!int.TryParse(_port.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535)
        {
            error = "Порт — число от 1 до 65535. Для MySQL обычно 3306.";
            return false;
        }

        if (!int.TryParse(_start.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var start)
            || !int.TryParse(_end.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var end)
            || !int.TryParse(_next.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var next))
        {
            error = "Проверьте начало, конец и следующий номер мешка.";
            return false;
        }

        if (!CargoRules.TryRange(start, end, next, out error))
            return false;

        settings.Host = host;
        settings.Port = port;
        settings.Database = database;
        settings.User = user;
        settings.Password = _password.Text;
        settings.UseSsl = _ssl.Checked;
        settings.LabelPrinter = SelectedPrinter(_labelPrinter);
        settings.ReceiptPrinter = SelectedPrinter(_receiptPrinter);
        settings.BagStart = start;
        settings.BagEnd = end;
        settings.BagNext = next;
        settings.Company = _company.Text.Trim();
        settings.Branch = _branch.Text.Trim();
        settings.Language = _language.SelectedItem is LanguageItem language ? language.Code : "tr";
        error = "";
        return true;
    }

    private AppSettings? SettingsFromForm()
    {
        if (!TryRead(out var settings, out var error))
        {
            MessageBox.Show(this, error, "Настройки", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        if (!settings.IsConfigured)
        {
            MessageBox.Show(this, "Сначала заполните адрес, базу и логин.", "База", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }

        return settings;
    }

    private async Task TestAsync(Button button)
    {
        var settings = SettingsFromForm();
        if (settings is null)
            return;

        button.Enabled = false;
        _testResult.ForeColor = Theme.Muted;
        _testResult.Text = "Подключаюсь...";
        try
        {
            var status = await _remote.EnsureSchemaAsync(settings);
            var table = status.CreatedTable
                ? $"Таблицы {RemoteDb.PriemTable} не было, программа её создала. Боевая priem_ob не используется."
                : status.AddedTrack
                    ? $"Таблица {RemoteDb.PriemTable} есть. Колонка track_code добавлена. Боевая priem_ob не используется."
                    : $"Запись идёт в {RemoteDb.PriemTable}. Боевая priem_ob не трогается.";
            var client = status.ClientTableExists
                ? "Код клиента читается из официальной таблицы client."
                : "Таблицы client на сервере нет, код клиента проверить нельзя.";
            _testResult.ForeColor = status.ClientTableExists ? Theme.Ok : Theme.Danger;
            _testResult.Text = "Подключение есть. " + table + " " + client + " Нажмите «Сохранить», чтобы приемка пользовалась этими данными.";
        }
        catch (Exception ex)
        {
            _testResult.ForeColor = Theme.Danger;
            _testResult.Text = RemoteDb.Human(ex);
        }
        finally
        {
            button.Enabled = true;
        }
    }

    private async Task LookupServerAsync(Button button)
    {
        var settings = SettingsFromForm();
        if (settings is null)
            return;

        button.Enabled = false;
        _serverHint.Text = "Спрашиваю сервер...";
        try
        {
            var max = await _remote.MaxBagNumberAsync(settings, settings.BagStart, settings.BagEnd);
            _serverHint.ForeColor = Theme.Ink;
            _serverHint.Text = max is null
                ? "В этом диапазоне на сервере номеров ещё нет."
                : $"Последний номер на сервере в этом диапазоне: {max}.";
        }
        catch (Exception ex)
        {
            _serverHint.ForeColor = Theme.Danger;
            _serverHint.Text = RemoteDb.Human(ex);
        }
        finally
        {
            button.Enabled = true;
        }
    }

    private void RefreshRangeHint()
    {
        if (!int.TryParse(_start.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var start)
            || !int.TryParse(_end.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var end)
            || !int.TryParse(_next.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var next))
        {
            _rangeHint.ForeColor = Theme.Danger;
            _rangeHint.Text = "Проверьте числа диапазона.";
            return;
        }

        if (!CargoRules.TryRange(start, end, next, out var error))
        {
            _rangeHint.ForeColor = Theme.Danger;
            _rangeHint.Text = error;
            return;
        }

        var remaining = next > end ? 0 : end - next + 1;
        _rangeHint.ForeColor = remaining <= CargoRules.LowBagWarning ? Theme.Danger : Theme.Ok;
        _rangeHint.Text = remaining == 0
            ? "Свободных номеров нет."
            : $"Свободно номеров: {remaining}. Следующий мешок получит {next}.";
    }

    private void AddOperator()
    {
        using var dialog = new OperatorDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        var error = _store.AddOperator(dialog.OperatorName, dialog.Level, dialog.CategoryName, dialog.Pin);
        if (error is not null)
        {
            MessageBox.Show(this, error, "Оператор", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ReloadOperators();
    }

    private void ChangePin()
    {
        if (_operators.SelectedItem is not OperatorRow row)
        {
            MessageBox.Show(this, "Выберите оператора.", "PIN", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new PinDialog(row.User.Name);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        var error = _store.ChangePin(row.User.Id, dialog.Pin);
        if (error is not null)
            MessageBox.Show(this, error, "PIN", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void DeleteOperator()
    {
        if (_operators.SelectedItem is not OperatorRow row)
        {
            MessageBox.Show(this, "Выберите оператора.", "Оператор", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(this, $"Удалить оператора {row.User.Name}?", "Оператор", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
            return;
        var error = _store.DeleteOperator(row.User.Id, _user.Id);
        if (error is not null)
        {
            MessageBox.Show(this, error, "Оператор", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ReloadOperators();
    }

    private static string SelectedPrinter(ComboBox box) =>
        box.SelectedItem is PrinterItem item ? item.Value : "";

    private static void DigitsOnly(object? sender, KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            e.Handled = true;
    }

    private sealed class LanguageItem
    {
        public LanguageItem(string code, string caption)
        {
            Code = code;
            Caption = caption;
        }

        public string Code { get; }
        public string Caption { get; }
        public override string ToString() => Caption;
    }

    private sealed class PrinterItem
    {
        public PrinterItem(string value, string caption)
        {
            Value = value;
            Caption = caption;
        }

        public string Value { get; }
        public string Caption { get; }
        public override string ToString() => Caption;
    }

    private sealed class OperatorRow
    {
        public OperatorRow(AppUser user) => User = user;
        public AppUser User { get; }
        public override string ToString() => $"{User.Name}   ·   {User.AccessLevel}   ·   {User.Category}";
    }
}

internal sealed class OperatorDialog : Form
{
    private readonly TextBox _name = new() { Font = Theme.Input, BorderStyle = BorderStyle.FixedSingle };
    private readonly ComboBox _level = new() { DropDownStyle = ComboBoxStyle.DropDownList, Font = Theme.Input };
    private readonly TextBox _category = new() { Font = Theme.Input, BorderStyle = BorderStyle.FixedSingle };
    private readonly TextBox _pin = new() { Font = Theme.Input, MaxLength = 4, UseSystemPasswordChar = true, BorderStyle = BorderStyle.FixedSingle };

    public string OperatorName => _name.Text;
    public string Level => _level.SelectedItem?.ToString() ?? "";
    public string CategoryName => _category.Text;
    public string Pin => _pin.Text;

    public OperatorDialog()
    {
        Text = "Новый оператор";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(440, 420);
        Font = Theme.Ui;
        _level.Items.AddRange(AccessLevels.All);
        _level.SelectedIndex = 1;
        _pin.KeyPress += (_, e) =>
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                e.Handled = true;
        };

        var ok = Theme.Command("Добавить", Theme.Accent, 150);
        var cancel = Theme.Command("Отмена", Color.FromArgb(90, 104, 116), 130);
        ok.Click += (_, _) =>
        {
            if (_name.Text.Trim().Length == 0 || _category.Text.Trim().Length == 0 || !CargoRules.IsPin(_pin.Text.Trim()))
            {
                MessageBox.Show(this, "Нужны имя, категория и PIN из четырёх цифр.", "Оператор", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };
        cancel.Click += (_, _) => Close();
        var buttons = new FlowLayoutPanel { Height = 64, Padding = new Padding(0, 12, 0, 0), WrapContents = false };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
        Theme.Stack(
            body,
            Theme.Field("Имя оператора", _name),
            Theme.Field("Уровень доступа", _level),
            Theme.Field("Категория", _category),
            Theme.Field("PIN", _pin),
            buttons);
        Controls.Add(body);
    }
}

internal sealed class PinDialog : Form
{
    private readonly TextBox _pin = new() { Font = Theme.Input, MaxLength = 4, UseSystemPasswordChar = true, BorderStyle = BorderStyle.FixedSingle };
    private readonly TextBox _repeat = new() { Font = Theme.Input, MaxLength = 4, UseSystemPasswordChar = true, BorderStyle = BorderStyle.FixedSingle };

    public string Pin => _pin.Text.Trim();

    public PinDialog(string operatorName)
    {
        Text = "PIN — " + operatorName;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(420, 280);
        Font = Theme.Ui;
        foreach (var box in new[] { _pin, _repeat })
        {
            box.KeyPress += (_, e) =>
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                    e.Handled = true;
            };
        }

        var ok = Theme.Command("Сохранить", Theme.Accent, 150);
        var cancel = Theme.Command("Отмена", Color.FromArgb(90, 104, 116), 130);
        ok.Click += (_, _) =>
        {
            if (!CargoRules.IsPin(_pin.Text.Trim()) || _pin.Text.Trim() != _repeat.Text.Trim())
            {
                MessageBox.Show(this, "Оба поля — один и тот же PIN из четырёх цифр.", "PIN", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };
        cancel.Click += (_, _) => Close();
        var buttons = new FlowLayoutPanel { Height = 64, Padding = new Padding(0, 12, 0, 0), WrapContents = false };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
        Theme.Stack(body, Theme.Field("Новый PIN", _pin), Theme.Field("Повтор PIN", _repeat), buttons);
        Controls.Add(body);
    }
}
