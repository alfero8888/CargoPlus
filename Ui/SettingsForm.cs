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
    private readonly CheckBox[] _bolumBoxes = BolumChoice.All
        .Select(choice => new CheckBox
        {
            Tag = choice.Code,
            AutoSize = true,
            Font = Theme.Ui,
            Margin = new Padding(0, 4, 18, 4),
            Checked = true,
        })
        .ToArray();
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
    private readonly List<Action> _bind = new();
    private readonly PictureBox _logoPreview = new()
    {
        SizeMode = PictureBoxSizeMode.Zoom,
        Height = 88,
        Width = 160,
        BackColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle,
    };
    private string _logoSource = "";
    private bool _logoClear;
    private TabPage _firmPage = null!;
    private TabPage _dbPage = null!;
    private TabPage _printerPage = null!;
    private TabPage _numberPage = null!;
    private TabPage _operatorPage = null!;
    private Button _save = null!;
    private Button _closeButton = null!;

    public SettingsForm(LocalStore store, RemoteDb remote, AppUser user)
    {
        _store = store;
        _remote = remote;
        _user = user;
        Text = UiText.WindowTitle(store.GetSettings().Company);
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

        _save = Theme.Command(UiText.Save, Theme.Accent, 160);
        _closeButton = Theme.Command(UiText.Close, Color.FromArgb(90, 104, 116), 140);
        _save.Click += (_, _) => Save();
        _closeButton.Click += (_, _) => Close();
        _bind.Add(() =>
        {
            Text = UiText.WindowTitle(_company.Text);
            _save.Text = UiText.Save;
            _closeButton.Text = UiText.Close;
            _firmPage.Text = UiText.TabFirm;
            _dbPage.Text = UiText.TabDb;
            _printerPage.Text = UiText.TabPrinters;
            _numberPage.Text = UiText.TabNumbers;
            _operatorPage.Text = UiText.TabOperators;
            _ssl.Text = UiText.UseSsl;
            foreach (var box in _bolumBoxes)
                box.Text = BolumChoice.TitleOf((string)box.Tag!);
        });
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 68,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12),
            BackColor = Color.White,
        };
        bar.Controls.Add(_closeButton);
        bar.Controls.Add(_save);

        Controls.Add(tabs);
        Controls.Add(bar);
        LoadValues();
    }

    private TabPage BuildFirm()
    {
        _firmPage = new TabPage(UiText.TabFirm) { Padding = new Padding(16), BackColor = Theme.Bg };
        _language.Items.Add(new LanguageItem("tr", "Türkçe"));
        _language.Items.Add(new LanguageItem("ru", "Русский"));
        _language.SelectedIndexChanged += (_, _) => PreviewLanguage();
        var hint = BindLabel(() => UiText.LanguageHint, 72);
        var toggles = BindLabel(() => UiText.BolumToggles, 52);
        var toggleRow = new FlowLayoutPanel { Height = 40, WrapContents = false, Padding = new Padding(0, 4, 0, 0) };
        foreach (var box in _bolumBoxes)
            toggleRow.Controls.Add(box);
        var choose = BindButton(() => UiText.ChooseLogo, Theme.Header, 180);
        var clear = BindButton(() => UiText.ClearLogo, Color.FromArgb(90, 104, 116), 180);
        choose.Click += (_, _) => ChooseLogo();
        clear.Click += (_, _) => ClearLogo();
        var buttons = new FlowLayoutPanel { Height = 58, WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
        buttons.Controls.Add(choose);
        buttons.Controls.Add(clear);
        var preview = new Panel { Height = 96 };
        _logoPreview.Dock = DockStyle.Left;
        preview.Controls.Add(_logoPreview);
        Theme.Stack(
            _firmPage,
            BindField(() => UiText.Company, _company),
            BindField(() => UiText.Branch, _branch),
            BindField(() => UiText.Language, _language),
            hint,
            toggles,
            toggleRow,
            preview,
            buttons);
        return _firmPage;
    }

    private TabPage BuildConnection()
    {
        _dbPage = new TabPage(UiText.TabDb) { Padding = new Padding(16), BackColor = Theme.Bg };
        var test = BindButton(() => UiText.TestConnection, Theme.Header, 260);
        test.Click += async (_, _) => await TestAsync(test);
        var host = BindField(() => UiText.ServerAddress, _host);
        var port = BindField(() => UiText.Port, _port, 68);
        port.Width = 160;
        port.Dock = DockStyle.Left;
        var database = BindField(() => UiText.Database, _database);
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
            _dbPage,
            host,
            row,
            BindField(() => UiText.LoginName, _login),
            BindField(() => UiText.Password, _password),
            sslRow,
            buttons,
            _testResult);
        return _dbPage;
    }

    private TabPage BuildPrinters()
    {
        _printerPage = new TabPage(UiText.TabPrinters) { Padding = new Padding(16), BackColor = Theme.Bg };
        var scan = BindButton(() => UiText.FindNetwork, Theme.Accent, 180);
        var asLabel = BindButton(() => UiText.ThisLabels, Theme.Header, 170);
        var asReceipt = BindButton(() => UiText.ThisReceipts, Theme.Ok, 150);
        scan.Click += async (_, _) => await ScanAsync(scan);
        asLabel.Click += (_, _) => AssignFound(true);
        asReceipt.Click += (_, _) => AssignFound(false);
        var actions = new FlowLayoutPanel { Height = 58, WrapContents = false, Padding = new Padding(0, 6, 0, 0) };
        actions.Controls.Add(scan);
        actions.Controls.Add(asLabel);
        actions.Controls.Add(asReceipt);

        var addLabel = BindButton(() => UiText.IpLabels, Theme.Header, 180);
        var addReceipt = BindButton(() => UiText.IpReceipts, Theme.Ok, 160);
        addLabel.Click += (_, _) => AddManual(true);
        addReceipt.Click += (_, _) => AddManual(false);
        var manual = new FlowLayoutPanel { Height = 58, WrapContents = false, Padding = new Padding(0, 6, 0, 0) };
        manual.Controls.Add(addLabel);
        manual.Controls.Add(addReceipt);

        var hint = BindLabel(() => UiText.PrinterHint, 64);
        Theme.Stack(
            _printerPage,
            BindField(() => UiText.LabelPrinter, _labelPrinter),
            BindField(() => UiText.ReceiptPrinter, _receiptPrinter),
            actions,
            _foundPrinters,
            _scanStatus,
            BindField(() => UiText.ManualIp, _manualIp),
            manual,
            hint);
        return _printerPage;
    }

    private TabPage BuildNumbers()
    {
        _numberPage = new TabPage(UiText.TabNumbers) { Padding = new Padding(16), BackColor = Theme.Bg };
        var restart = BindButton(() => UiText.RestartRange, Theme.Header, 200);
        var lookup = BindButton(() => UiText.LastOnServer, Theme.Accent, 280);
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
        var explain = BindLabel(() => UiText.RangeExplain, 88);
        Theme.Stack(
            _numberPage,
            BindField(() => UiText.RangeStart, _start),
            BindField(() => UiText.RangeEnd, _end),
            BindField(() => UiText.RangeNext, _next),
            _rangeHint,
            actions,
            _serverHint,
            explain);
        return _numberPage;
    }

    private TabPage BuildOperators()
    {
        _operatorPage = new TabPage(UiText.TabOperators) { Padding = new Padding(16), BackColor = Theme.Bg };
        var add = BindButton(() => UiText.Add, Theme.Accent, 150);
        var pin = BindButton(() => UiText.ChangePin, Theme.Header, 160);
        var delete = BindButton(() => UiText.Delete, Theme.Danger, 140);
        add.Click += (_, _) => AddOperator();
        pin.Click += (_, _) => ChangePin();
        delete.Click += (_, _) => DeleteOperator();
        var actions = new FlowLayoutPanel { Height = 64, Dock = DockStyle.Top, WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
        actions.Controls.Add(add);
        actions.Controls.Add(pin);
        actions.Controls.Add(delete);
        _operators.Dock = DockStyle.Fill;
        _operatorPage.Controls.Add(_operators);
        _operatorPage.Controls.Add(actions);
        return _operatorPage;
    }

    private Label BindLabel(Func<string> text, int height)
    {
        var label = new Label
        {
            Height = height,
            Font = Theme.Ui,
            ForeColor = Theme.Muted,
        };
        _bind.Add(() => label.Text = text());
        return label;
    }

    private Button BindButton(Func<string> text, Color back, int width)
    {
        var button = Theme.Command(text(), back, width);
        _bind.Add(() => button.Text = text());
        return button;
    }

    private Panel BindField(Func<string> text, Control input, int height = 68)
    {
        var caption = new Label
        {
            AutoSize = false,
            Height = 22,
            Font = Theme.Small,
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.BottomLeft,
        };
        _bind.Add(() => caption.Text = text());
        return Theme.Field(caption, input, height);
    }

    private void PreviewLanguage()
    {
        if (_save is null)
            return;
        var code = _language.SelectedItem is LanguageItem item ? item.Code : "tr";
        AppLanguage.Use(new AppSettings { Language = code });
        foreach (var bind in _bind)
            bind();
        ReloadOperators();
        RefreshRangeHint();
    }

    private void ChooseLogo()
    {
        using var dialog = new OpenFileDialog
        {
            Title = UiText.LogoTitle,
            Filter = "PNG, JPG, BMP|*.png;*.jpg;*.jpeg;*.bmp",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        if (BrandLogo.TryLoad(dialog.FileName) is null)
        {
            MessageBox.Show(this, UiText.LogoBad, UiText.LogoTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _logoSource = dialog.FileName;
        _logoClear = false;
        ShowLogo(dialog.FileName);
    }

    private void ClearLogo()
    {
        _logoSource = "";
        _logoClear = true;
        ShowLogo(null);
    }

    private void ShowLogo(string? path)
    {
        var image = BrandLogo.TryLoad(path);
        var previous = _logoPreview.Image;
        _logoPreview.Image = image;
        previous?.Dispose();
    }

    private string ResolveLogo()
    {
        var current = _store.GetSettings().LogoPath;
        if (_logoClear)
        {
            BrandLogo.Delete(current);
            return "";
        }

        if (_logoSource.Length > 0)
            return BrandLogo.Import(_logoSource);

        return current;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        BrandLogo.Apply(this);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _logoPreview.Image?.Dispose();
        base.OnFormClosed(e);
    }

    private void LoadValues()
    {
        var settings = _store.GetSettings();
        _company.Text = settings.Company;
        _branch.Text = settings.Branch;
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
        _logoSource = "";
        _logoClear = false;
        ShowLogo(settings.LogoPath);
        _language.SelectedIndex = settings.Language == "ru" ? 1 : 0;
        foreach (var box in _bolumBoxes)
            box.Checked = settings.IsBolumEnabled((string)box.Tag!);
        RefreshRangeHint();
    }

    private static void FillPrinters(ComboBox box, string selected)
    {
        box.Items.Clear();
        box.Items.Add(new PrinterItem("", UiText.PrinterNone));
        foreach (string printer in PrinterSettings.InstalledPrinters)
            box.Items.Add(new PrinterItem(printer, printer));
        if (PrinterAddress.TryParse(selected, out var address))
            box.Items.Add(new PrinterItem(selected, UiText.NetworkMark + " · " + address));
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
        _scanStatus.Text = UiText.ScanLooking;
        try
        {
            var progress = new Progress<string>(text => _scanStatus.Text = text);
            var found = await NetworkPrinters.ScanAsync(progress, token);
            foreach (var printer in found)
                _foundPrinters.Items.Add(printer);
            Remember(found);
            _scanStatus.Text = found.Count == 0
                ? UiText.ScanNone
                : UiText.ScanFound(found.Count);
        }
        catch (OperationCanceledException)
        {
            _scanStatus.Text = UiText.ScanStopped;
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
            MessageBox.Show(this, UiText.PickFoundPrinter, UiText.TabPrinters, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        AddNetwork(label ? _labelPrinter : _receiptPrinter, printer, true);
        _scanStatus.Text = UiText.PrinterAssigned(label ? UiText.ThisLabels : UiText.ThisReceipts, printer.Address.ToString());
    }

    private void AddManual(bool label)
    {
        var text = _manualIp.Text.Trim();
        if (!System.Net.IPAddress.TryParse(text, out var address) || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            MessageBox.Show(this, UiText.EnterIp, UiText.TabPrinters, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        AddNetwork(label ? _labelPrinter : _receiptPrinter, new NetworkPrinter { Address = address }, true);
        _scanStatus.Text = UiText.PrinterAssigned(label ? UiText.ThisLabels : UiText.ThisReceipts, address.ToString());
    }

    private static void AddNetwork(ComboBox box, NetworkPrinter printer, bool select)
    {
        var value = PrinterAddress.Format(printer.Address);
        var caption = string.IsNullOrWhiteSpace(printer.Name)
            ? UiText.NetworkMark + " · " + printer.Address
            : $"{UiText.NetworkMark} · {printer.Address} · {printer.Name.Trim()}";
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
            MessageBox.Show(this, error, UiText.Settings, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            settings.LogoPath = ResolveLogo();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, UiText.LogoBad + "\n" + ex.Message, UiText.LogoTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _logoSource = "";
        _logoClear = false;
        _store.SaveSettings(settings);
        ShowLogo(settings.LogoPath);
        BrandLogo.Publish(settings.LogoPath);
        MessageBox.Show(this, UiText.Saved, UiText.Settings, MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            error = UiText.ConnectionFields;
            return false;
        }

        if (!int.TryParse(_port.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535)
        {
            error = UiText.PortRange;
            return false;
        }

        if (!int.TryParse(_start.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var start)
            || !int.TryParse(_end.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var end)
            || !int.TryParse(_next.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var next))
        {
            error = UiText.CheckBagNumbers;
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
        var enabled = _bolumBoxes.Where(box => box.Checked).Select(box => (string)box.Tag!).ToArray();
        settings.EnabledBolum = enabled.Length == 0 ? "0" : string.Join(',', enabled);
        error = "";
        return true;
    }

    private AppSettings? SettingsFromForm()
    {
        if (!TryRead(out var settings, out var error))
        {
            MessageBox.Show(this, error, UiText.Settings, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        if (!settings.IsConfigured)
        {
            MessageBox.Show(this, UiText.NeedConnection, UiText.TitleDb, MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        _testResult.Text = UiText.Connecting;
        try
        {
            var status = await _remote.EnsureSchemaAsync(settings);
            var table = status.CreatedTable
                ? UiText.TableCreated(RemoteDb.PriemTable)
                : status.AddedTrack
                    ? UiText.TrackAdded(RemoteDb.PriemTable)
                    : UiText.TableReady(RemoteDb.PriemTable);
            var client = status.ClientTableExists ? UiText.ClientOk : UiText.ClientMissing;
            _testResult.ForeColor = status.ClientTableExists ? Theme.Ok : Theme.Danger;
            _testResult.Text = UiText.Connected(table, client);
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
        _serverHint.Text = UiText.AskingServer;
        try
        {
            var max = await _remote.MaxBagNumberAsync(settings, settings.BagStart, settings.BagEnd);
            _serverHint.ForeColor = Theme.Ink;
            _serverHint.Text = max is null ? UiText.NoServerNumbers : UiText.ServerMax(max.Value);
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
            _rangeHint.Text = UiText.RangeBad;
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
        _rangeHint.Text = remaining == 0 ? UiText.RangeEmpty : UiText.RangeFree(remaining, next);
    }

    private void AddOperator()
    {
        using var dialog = new OperatorDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        var error = _store.AddOperator(dialog.OperatorName, dialog.Level, dialog.CategoryName, dialog.Pin);
        if (error is not null)
        {
            MessageBox.Show(this, error, UiText.TitleOperator, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ReloadOperators();
    }

    private void ChangePin()
    {
        if (_operators.SelectedItem is not OperatorRow row)
        {
            MessageBox.Show(this, UiText.PickOperator, UiText.Pin, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new PinDialog(row.User.Name);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        var error = _store.ChangePin(row.User.Id, dialog.Pin);
        if (error is not null)
            MessageBox.Show(this, error, UiText.Pin, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void DeleteOperator()
    {
        if (_operators.SelectedItem is not OperatorRow row)
        {
            MessageBox.Show(this, UiText.PickOperator, UiText.TitleOperator, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(this, UiText.DeleteOperator(row.User.Name), UiText.TitleOperator, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
            return;
        var error = _store.DeleteOperator(row.User.Id, _user.Id);
        if (error is not null)
        {
            MessageBox.Show(this, error, UiText.TitleOperator, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
        public override string ToString() => $"{User.Name}   ·   {UiText.LevelTitle(User.AccessLevel)}   ·   {UiText.CategoryTitle(User.Category)}";
    }
}

internal sealed class OperatorDialog : Form
{
    private readonly TextBox _name = new() { Font = Theme.Input, BorderStyle = BorderStyle.FixedSingle };
    private readonly ComboBox _level = new() { DropDownStyle = ComboBoxStyle.DropDownList, Font = Theme.Input };
    private readonly TextBox _category = new() { Font = Theme.Input, BorderStyle = BorderStyle.FixedSingle };
    private readonly TextBox _pin = new() { Font = Theme.Input, MaxLength = 4, UseSystemPasswordChar = true, BorderStyle = BorderStyle.FixedSingle };

    public string OperatorName => _name.Text;
    public string Level => (_level.SelectedItem as LevelItem)?.Code ?? "";
    public string CategoryName => _category.Text;
    public string Pin => _pin.Text;

    public OperatorDialog()
    {
        Text = UiText.NewOperator;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(440, 420);
        Font = Theme.Ui;
        _level.Items.Add(new LevelItem(AccessLevels.Admin));
        _level.Items.Add(new LevelItem(AccessLevels.Operator));
        _level.SelectedIndex = 1;
        _pin.KeyPress += (_, e) =>
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                e.Handled = true;
        };

        var ok = Theme.Command(UiText.Add, Theme.Accent, 150);
        var cancel = Theme.Command(UiText.Cancel, Color.FromArgb(90, 104, 116), 130);
        ok.Click += (_, _) =>
        {
            if (_name.Text.Trim().Length == 0 || _category.Text.Trim().Length == 0 || !CargoRules.IsPin(_pin.Text.Trim()))
            {
                MessageBox.Show(this, UiText.OperatorNeed, UiText.TitleOperator, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            Theme.Field(UiText.OperatorName, _name),
            Theme.Field(UiText.AccessLevel, _level),
            Theme.Field(UiText.Category, _category),
            Theme.Field(UiText.Pin, _pin),
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

        var ok = Theme.Command(UiText.Save, Theme.Accent, 150);
        var cancel = Theme.Command(UiText.Cancel, Color.FromArgb(90, 104, 116), 130);
        ok.Click += (_, _) =>
        {
            if (!CargoRules.IsPin(_pin.Text.Trim()) || _pin.Text.Trim() != _repeat.Text.Trim())
            {
                MessageBox.Show(this, UiText.PinMismatch, UiText.Pin, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
        Theme.Stack(body, Theme.Field(UiText.NewPin, _pin), Theme.Field(UiText.RepeatPin, _repeat), buttons);
        Controls.Add(body);
    }
}
