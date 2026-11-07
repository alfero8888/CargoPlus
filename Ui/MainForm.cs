using System.Globalization;

namespace CargoPlus;

internal sealed class MainForm : FullScreenForm
{
    private readonly LocalStore _store;
    private readonly RemoteDb _remote = new();
    private readonly SyncService _sync;
    private readonly AppUser _user;
    private readonly TextBox _code = Theme.Box();
    private readonly TextBox _storePhone = Theme.Box();
    private readonly TextBox _storeName = Theme.Box();
    private readonly TextBox _insurance = Theme.Box();
    private readonly TextBox _note = Theme.Box();
    private readonly TextBox _kilo = Theme.Box();
    private readonly ComboBox _type = new() { DropDownStyle = ComboBoxStyle.DropDownList, Font = Theme.Input };
    private readonly Label _clientName = new() { Text = "—", Font = Theme.Big, ForeColor = Theme.Ink, AutoEllipsis = true, Height = 40 };
    private readonly Label _clientPhone = new() { Text = "Телефон: —", Font = Theme.UiBold, ForeColor = Theme.Ink, AutoEllipsis = true, Height = 28 };
    private readonly Label _clientState = new() { Text = "Код ещё не проверен", Font = Theme.Ui, ForeColor = Theme.Muted, AutoEllipsis = true, Height = 24 };
    private readonly Label _storeCount = new() { Font = Theme.Small, ForeColor = Theme.Muted, Height = 22, Text = "Вместе в строке магазина: 0 из 25" };
    private readonly Label _noteLabel;
    private readonly Label _headerMeta = new() { ForeColor = Color.White, Font = Theme.Small, AutoEllipsis = true };
    private readonly Label _clock = new() { ForeColor = Color.White, Font = Theme.UiBold, AutoSize = false, Width = 90, TextAlign = ContentAlignment.MiddleRight };
    private readonly Label _warn = new()
    {
        Dock = DockStyle.Top,
        Height = 36,
        BackColor = Theme.WarnBg,
        ForeColor = Theme.WarnFg,
        Font = Theme.UiBold,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(16, 0, 16, 0),
        Visible = false,
    };
    private readonly Label _status = new() { Font = Theme.Ui, ForeColor = Theme.Muted, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
    private readonly Label _count = new() { Font = Theme.UiBold, ForeColor = Theme.Ink, AutoSize = false, Width = 180, TextAlign = ContentAlignment.MiddleLeft, Text = "Мешков: 0" };
    private readonly DataGridView _grid = new();
    private readonly Button _settings;
    private readonly System.Windows.Forms.Timer _clockTimer = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer _blink = new() { Interval = 450 };
    private readonly Label _syncBanner = new()
    {
        Dock = DockStyle.Top,
        Height = 42,
        Font = Theme.UiBold,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(16, 0, 16, 0),
        Text = "Синхронизация ещё не проверялась",
        BackColor = Theme.WarnBg,
        ForeColor = Theme.WarnFg,
        Cursor = Cursors.Hand,
    };
    private readonly Label _browseTitle = new()
    {
        Dock = DockStyle.Top,
        Height = 52,
        Font = Theme.Title,
        ForeColor = Theme.Header,
        Padding = new Padding(8, 8, 0, 0),
        TextAlign = ContentAlignment.MiddleLeft,
    };
    private readonly DataGridView _browseGrid = new() { Dock = DockStyle.Fill };
    private Panel _home = null!;
    private Panel _work = null!;
    private Panel _browse = null!;
    private List<BagRecord[]>? _visits;
    private Action? _browseBack;
    private CancellationTokenSource? _flash;
    private DateTime _visitFrom;
    private string _visitTitle = "";
    private ClientInfo? _client;
    private SessionInfo? _session;
    private bool _busy;
    private bool _blinkOn;
    private bool _alert;
    private bool _lowWarned;
    private bool _allowClose;

    public bool LogoutRequested { get; private set; }

    public MainForm(LocalStore store, AppUser user)
    {
        _store = store;
        _user = user;
        _sync = new SyncService(store, _remote);
        Text = "Cargo Plus";
        _code.MaxLength = 10;
        _note.MaxLength = CargoRules.NoteMax;
        _insurance.MaxLength = 6;
        _storePhone.MaxLength = 24;
        _storeName.MaxLength = 24;
        _kilo.MaxLength = 11;
        _insurance.Enabled = false;
        _note.Enabled = false;
        _noteLabel = new Label
        {
            Text = "Примечание",
            Font = Theme.Small,
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.BottomLeft,
            AutoSize = false,
        };
        _type.IntegralHeight = false;
        AppLanguage.Use(_store.GetSettings());
        foreach (var choice in BolumChoice.All)
            _type.Items.Add(choice);

        _settings = Theme.Command("Настройки", Theme.Accent, 160);
        _settings.Visible = user.IsAdmin;
        _settings.Click += (_, _) => OpenSettings();
        var logout = Theme.Command("Выход", Color.FromArgb(120, 48, 48), 120);
        logout.Click += (_, _) => Logout();

        Build(logout);
        Wire();
        RestoreSession();
        _clockTimer.Tick += (_, _) => _clock.Text = DateTime.Now.ToString("HH:mm:ss");
        _clockTimer.Start();
        _blink.Tick += (_, _) => Blink();
        _blink.Start();
        _syncBanner.Click += (_, _) => ShowUnsynced();
        UpdateHeader();
        UpdateRangeBanner(false);
        ShowHome();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _sync.Start(ReportSync);
        UpdateRangeBanner(true);
        _code.Focus();
        _ = UpdateCheck.WatchAsync(this, CloseForUpdate);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _clockTimer.Stop();
        _clockTimer.Dispose();
        _blink.Stop();
        _blink.Dispose();
        _sync.Dispose();
        base.OnFormClosed(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_allowClose)
        {
            base.OnFormClosing(e);
            return;
        }

        var text = _session is null
            ? "Закрыть Cargo Plus?"
            : "Сдача ещё не закрыта, чеки не напечатаны. Закрыть программу? Мешки останутся и уйдут на сервер при связи.";
        if (MessageBox.Show(this, text, "Выход", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            e.Cancel = true;
        base.OnFormClosing(e);
    }

    private void Build(Button logout)
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 64,
            ColumnCount = 3,
            BackColor = Theme.Header,
            Padding = new Padding(8, 6, 8, 6),
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 500));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _headerMeta.Dock = DockStyle.Fill;
        _headerMeta.TextAlign = ContentAlignment.MiddleLeft;
        _headerMeta.ForeColor = Color.White;
        _headerMeta.Font = Theme.UiBold;
        _headerMeta.Padding = new Padding(8, 0, 8, 0);
        _clock.Dock = DockStyle.Fill;
        _clock.TextAlign = ContentAlignment.MiddleRight;
        _clock.Width = 100;
        var menu = Theme.Command("Меню", Color.FromArgb(36, 92, 130), 120);
        menu.Click += (_, _) => ShowHome();
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(8, 0, 0, 0),
            BackColor = Theme.Header,
        };
        buttons.Controls.Add(logout);
        buttons.Controls.Add(_settings);
        buttons.Controls.Add(menu);
        header.Controls.Add(_headerMeta, 0, 0);
        header.Controls.Add(_clock, 1, 0);
        header.Controls.Add(buttons, 2, 0);

        var left = Theme.CardPanel();
        var right = Theme.CardPanel();
        var find = Theme.Command("Проверить код", Theme.Accent, 200);
        find.Click += async (_, _) => await FindAsync();
        var add = Theme.Command("Принять мешок", Theme.Ok, 220);
        add.Click += (_, _) => AcceptBag();

        var leftGrid = FormGrid(1);
        AddRow(leftGrid, 34);
        AddRow(leftGrid, 22);
        AddRow(leftGrid, 40);
        AddRow(leftGrid, 52);
        AddRow(leftGrid, 40);
        AddRow(leftGrid, 28);
        AddRow(leftGrid, 26);
        Cell(leftGrid, SectionTitle("Клиент"), 0, 0);
        Cell(leftGrid, FieldLabel("Код клиента"), 0, 1);
        Cell(leftGrid, _code, 0, 2);
        Cell(leftGrid, find, 0, 3);
        Cell(leftGrid, _clientName, 0, 4);
        Cell(leftGrid, _clientPhone, 0, 5);
        Cell(leftGrid, _clientState, 0, 6);
        left.Controls.Add(leftGrid);

        var rightGrid = FormGrid(2);
        AddRow(rightGrid, 34);
        AddRow(rightGrid, 22);
        AddRow(rightGrid, 40);
        AddRow(rightGrid, 22);
        AddRow(rightGrid, 40);
        AddRow(rightGrid, 24);
        AddRow(rightGrid, 22);
        AddRow(rightGrid, 40);
        AddRow(rightGrid, 22);
        AddRow(rightGrid, 40);
        AddRow(rightGrid, 22);
        AddRow(rightGrid, 52);
        Cell(rightGrid, SectionTitle("Магазин и мешок"), 0, 0, 2);
        Cell(rightGrid, FieldLabel("Телефон магазина"), 0, 1, 2);
        Cell(rightGrid, _storePhone, 0, 2, 2);
        Cell(rightGrid, FieldLabel("Название магазина"), 0, 3, 2);
        Cell(rightGrid, _storeName, 0, 4, 2);
        Cell(rightGrid, _storeCount, 0, 5, 2);
        Cell(rightGrid, FieldLabel("Тип"), 0, 6);
        Cell(rightGrid, FieldLabel("Страховка, $"), 1, 6);
        Cell(rightGrid, _type, 0, 7);
        Cell(rightGrid, _insurance, 1, 7);
        Cell(rightGrid, _noteLabel, 0, 8, 2);
        Cell(rightGrid, _note, 0, 9, 2);
        Cell(rightGrid, FieldLabel("Килограммы"), 0, 10, 2);
        Cell(rightGrid, _kilo, 0, 11);
        Cell(rightGrid, add, 1, 11);
        right.Controls.Add(rightGrid);

        var entry = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 460,
            ColumnCount = 2,
            Padding = new Padding(12, 8, 12, 0),
            BackColor = Theme.Bg,
        };
        entry.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        entry.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        entry.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.Margin = new Padding(0, 0, 8, 8);
        right.Margin = new Padding(8, 0, 0, 8);
        left.Dock = DockStyle.Fill;
        right.Dock = DockStyle.Fill;
        entry.Controls.Add(left, 0, 0);
        entry.Controls.Add(right, 1, 0);

        SetupGrid();
        var gridHost = Theme.CardPanel();
        gridHost.Dock = DockStyle.Fill;
        gridHost.Margin = new Padding(12);
        gridHost.Padding = new Padding(8);
        _grid.Dock = DockStyle.Fill;
        gridHost.Controls.Add(_grid);
        var gridWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 12), BackColor = Theme.Bg };
        gridWrap.Controls.Add(gridHost);

        var reprint = Theme.Command("Этикетка ещё раз", Theme.Header, 210);
        reprint.Click += (_, _) => Reprint();
        var finish = Theme.Command("Завершить и напечатать два чека", Theme.Accent, 340);
        finish.Click += (_, _) => Finish();
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 76, BackColor = Color.White, Padding = new Padding(16, 14, 16, 14) };
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 760,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
        };
        actions.Controls.Add(finish);
        actions.Controls.Add(reprint);
        actions.Controls.Add(_count);
        footer.Controls.Add(_status);
        footer.Controls.Add(actions);

        _work = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Visible = false };
        _work.Controls.Add(gridWrap);
        _work.Controls.Add(entry);
        _work.Controls.Add(footer);

        _home = BuildHome();
        _browse = BuildBrowse();
        Controls.Add(_home);
        Controls.Add(_work);
        Controls.Add(_browse);
        Controls.Add(_warn);
        Controls.Add(_syncBanner);
        Controls.Add(header);
    }

    private static TableLayoutPanel FormGrid(int columns)
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = columns,
            Padding = new Padding(4, 0, 4, 0),
        };
        for (var i = 0; i < columns; i++)
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
        return grid;
    }

    private static void AddRow(TableLayoutPanel grid, int height)
    {
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        grid.RowCount = grid.RowStyles.Count;
    }

    private static void Cell(TableLayoutPanel grid, Control control, int column, int row, int span = 1)
    {
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 1, 10, 1);
        grid.Controls.Add(control, column, row);
        if (span > 1)
            grid.SetColumnSpan(control, span);
    }

    private static Label SectionTitle(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        Font = Theme.UiBold,
        ForeColor = Theme.Header,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private static Label FieldLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        Font = Theme.Small,
        ForeColor = Theme.Muted,
        TextAlign = ContentAlignment.BottomLeft,
    };

    private void SetupGrid()
    {
        PrepareGrid(_grid);
        AddColumn("kodid", "Мешок", 70);
        AddColumn("track", "Трекинг", 140);
        AddColumn("kilo", "Кг", 60);
        AddColumn("type", "Тип", 80);
        AddColumn("insurance", "Страховка", 80);
        AddColumn("store", "Магазин", 140);
        AddColumn("time", "Время", 110);
        AddColumn("sync", "Сервер", 90);
    }

    private static void PrepareGrid(DataGridView grid)
    {
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;
        grid.Font = Theme.Ui;
        grid.RowTemplate.Height = 34;
        grid.ColumnHeadersHeight = 38;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.Header;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font = Theme.UiBold;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(214, 232, 245);
        grid.DefaultCellStyle.SelectionForeColor = Theme.Ink;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 249, 252);
    }

    private void AddColumn(string name, string title, float weight)
    {
        var column = _grid.Columns.Add(name, title);
        _grid.Columns[column].FillWeight = weight;
        _grid.Columns[column].SortMode = DataGridViewColumnSortMode.NotSortable;
    }

    private void Wire()
    {
        _code.KeyPress += DigitsOnly;
        _insurance.KeyPress += DigitsOnly;
        _kilo.KeyPress += KiloChar;
        _code.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true;
            await FindAsync();
        };
        _kilo.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true;
            AcceptBag();
        };
        _storePhone.TextChanged += (_, _) => UpdateStoreCount();
        _storeName.TextChanged += (_, _) => UpdateStoreCount();
        _type.SelectedIndexChanged += (_, _) => ApplyType();
        _insurance.TextChanged += (_, _) => ApplyNoteState();
    }

    private void RestoreSession()
    {
        _session = _store.OpenSession();
        if (_session is null)
            return;

        _client = new ClientInfo
        {
            Kod = _session.Kod,
            Name = _session.ClientName,
            Surname = _session.ClientSurname,
            Phone = _session.ClientPhone,
        };
        _code.Text = _session.Kod.ToString(CultureInfo.InvariantCulture);
        _storePhone.Text = _session.StorePhone;
        _storeName.Text = _session.StoreName;
        ShowClient(_client, "Незакрытая сдача. Код уже был проверен.");
        foreach (var bag in _store.GetBags(_session.Id))
            AddRow(bag);
        UpdateCount();
    }

    private async Task FindAsync()
    {
        if (_busy)
            return;
        var text = _code.Text.Trim();
        if (text.Length == 0)
        {
            MessageBox.Show(this, "Введите код клиента.", "Код", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var kod))
        {
            MessageBox.Show(this, "Код клиента — число.", "Код", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_session is not null && _session.Kod != kod)
        {
            MessageBox.Show(this, $"Сейчас идёт сдача кода {_session.Kod}. Сначала напечатайте чеки.", "Сдача", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var settings = _store.GetSettings();
        if (!settings.IsConfigured)
        {
            MessageBox.Show(this, "Подключение к базе ещё не настроено. Это делает главный администратор.", "База", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _busy = true;
        UseWaitCursor = true;
        _clientState.ForeColor = Theme.Muted;
        _clientState.Text = "Проверяю код на сервере...";
        try
        {
            var client = await _remote.FindClientAsync(settings, kod);
            if (client is null)
            {
                if (_session is null)
                {
                    _client = null;
                    ShowClient(null, "Такого кода в базе нет.");
                    _clientState.ForeColor = Theme.Danger;
                }
                else
                {
                    _clientState.ForeColor = Theme.Danger;
                    _clientState.Text = "Такого кода в базе нет.";
                }

                MessageBox.Show(this, "Такого кода в базе нет. Мешок не принимаем.", "Код", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _client = client;
            if (_session is null)
                _grid.Rows.Clear();
            ShowClient(client, "Код есть в базе. Можно принимать мешки.");
            _storePhone.Focus();
        }
        catch (Exception ex)
        {
            if (_session is null)
                _client = null;
            _clientState.ForeColor = Theme.Danger;
            _clientState.Text = "Сервер не подтвердил код.";
            MessageBox.Show(this, "Код не подтверждён. Приём по нему не начат.\n" + RemoteDb.Human(ex), "База", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _busy = false;
            UseWaitCursor = false;
        }
    }

    private void AcceptBag()
    {
        if (_busy)
            return;
        if (_client is null)
        {
            MessageBox.Show(this, "Сначала введите код и дождитесь ответа сервера.", "Мешок", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _code.Focus();
            return;
        }

        var bolum = (_type.SelectedItem as BolumChoice)?.Code;
        if (!CargoRules.TryStore(_storePhone.Text, _storeName.Text, out var teslim, out var storeError))
        {
            MessageBox.Show(this, storeError, "Магазин", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!CargoRules.TryInsurance(bolum, _insurance.Text, _note.Text, out var sigorta, out var not1, out var insuranceError))
        {
            MessageBox.Show(this, insuranceError, "Страховка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!CargoRules.TryKilo(_kilo.Text, out var kilo, out var kiloError))
        {
            MessageBox.Show(this, kiloError, "Килограммы", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _kilo.Focus();
            return;
        }

        _busy = true;
        UseWaitCursor = true;
        try
        {
            var result = _store.AddBag(_session, _user, _client, _storePhone.Text, _storeName.Text, teslim, bolum!, sigorta, not1, kilo);
            _session = result.Session;
            AddRow(result.Bag);
            UpdateCount();
            UpdateHeader();
            UpdateRangeBanner(false);
            _kilo.Clear();
            _kilo.Focus();
            var printed = true;
            try
            {
                CargoPrinters.PrintLabel(_store.GetSettings(), result.Session, result.Bag);
            }
            catch (Exception ex)
            {
                printed = false;
                SetStatus($"Этикетка мешка {result.Bag.KodId} не напечаталась. {ex.Message}", Theme.Danger);
            }

            if (printed)
            {
                SetStatus("", Theme.Muted);
                FlashKilo();
            }
            _sync.Kick();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Мешок", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _busy = false;
            UseWaitCursor = false;
        }
    }

    private void FlashKilo()
    {
        _flash?.Cancel();
        var flash = new CancellationTokenSource();
        _flash = flash;
        _kilo.BackColor = Color.FromArgb(186, 230, 196);
        _ = ClearFlashAsync(flash.Token);
    }

    private async Task ClearFlashAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(1000, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (IsDisposed || _kilo.IsDisposed)
            return;
        _kilo.BackColor = SystemColors.Window;
    }

    private void Reprint()
    {
        if (_grid.CurrentRow?.Tag is not BagRecord bag || _session is null)
        {
            MessageBox.Show(this, "Выберите мешок в таблице.", "Этикетка", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            CargoPrinters.PrintLabel(_store.GetSettings(), _session, bag);
            FlashKilo();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Этикетка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void Finish()
    {
        if (_session is null || _grid.Rows.Count == 0)
        {
            MessageBox.Show(this, "В этой сдаче ещё нет мешков.", "Чек", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var bags = _store.GetBags(_session.Id);
        try
        {
            CargoPrinters.PrintReceipts(_store.GetSettings(), _session, bags);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Чеки не напечатались. Сдача не закрыта, можно попробовать ещё раз.\n" + ex.Message, "Чек", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _store.CloseSession(_session.Id);
        _session = null;
        _client = null;
        _code.Clear();
        _storePhone.Clear();
        _storeName.Clear();
        _kilo.Clear();
        _note.Clear();
        _insurance.Clear();
        _insurance.Enabled = false;
        _note.Enabled = false;
        _type.SelectedIndex = -1;
        _grid.Rows.Clear();
        SetStatus("", Theme.Muted);
        ShowClient(null, "Введите код клиента.");
        UpdateCount();
        BeginInvoke(() => _code.Focus());
    }

    private void OpenSettings()
    {
        if (!_user.IsAdmin)
            return;
        using var settings = new SettingsForm(_store, _remote, _user);
        settings.ShowDialog(this);
        ApplyLanguage();
        UpdateHeader();
        UpdateRangeBanner(true);
        _sync.Kick();
    }

    private void ApplyLanguage()
    {
        var selected = (_type.SelectedItem as BolumChoice)?.Code;
        AppLanguage.Use(_store.GetSettings());
        _type.Items.Clear();
        foreach (var choice in BolumChoice.All)
            _type.Items.Add(choice);
        if (selected is null)
            return;
        for (var i = 0; i < _type.Items.Count; i++)
        {
            if (_type.Items[i] is BolumChoice choice && choice.Code == selected)
            {
                _type.SelectedIndex = i;
                return;
            }
        }
    }

    private void CloseForUpdate()
    {
        _allowClose = true;
        Close();
    }

    private void Logout()
    {
        var text = _session is null
            ? "Выйти из программы на экран входа?"
            : "Сдача не закрыта. Выйти? Мешки сохранятся, чек можно напечатать после следующего входа.";
        if (MessageBox.Show(this, text, "Выход", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        LogoutRequested = true;
        _allowClose = true;
        Close();
    }

    private void ShowClient(ClientInfo? client, string state)
    {
        if (client is null)
        {
            _clientName.Text = "—";
            _clientPhone.Text = "Телефон: —";
        }
        else
        {
            _clientName.Text = client.FullName;
            _clientPhone.Text = string.IsNullOrWhiteSpace(client.Phone) ? "Телефон: —" : "Телефон: " + client.Phone;
        }

        _clientState.ForeColor = client is null ? Theme.Muted : Theme.Ok;
        _clientState.Text = state;
    }

    private void AddRow(BagRecord bag)
    {
        var index = _grid.Rows.Add(
            bag.KodId,
            bag.TrackCode,
            bag.Kilo,
            BolumChoice.TitleOf(bag.Bolum),
            bag.Sigorta.ToString(CultureInfo.InvariantCulture),
            bag.TeslimEden,
            bag.Tarih.ToString("dd.MM.yyyy HH:mm"),
            "в очереди");
        _grid.Rows[index].Tag = bag;
    }

    private void UpdateCount() => _count.Text = "Мешков: " + _grid.Rows.Count;

    private void UpdateStoreCount()
    {
        var length = CargoRules.StoreLength(_storePhone.Text, _storeName.Text);
        _storeCount.Text = $"Вместе в строке магазина: {length} из 25";
        _storeCount.ForeColor = length > 25 ? Theme.Danger : Theme.Muted;
    }

    private void ApplyType()
    {
        var code = (_type.SelectedItem as BolumChoice)?.Code;
        if (code is null || CargoRules.InsuranceLocked(code))
        {
            _insurance.Text = code is null ? "" : "0";
            _insurance.Enabled = false;
            ApplyNoteState();
            return;
        }

        _insurance.Enabled = true;
        if (_insurance.Text.Trim() is "" or "0")
            _insurance.Text = "10";
        ApplyNoteState();
    }

    private void ApplyNoteState()
    {
        var code = (_type.SelectedItem as BolumChoice)?.Code;
        var locked = code is null || CargoRules.InsuranceLocked(code);
        var zero = !int.TryParse(_insurance.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value <= 0;
        var need = !locked && zero;
        _note.Enabled = need;
        _noteLabel.ForeColor = need ? Theme.Danger : Theme.Muted;
        _noteLabel.Text = need ? "Почему страховка не взята" : "Примечание";
    }

    private void UpdateHeader()
    {
        var settings = _store.GetSettings();
        _clock.Text = DateTime.Now.ToString("HH:mm:ss");
        _headerMeta.Text = $"CARGO PLUS {AppRelease.Current}     {_user.Name}  ·  {_user.AccessLevel}  ·  следующий мешок {settings.BagNext}";
    }

    private void UpdateRangeBanner(bool dialog)
    {
        var settings = _store.GetSettings();
        var low = settings.Remaining <= CargoRules.LowBagWarning;
        _warn.Visible = low;
        _warn.Text = settings.Remaining == 0
            ? "Номера мешков закончились. Позовите главного администратора: нужно задать новый диапазон."
            : $"До конца номеров мешков осталось {settings.Remaining}. Обратите внимание: нужно обновить номера.";
        if (dialog && low && !_lowWarned)
        {
            _lowWarned = true;
            MessageBox.Show(this, _warn.Text, "Номера мешков", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        if (!low)
            _lowWarned = false;
    }

    private void ReportSync(SyncState state)
    {
        if (IsDisposed || !IsHandleCreated)
            return;
        BeginInvoke(() =>
        {
            if (IsDisposed)
                return;
            RefreshSyncColumn();
            ApplySyncBanner(state);
            if (!state.Configured)
            {
                SetStatus("База не настроена. Приём кода недоступен, пока администратор не укажет сервер.", Theme.Danger);
                return;
            }

            if (!state.Online)
            {
                SetStatus($"Нет связи. Не отправлено {state.Pending}. {state.Error}", Theme.Danger);
                return;
            }

            var when = state.LastOk?.ToString("HH:mm:ss") ?? "—";
            SetStatus(state.Pending == 0
                ? $"На связи. Всё отправлено. {when}"
                : $"На связи. Не отправлено {state.Pending}. {when}", state.Pending == 0 ? Theme.Ok : Theme.Danger);
        });
    }

    private void ApplySyncBanner(SyncState state)
    {
        var speed = state.KiloBytesPerSecond is { } value
            ? value >= 1024 ? $"{value / 1024:0.0} МБ/с" : $"{value:0} КБ/с"
            : "—";
        var lag = state.LatencyMs is { } ms ? $"{ms} мс" : "нет ответа";
        _alert = !state.Configured || !state.Online || state.Pending > 0;
        if (!_alert)
        {
            _syncBanner.BackColor = Theme.Ok;
            _syncBanner.ForeColor = Color.White;
            _syncBanner.Text = $"Синхронизация в норме. На сервере {state.Synced}, не отправлено 0. Задержка {lag}. Канал {speed}.";
            return;
        }

        var reason = !state.Configured
            ? "База не настроена."
            : !state.Online
                ? $"Связи нет. {state.Error}"
                : "Есть мешки, которые ещё не ушли на сервер.";
        _syncBanner.ForeColor = Color.White;
        _syncBanner.Text = $"Не отправлено {state.Pending}. На сервере {state.Synced}. Задержка {lag}. Канал {speed}. {reason}";
    }

    private void Blink()
    {
        if (!_alert)
            return;
        _blinkOn = !_blinkOn;
        _syncBanner.BackColor = _blinkOn ? Theme.Danger : Color.FromArgb(92, 24, 22);
    }

    private void ShowHome() => ShowPanel(_home);

    private void ShowWork()
    {
        ShowPanel(_work);
        BeginInvoke(() => _code.Focus());
    }

    private void ShowToday() => ShowVisits(DateTime.Today, "Сегодняшние приёмы");

    private void ShowReports() => ShowVisits(DateTime.Today.AddDays(-19), "Приёмы за 20 дней");

    private void ShowVisits(DateTime from, string title)
    {
        _visitFrom = from;
        _visitTitle = title;
        var visits = _store.GetBagsSince(from)
            .GroupBy(bag => bag.SessionId)
            .Select(group => group.OrderBy(bag => bag.Tarih).ToArray())
            .OrderByDescending(group => group[0].Tarih)
            .ToList();
        _visits = visits;
        _browseBack = ShowHome;
        var rows = visits.Select(visit =>
        {
            var first = visit[0];
            var kilos = visit.Sum(bag => CargoRules.ParseKilo(bag.Kilo));
            return new[]
            {
                first.Tarih.ToString("dd.MM.yyyy HH:mm"),
                first.Kod.ToString(CultureInfo.InvariantCulture),
                visit.Length.ToString(CultureInfo.InvariantCulture),
                CargoRules.FormatWeight(kilos),
                first.TrackCode,
                first.StoreName,
            };
        });
        ShowBrowse($"{title}: {visits.Count}. Нажмите строку, чтобы открыть мешки.", ["Время", "Код", "Мешков", "Кг", "Трекинг", "Магазин"], rows);
    }

    private void OpenVisit(BagRecord[] visit)
    {
        var first = visit[0];
        _visits = null;
        _browseBack = () => ShowVisits(_visitFrom, _visitTitle);
        ShowBrowse(
            $"Код {first.Kod} · {visit.Length} меш. · трекинг {first.TrackCode}",
            ["Мешок", "Кг", "Тип", "Страховка", "Магазин", "Время"],
            visit.Select(bag => new[]
            {
                bag.KodId.ToString(CultureInfo.InvariantCulture),
                bag.Kilo,
                BolumChoice.TitleOf(bag.Bolum),
                bag.Sigorta.ToString(CultureInfo.InvariantCulture),
                bag.TeslimEden,
                bag.Tarih.ToString("HH:mm"),
            }));
    }

    private void ShowUnsynced()
    {
        _visits = null;
        _browseBack = ShowHome;
        var bags = _store.GetPending(500);
        ShowBrowse(
            bags.Count == 0 ? "Не отправлено на сервер: пусто" : $"Не отправлено на сервер: {bags.Count}",
            ["Мешок", "Код", "Кг", "Тип", "Магазин", "Трекинг", "Время", "Ошибка"],
            bags.Select(bag => new[]
            {
                bag.KodId.ToString(CultureInfo.InvariantCulture),
                bag.Kod.ToString(CultureInfo.InvariantCulture),
                bag.Kilo,
                BolumChoice.TitleOf(bag.Bolum),
                bag.TeslimEden,
                bag.TrackCode,
                bag.Tarih.ToString("dd.MM.yyyy HH:mm"),
                bag.SyncError ?? "",
            }));
    }

    private void ShowBrowse(string title, string[] headers, IEnumerable<string[]> rows)
    {
        _browseTitle.Text = title;
        _browseGrid.Columns.Clear();
        _browseGrid.Rows.Clear();
        foreach (var header in headers)
        {
            var index = _browseGrid.Columns.Add(header, header);
            _browseGrid.Columns[index].SortMode = DataGridViewColumnSortMode.NotSortable;
        }

        foreach (var row in rows)
            _browseGrid.Rows.Add(row.Cast<object>().ToArray());
        ShowPanel(_browse);
    }

    private void ShowPanel(Panel panel)
    {
        _home.Visible = panel == _home;
        _work.Visible = panel == _work;
        _browse.Visible = panel == _browse;
    }

    private Panel BuildHome()
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Visible = false };
        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(48, 36, 0, 0),
            BackColor = Theme.Bg,
        };
        stack.Controls.Add(MenuButton("Принять груз", ShowWork));
        stack.Controls.Add(MenuButton("Сегодня принято", ShowToday));
        stack.Controls.Add(MenuButton("Отчёты за 20 дней", ShowReports));
        stack.Controls.Add(MenuButton("Не отправлено на сервер", ShowUnsynced));
        var note = new Label
        {
            AutoSize = true,
            Margin = new Padding(4, 18, 0, 0),
            Font = Theme.Ui,
            ForeColor = Theme.Muted,
            Text = "Принятое хранится в программе 20 дней. Старше 20 дней уходит в папку Backups и остаётся на сервере.",
        };
        stack.Controls.Add(note);
        page.Controls.Add(stack);
        return page;
    }

    private Panel BuildBrowse()
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Visible = false };
        var back = Theme.Command("В меню", Theme.Header, 160);
        back.Click += (_, _) =>
        {
            if (_browseBack is { } backTo)
                backTo();
            else
                ShowHome();
        };
        var bar = new Panel { Dock = DockStyle.Top, Height = 64, Padding = new Padding(16, 8, 0, 8), BackColor = Theme.Bg };
        back.Dock = DockStyle.Left;
        bar.Controls.Add(back);
        PrepareGrid(_browseGrid);
        _browseGrid.CellClick += (_, e) =>
        {
            if (_visits is null || e.RowIndex < 0 || e.RowIndex >= _visits.Count)
                return;
            OpenVisit(_visits[e.RowIndex]);
        };
        var host = Theme.CardPanel();
        host.Dock = DockStyle.Fill;
        host.Padding = new Padding(8);
        _browseGrid.Dock = DockStyle.Fill;
        host.Controls.Add(_browseGrid);
        var wrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16), BackColor = Theme.Bg };
        wrap.Controls.Add(host);
        page.Controls.Add(wrap);
        page.Controls.Add(_browseTitle);
        page.Controls.Add(bar);
        return page;
    }

    private static Button MenuButton(string text, Action click)
    {
        var button = Theme.Command(text, Theme.Accent, 460);
        button.Height = 64;
        button.Margin = new Padding(0, 0, 0, 14);
        button.Font = Theme.Title;
        button.Click += (_, _) => click();
        return button;
    }

    private void RefreshSyncColumn()
    {
        if (_session is null)
            return;
        var map = _store.GetBags(_session.Id).ToDictionary(bag => bag.LocalId);
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is not BagRecord bag || !map.TryGetValue(bag.LocalId, out var fresh))
                continue;
            row.Tag = fresh;
            row.Cells["sync"].Value = fresh.Synced
                ? "на сервере"
                : string.IsNullOrEmpty(fresh.SyncError) ? "в очереди" : "ошибка";
        }
    }

    private void SetStatus(string text, Color color)
    {
        _status.Text = text;
        _status.ForeColor = color;
    }

    private static void DigitsOnly(object? sender, KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            e.Handled = true;
    }

    private static void KiloChar(object? sender, KeyPressEventArgs e)
    {
        if (char.IsControl(e.KeyChar) || char.IsDigit(e.KeyChar) || e.KeyChar is ',' or '.')
            return;
        e.Handled = true;
    }
}
