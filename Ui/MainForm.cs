using System.Globalization;
using System.Net.NetworkInformation;

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
    private readonly System.Windows.Forms.Timer _blink = new() { Interval = 500 };
    private readonly Label _syncBanner = new()
    {
        Dock = DockStyle.Top,
        Height = 42,
        Font = Theme.UiBold,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(16, 0, 16, 0),
        Text = UiText.SyncIdle,
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
    private readonly PictureBox _homeLogo = new()
    {
        SizeMode = PictureBoxSizeMode.Zoom,
        Height = 150,
        BackColor = Color.White,
    };
    private readonly Label _homeCompany = new()
    {
        Height = 40,
        Font = Theme.Title,
        ForeColor = Theme.Header,
        TextAlign = ContentAlignment.MiddleLeft,
        AutoEllipsis = true,
    };
    private readonly Label _homeReport = new()
    {
        Font = Theme.Ui,
        ForeColor = Theme.Ink,
        AutoSize = false,
    };
    private Panel _work = null!;
    private Panel _browse = null!;
    private Panel _reports = null!;
    private readonly DateTimePicker _reportFrom = new() { Format = DateTimePickerFormat.Short, Width = 140, Font = Theme.Input };
    private readonly DateTimePicker _reportTo = new() { Format = DateTimePickerFormat.Short, Width = 140, Font = Theme.Input };
    private readonly TextBox _reportStore = Theme.Box();
    private readonly TextBox _reportCode = Theme.Box();
    private readonly DataGridView _reportGrid = new() { Dock = DockStyle.Fill };
    private readonly Label _reportTitle = new()
    {
        Dock = DockStyle.Top,
        Height = 52,
        Font = Theme.Title,
        ForeColor = Theme.Header,
        Padding = new Padding(16, 8, 0, 0),
        TextAlign = ContentAlignment.MiddleLeft,
    };
    private readonly List<BagRecord[]> _reportGroups = new();
    private Panel _right = null!;
    private TableLayoutPanel _entry = null!;
    private readonly List<Action> _bind = new();
    private List<BagRecord[]>? _visits;
    private Action? _browseBack;
    private CancellationTokenSource? _flash;
    private DateTime _visitFrom;
    private string _visitTitle = "";
    private ClientInfo? _client;
    private SessionInfo? _session;
    private bool _busy;
    private bool _blinkOn;
    private bool _offlineBlink;
    private bool _lowWarned;
    private SyncState? _lastSync;
    private bool _allowClose;

    public bool LogoutRequested { get; private set; }

    public MainForm(LocalStore store, AppUser user)
    {
        _store = store;
        _user = user;
        _sync = new SyncService(store, _remote);
        Text = UiText.WindowTitle(_store.GetSettings().Company);
        _code.MaxLength = 10;
        _note.MaxLength = CargoRules.NoteMax;
        _insurance.MaxLength = 6;
        _storePhone.MaxLength = 24;
        _storeName.MaxLength = 24;
        _kilo.MaxLength = 11;
        _insurance.Enabled = false;
        _note.Enabled = true;
        _noteLabel = new Label
        {
            Font = Theme.Small,
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.BottomLeft,
            AutoSize = false,
        };
        _bind.Add(() => _noteLabel.Text = UiText.Note);
        _type.IntegralHeight = false;
        AppLanguage.Use(_store.GetSettings());

        _settings = Theme.Command(UiText.Settings, Theme.Accent, 160);
        _bind.Add(() => _settings.Text = UiText.Settings);
        _settings.Visible = user.IsAdmin;
        _settings.Click += (_, _) => OpenSettings();
        var logout = Theme.Command(UiText.Exit, Color.FromArgb(120, 48, 48), 120);
        _bind.Add(() => logout.Text = UiText.Exit);
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
        ApplyTexts();
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

        var text = _session is null ? UiText.CloseApp : UiText.CloseAppOpen;
        if (MessageBox.Show(this, text, UiText.TitleExit, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
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
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 700));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _headerMeta.Dock = DockStyle.Fill;
        _headerMeta.TextAlign = ContentAlignment.MiddleLeft;
        _headerMeta.ForeColor = Color.White;
        _headerMeta.Font = Theme.UiBold;
        _headerMeta.Padding = new Padding(8, 0, 8, 0);
        _clock.Dock = DockStyle.Fill;
        _clock.TextAlign = ContentAlignment.MiddleRight;
        _clock.Width = 100;
        var about = Theme.Command(UiText.About, Color.FromArgb(36, 92, 130), 170);
        _bind.Add(() => about.Text = UiText.About);
        about.Click += (_, _) =>
        {
            using var dialog = new AboutForm(CloseForUpdate);
            dialog.ShowDialog(this);
        };
        var menu = Theme.Command(UiText.Menu, Color.FromArgb(36, 92, 130), 120);
        _bind.Add(() => menu.Text = UiText.Menu);
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
        buttons.Controls.Add(about);
        header.Controls.Add(_headerMeta, 0, 0);
        header.Controls.Add(_clock, 1, 0);
        header.Controls.Add(buttons, 2, 0);

        var left = Theme.CardPanel();
        _right = Theme.CardPanel();
        var find = Theme.Command(UiText.CheckCode, Theme.Accent, 200);
        _bind.Add(() => find.Text = UiText.CheckCode);
        find.Click += async (_, _) => await FindAsync();

        var leftGrid = FormGrid(1);
        AddRow(leftGrid, 34);
        AddRow(leftGrid, 22);
        AddRow(leftGrid, 40);
        AddRow(leftGrid, 52);
        AddRow(leftGrid, 40);
        AddRow(leftGrid, 28);
        AddRow(leftGrid, 26);
        Cell(leftGrid, Section(() => UiText.ClientSection), 0, 0);
        Cell(leftGrid, Field(() => UiText.ClientCode), 0, 1);
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
        AddRow(rightGrid, 40);
        Cell(rightGrid, Section(() => UiText.StoreSection), 0, 0, 2);
        Cell(rightGrid, Field(() => UiText.StorePhone), 0, 1, 2);
        Cell(rightGrid, _storePhone, 0, 2, 2);
        Cell(rightGrid, Field(() => UiText.StoreName), 0, 3, 2);
        Cell(rightGrid, _storeName, 0, 4, 2);
        Cell(rightGrid, _storeCount, 0, 5, 2);
        Cell(rightGrid, Field(() => UiText.Type), 0, 6);
        Cell(rightGrid, Field(() => UiText.Insurance), 1, 6);
        Cell(rightGrid, _type, 0, 7);
        Cell(rightGrid, _insurance, 1, 7);
        Cell(rightGrid, _noteLabel, 0, 8, 2);
        Cell(rightGrid, _note, 0, 9, 2);
        Cell(rightGrid, Field(() => UiText.Kilos), 0, 10, 2);
        Cell(rightGrid, _kilo, 0, 11, 2);
        _right.Controls.Add(rightGrid);

        _entry = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 460,
            ColumnCount = 2,
            Padding = new Padding(12, 8, 12, 0),
            BackColor = Theme.Bg,
        };
        _entry.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        _entry.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        _entry.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.Margin = new Padding(0, 0, 8, 8);
        _right.Margin = new Padding(8, 0, 0, 8);
        left.Dock = DockStyle.Fill;
        _right.Dock = DockStyle.Fill;
        _entry.Controls.Add(left, 0, 0);
        _entry.Controls.Add(_right, 1, 0);

        SetupGrid();
        var gridHost = Theme.CardPanel();
        gridHost.Dock = DockStyle.Fill;
        gridHost.Margin = new Padding(12);
        gridHost.Padding = new Padding(8);
        _grid.Dock = DockStyle.Fill;
        gridHost.Controls.Add(_grid);
        var gridWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 12), BackColor = Theme.Bg };
        gridWrap.Controls.Add(gridHost);

        var finish = Theme.Command(UiText.Finish, Theme.Accent, 360);
        _bind.Add(() => finish.Text = UiText.Finish);
        finish.Click += (_, _) => Finish();
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 76, BackColor = Color.White, Padding = new Padding(16, 14, 16, 14) };
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 560,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
        };
        actions.Controls.Add(finish);
        actions.Controls.Add(_count);
        footer.Controls.Add(_status);
        footer.Controls.Add(actions);

        _work = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Visible = false };
        _work.Controls.Add(gridWrap);
        _work.Controls.Add(_entry);
        _work.Controls.Add(footer);

        _home = BuildHome();
        _browse = BuildBrowse();
        _reports = BuildReports();
        Controls.Add(_home);
        Controls.Add(_work);
        Controls.Add(_browse);
        Controls.Add(_reports);
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

    private Label Section(Func<string> text)
    {
        var label = new Label
        {
            Dock = DockStyle.Fill,
            Font = Theme.UiBold,
            ForeColor = Theme.Header,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _bind.Add(() => label.Text = text());
        return label;
    }

    private Label Field(Func<string> text)
    {
        var label = new Label
        {
            Dock = DockStyle.Fill,
            Font = Theme.Small,
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.BottomLeft,
        };
        _bind.Add(() => label.Text = text());
        return label;
    }

    private void ApplyTexts()
    {
        foreach (var bind in _bind)
            bind();
        SetHeader("kodid", UiText.ColBag);
        SetHeader("track", UiText.ColTrack);
        SetHeader("kilo", UiText.ColKilo);
        SetHeader("type", UiText.ColType);
        SetHeader("office", UiText.OfficeColumn);
        SetHeader("operator", UiText.OperatorColumn);
        SetHeader("insurance", UiText.ColInsurance);
        SetHeader("store", UiText.ColStore);
        SetHeader("time", UiText.ColTime);
        SetHeader("sync", UiText.ColServer);

        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is BagRecord bag)
                row.Cells["type"].Value = BolumChoice.TitleOf(bag.Bolum);
        }

        UpdateCount();
        UpdateStoreCount();
        UpdateHeader();
        RefreshHomeReport();
        RefreshSyncColumn();
        if (_lastSync is { } state)
            ApplySyncBanner(state);
        else
            _syncBanner.Text = UiText.SyncIdle;
        if (_client is null)
            ShowClient(null, _session is null ? UiText.EnterCode : UiText.SessionOpen);
        else
            ShowClient(_client, UiText.CodeOk);
    }

    private void SetupGrid()
    {
        PrepareGrid(_grid);
        AddColumn("kodid", "Мешок", 70);
        AddColumn("track", "Трекинг", 140);
        AddColumn("kilo", "Кг", 60);
        AddColumn("type", "Тип", 80);
        AddColumn("office", "Отделение", 110);
        AddColumn("operator", "Принял", 110);
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
        _storePhone.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true;
            _storeName.Focus();
        };
        _storeName.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true;
            if (_type.Enabled)
            {
                _type.Focus();
                BeginInvoke(() =>
                {
                    if (_type.Enabled)
                        _type.DroppedDown = true;
                });
            }
            else
            {
                _kilo.Focus();
            }
        };
        _insurance.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true;
            _note.Focus();
        };
        _note.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true;
            _kilo.Focus();
        };
        _kilo.TextChanged += (_, _) => NormalizeKilo();
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
        _type.SelectionChangeCommitted += (_, _) =>
        {
            if (_type.Focused)
                BeginInvoke(AdvanceFromType);
        };
    }

    private void AdvanceFromType()
    {
        if (_insurance.Enabled)
            _insurance.Focus();
        else
            _note.Focus();
    }

    private bool _kiloEdit;

    private void NormalizeKilo()
    {
        if (_kiloEdit)
            return;
        var text = _kilo.Text.Replace('.', ',');
        if (text == _kilo.Text)
            return;
        _kiloEdit = true;
        var caret = _kilo.SelectionStart;
        _kilo.Text = text;
        _kilo.SelectionStart = Math.Min(caret, text.Length);
        _kiloEdit = false;
    }

    private void RestoreSession()
    {
        _session = _store.OpenSession();
        if (_session is null)
        {
            FillTypes();
            return;
        }

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
        ShowClient(_client, UiText.SessionOpen);
        foreach (var bag in _store.GetBags(_session.Id))
            AddRow(bag);
        var keep = _grid.Rows.Count > 0 && _grid.Rows[0].Tag is BagRecord firstBag ? firstBag.Bolum : null;
        FillTypes(keep);
        if (keep is not null)
            SelectBolum(keep);
        LockType();
        UpdateCount();
    }

    private async Task FindAsync()
    {
        if (_busy)
            return;
        var text = _code.Text.Trim();
        if (text.Length == 0)
        {
            MessageBox.Show(this, UiText.NeedCode, UiText.TitleCode, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var kod))
        {
            MessageBox.Show(this, UiText.CodeNumber, UiText.TitleCode, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_session is not null && _session.Kod != kod)
        {
            MessageBox.Show(this, UiText.CloseSessionFirst(_session.Kod), UiText.TitleSession, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var settings = _store.GetSettings();
        if (!settings.IsConfigured)
        {
            MessageBox.Show(this, UiText.DbNotReady, UiText.TitleDb, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _busy = true;
        UseWaitCursor = true;
        _clientState.ForeColor = Theme.Muted;
        _clientState.Text = UiText.CheckingCode;
        try
        {
            var client = await _remote.FindClientAsync(settings, kod);
            if (client is null)
            {
                if (_session is null)
                {
                    _client = null;
                    ShowClient(null, UiText.CodeMissing);
                    _clientState.ForeColor = Theme.Danger;
                }
                else
                {
                    _clientState.ForeColor = Theme.Danger;
                    _clientState.Text = UiText.CodeMissing;
                }

                MessageBox.Show(this, UiText.CodeMissingBag, UiText.TitleCode, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _client = client;
            if (_session is null)
                _grid.Rows.Clear();
            ShowClient(client, UiText.CodeOk);
            _storePhone.Focus();
        }
        catch (Exception ex)
        {
            if (_session is null)
                _client = null;
            _clientState.ForeColor = Theme.Danger;
            _clientState.Text = UiText.ServerRejected;
            MessageBox.Show(this, UiText.CodeNotConfirmed + RemoteDb.Human(ex), UiText.TitleDb, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            MessageBox.Show(this, UiText.NeedClientFirst, UiText.TitleBag, MessageBoxButtons.OK, MessageBoxIcon.Information);
            _code.Focus();
            return;
        }

        var bolum = _grid.Rows.Count > 0 && _grid.Rows[0].Tag is BagRecord firstBag
            ? firstBag.Bolum
            : (_type.SelectedItem as BolumChoice)?.Code;
        if (!CargoRules.TryStore(_storePhone.Text, _storeName.Text, out var teslim, out var storeError))
        {
            MessageBox.Show(this, storeError, UiText.TitleStore, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!CargoRules.TryInsurance(bolum, _insurance.Text, _note.Text, out var sigorta, out var not1, out var insuranceError))
        {
            MessageBox.Show(this, insuranceError, UiText.TitleInsurance, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!CargoRules.TryKilo(_kilo.Text, out var kilo, out var kiloError))
        {
            MessageBox.Show(this, kiloError, UiText.TitleKilo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            _note.Clear();
            LockType();
            _kilo.Focus();
            var printed = true;
            try
            {
                CargoPrinters.PrintLabel(_store.GetSettings(), result.Session, result.Bag);
            }
            catch (Exception ex)
            {
                printed = false;
                SetStatus(UiText.LabelFailed(result.Bag.KodId, ex.Message), Theme.Danger);
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
            MessageBox.Show(this, ex.Message, UiText.TitleBag, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

    private void Finish()
    {
        if (_session is null || _grid.Rows.Count == 0)
        {
            MessageBox.Show(this, UiText.NoBags, UiText.TitleReceipt, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var bags = _store.GetBags(_session.Id);
        try
        {
            CargoPrinters.PrintReceipts(_store.GetSettings(), _session, bags);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, UiText.ReceiptFailed + ex.Message, UiText.TitleReceipt, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
        _note.Enabled = true;
        _grid.Rows.Clear();
        _type.SelectedIndex = -1;
        LockType();
        SetStatus("", Theme.Muted);
        ShowClient(null, UiText.EnterCode);
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
        ApplyTexts();
        UpdateHeader();
        UpdateRangeBanner(true);
        _sync.Kick();
    }

    private void ApplyLanguage()
    {
        AppLanguage.Use(_store.GetSettings());
        FillTypes();
        ApplyTexts();
    }

    private void FillTypes(string? keep = null)
    {
        var selected = keep ?? (_type.SelectedItem as BolumChoice)?.Code;
        var settings = _store.GetSettings();
        var locked = _grid.Rows.Count > 0 ? selected : null;
        _type.Items.Clear();
        foreach (var choice in BolumChoice.All)
        {
            if (settings.IsBolumEnabled(choice.Code) || choice.Code == locked)
                _type.Items.Add(choice);
        }

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

        _type.SelectedIndex = -1;
    }

    private void SetHeader(string name, string text)
    {
        if (_grid.Columns[name] is { } column)
            column.HeaderText = text;
    }

    private void SelectBolum(string code)
    {
        for (var i = 0; i < _type.Items.Count; i++)
        {
            if (_type.Items[i] is BolumChoice choice && choice.Code == code)
            {
                _type.SelectedIndex = i;
                return;
            }
        }
    }

    private void LockType() => _type.Enabled = _grid.Rows.Count == 0;

    private void CloseForUpdate()
    {
        _allowClose = true;
        Close();
    }

    private void Logout()
    {
        var text = _session is null ? UiText.LogoutAsk : UiText.LogoutOpen;
        if (MessageBox.Show(this, text, UiText.TitleExit, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
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
            _clientPhone.Text = UiText.PhoneEmpty;
        }
        else
        {
            _clientName.Text = client.FullName;
            _clientPhone.Text = string.IsNullOrWhiteSpace(client.Phone) ? UiText.PhoneEmpty : UiText.PhonePrefix + client.Phone;
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
            bag.Office,
            bag.OperatorName,
            bag.Sigorta.ToString(CultureInfo.InvariantCulture),
            bag.TeslimEden,
            bag.Tarih.ToString("dd.MM.yyyy HH:mm"),
            UiText.InQueue);
        _grid.Rows[index].Tag = bag;
    }

    private void UpdateCount() => _count.Text = UiText.Bags(_grid.Rows.Count);

    private void UpdateStoreCount()
    {
        var length = CargoRules.StoreLength(_storePhone.Text, _storeName.Text);
        _storeCount.Text = UiText.StoreLine(length);
        _storeCount.ForeColor = length > 25 ? Theme.Danger : Theme.Muted;
    }

    private void ApplyType()
    {
        var code = (_type.SelectedItem as BolumChoice)?.Code;
        PaintIntake(code);
        if (code is null || CargoRules.InsuranceLocked(code))
        {
            _insurance.Text = code is null ? "" : "0";
            _insurance.Enabled = false;
            return;
        }

        _insurance.Enabled = true;
        if (_insurance.Text.Trim() is "" or "0")
            _insurance.Text = "10";
    }

    private void PaintIntake(string? code)
    {
        var tint = code switch
        {
            "1" => Color.FromArgb(206, 230, 252),
            "2" => Color.FromArgb(210, 236, 214),
            "3" => Color.FromArgb(255, 214, 226),
            "4" => Color.FromArgb(255, 244, 186),
            "6" => Color.FromArgb(255, 190, 130),
            _ => (Color?)null,
        };
        var page = tint ?? Theme.Bg;
        var card = tint ?? Theme.Card;
        _work.BackColor = page;
        _entry.BackColor = page;
        Tint(_right, card);
    }

    private static void Tint(Control parent, Color color)
    {
        parent.BackColor = color;
        foreach (Control child in parent.Controls)
        {
            if (child is TextBox or ComboBox or Button or DataGridView)
                continue;
            Tint(child, color);
        }
    }

    private void UpdateHeader()
    {
        var settings = _store.GetSettings();
        _clock.Text = DateTime.Now.ToString("HH:mm:ss");
        var company = string.IsNullOrWhiteSpace(settings.Company) ? AppRelease.Product : settings.Company.Trim();
        Text = UiText.WindowTitle(settings.Company);
        _headerMeta.Text = UiText.Header(company, _user.Name, UiText.LevelTitle(_user.AccessLevel), settings.BagNext);
    }

    private void UpdateRangeBanner(bool dialog)
    {
        var settings = _store.GetSettings();
        var low = settings.Remaining <= CargoRules.LowBagWarning;
        _warn.Visible = low;
        _warn.Text = settings.Remaining == 0 ? UiText.NumbersEnded : UiText.NumbersLow(settings.Remaining);
        if (dialog && low && !_lowWarned)
        {
            _lowWarned = true;
            MessageBox.Show(this, _warn.Text, UiText.TitleNumbers, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                SetStatus(UiText.DbMissingStatus, Theme.Danger);
                return;
            }

            if (!state.Online)
            {
                SetStatus(UiText.Offline(state.Pending, state.Error), Theme.Danger);
                return;
            }

            var when = state.LastOk?.ToString("HH:mm:ss") ?? "—";
            SetStatus(state.Pending == 0
                ? UiText.AllSent(when)
                : UiText.SomePending(state.Pending, when), state.Pending == 0 ? Theme.Ok : Theme.Danger);
        });
    }

    private void ApplySyncBanner(SyncState state)
    {
        _lastSync = state;
        var speed = UiText.Speed(state.KiloBytesPerSecond);
        var lag = UiText.Lag(state.LatencyMs);
        var offline = IsOffline(state);
        if (!offline && state.Configured && state.Online && state.Pending == 0)
        {
            _offlineBlink = false;
            _syncBanner.BackColor = Theme.Ok;
            _syncBanner.ForeColor = Color.White;
            _syncBanner.Text = UiText.SyncOk(state.Synced, lag, speed);
            return;
        }

        var reason = !state.Configured
            ? UiText.SyncDb
            : !state.Online || !NetworkInterface.GetIsNetworkAvailable()
                ? UiText.SyncOffline(state.Error)
                : UiText.SyncPending;
        _syncBanner.Text = UiText.SyncLine(state.Pending, state.Synced, lag, speed, reason);
        if (offline)
        {
            if (!_offlineBlink)
                PaintOffline(true);
            _offlineBlink = true;
            return;
        }

        _offlineBlink = false;
        _syncBanner.BackColor = state.Pending > 0 ? Theme.Danger : Theme.WarnBg;
        _syncBanner.ForeColor = state.Pending > 0 ? Color.White : Theme.WarnFg;
    }

    private static bool IsOffline(SyncState? state)
    {
        if (!NetworkInterface.GetIsNetworkAvailable())
            return true;
        return state is { Configured: true, Online: false };
    }

    private void PaintOffline(bool hot)
    {
        _blinkOn = hot;
        _syncBanner.BackColor = hot ? Theme.Danger : Color.FromArgb(255, 196, 64);
        _syncBanner.ForeColor = hot ? Color.White : Color.FromArgb(72, 32, 0);
    }

    private void Blink()
    {
        var offline = IsOffline(_lastSync);
        if (!offline)
        {
            if (_offlineBlink && _lastSync is { } state)
            {
                _offlineBlink = false;
                ApplySyncBanner(state);
            }
            return;
        }

        if (!_offlineBlink && _lastSync is { } known)
        {
            _syncBanner.Text = UiText.SyncLine(
                known.Pending,
                known.Synced,
                UiText.Lag(known.LatencyMs),
                UiText.Speed(known.KiloBytesPerSecond),
                UiText.SyncOffline(known.Error));
        }

        _offlineBlink = true;
        PaintOffline(!_blinkOn);
    }

    private void ShowHome()
    {
        RefreshHomeReport();
        ShowPanel(_home);
    }

    private void ShowWork()
    {
        ShowPanel(_work);
        BeginInvoke(() => _code.Focus());
    }

    private void ShowToday() => ShowVisits(DateTime.Today, UiText.TodayTitle);

    private void ShowReports()
    {
        ShowPanel(_reports);
        SearchReports();
    }

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
        ShowBrowse(UiText.VisitTitle(title, visits.Count), [UiText.ColTime, UiText.ColCode, UiText.ColBag, UiText.ColKilo, UiText.ColTrack, UiText.ColStore], rows);
    }

    private void OpenVisit(BagRecord[] visit)
    {
        var first = visit[0];
        _visits = null;
        _browseBack = () => ShowVisits(_visitFrom, _visitTitle);
        ShowBrowse(
            UiText.VisitHead(first.Kod, visit.Length, first.TrackCode),
            [UiText.ColBag, UiText.ColKilo, UiText.ColType, UiText.ColInsurance, UiText.ColStore, UiText.ColTime],
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
            UiText.UnsyncedTitle(bags.Count),
            [UiText.ColBag, UiText.ColCode, UiText.ColKilo, UiText.ColType, UiText.ColStore, UiText.ColTrack, UiText.ColTime, UiText.ColError],
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
        _reports.Visible = panel == _reports;
    }

    private Panel BuildReports()
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Visible = false };
        _reportFrom.Value = DateTime.Today.AddDays(-19);
        _reportTo.Value = DateTime.Today;
        _reportCode.MaxLength = 10;
        _reportCode.KeyPress += DigitsOnly;
        var search = Theme.Command(UiText.ReportSearch, Theme.Accent, 140);
        var reprint = Theme.Command(UiText.ReprintReceipt, Theme.Header, 280);
        var back = Theme.Command(UiText.BackMenu, Color.FromArgb(90, 104, 116), 150);
        _bind.Add(() =>
        {
            search.Text = UiText.ReportSearch;
            reprint.Text = UiText.ReprintReceipt;
            back.Text = UiText.BackMenu;
        });
        search.Click += (_, _) => SearchReports();
        reprint.Click += (_, _) => ReprintStoreReceipt();
        back.Click += (_, _) => ShowHome();
        _reportStore.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true;
            SearchReports();
        };
        _reportCode.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true;
            SearchReports();
        };
        _reportGrid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
                ReprintStoreReceipt();
        };

        var filters = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 78,
            WrapContents = false,
            Padding = new Padding(12, 16, 12, 8),
            BackColor = Theme.Bg,
        };
        filters.Controls.Add(FilterBox(() => UiText.ReportFrom, _reportFrom));
        filters.Controls.Add(FilterBox(() => UiText.ReportTo, _reportTo));
        filters.Controls.Add(FilterBox(() => UiText.StoreName, _reportStore, 220));
        filters.Controls.Add(FilterBox(() => UiText.ClientCode, _reportCode, 160));
        search.Margin = new Padding(8, 18, 0, 0);
        filters.Controls.Add(search);

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 70,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(12, 10, 12, 8),
            BackColor = Color.White,
        };
        bar.Controls.Add(reprint);
        bar.Controls.Add(back);

        PrepareGrid(_reportGrid);
        var host = Theme.CardPanel();
        host.Dock = DockStyle.Fill;
        host.Padding = new Padding(8);
        host.Controls.Add(_reportGrid);
        var wrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = Theme.Bg };
        wrap.Controls.Add(host);
        page.Controls.Add(wrap);
        page.Controls.Add(bar);
        page.Controls.Add(_reportTitle);
        page.Controls.Add(filters);
        return page;
    }

    private Panel FilterBox(Func<string> caption, Control input, int width = 160)
    {
        var label = new Label
        {
            AutoSize = false,
            Height = 18,
            Dock = DockStyle.Top,
            Font = Theme.Small,
            ForeColor = Theme.Muted,
        };
        _bind.Add(() => label.Text = caption());
        input.Dock = DockStyle.Top;
        input.Width = width;
        var box = new Panel { Width = width, Height = 58, Margin = new Padding(0, 0, 12, 0) };
        box.Controls.Add(input);
        box.Controls.Add(label);
        return box;
    }

    private void SearchReports()
    {
        var bags = _store.SearchBags(_reportFrom.Value, _reportTo.Value, _reportStore.Text, _reportCode.Text);
        var groups = bags
            .GroupBy(bag => string.IsNullOrWhiteSpace(bag.TrackCode) ? bag.SessionId : bag.TrackCode)
            .Select(group => group.OrderBy(bag => bag.KodId).ToArray())
            .OrderByDescending(group => group[0].Tarih)
            .ToList();
        _reportGroups.Clear();
        _reportGroups.AddRange(groups);
        _reportTitle.Text = UiText.ReportTitle(groups.Count);
        _reportGrid.Columns.Clear();
        _reportGrid.Rows.Clear();
        foreach (var header in new[] { UiText.ColTime, UiText.ColCode, UiText.ColStore, UiText.ColBag, UiText.ColKilo, UiText.ColTrack, UiText.ColType, UiText.OfficeColumn, UiText.OperatorColumn })
        {
            var index = _reportGrid.Columns.Add(header, header);
            _reportGrid.Columns[index].SortMode = DataGridViewColumnSortMode.NotSortable;
        }

        foreach (var group in groups)
        {
            var first = group[0];
            var kilos = group.Sum(bag => CargoRules.ParseKilo(bag.Kilo));
            var row = _reportGrid.Rows.Add(
                first.Tarih.ToString("dd.MM.yyyy HH:mm"),
                first.Kod.ToString(CultureInfo.InvariantCulture),
                first.StoreName,
                group.Length.ToString(CultureInfo.InvariantCulture),
                CargoRules.FormatWeight(kilos),
                first.TrackCode,
                BolumChoice.TitleOf(first.Bolum),
                first.Office,
                first.OperatorName);
            _reportGrid.Rows[row].Tag = group;
        }
    }

    private void ReprintStoreReceipt()
    {
        if (_reportGrid.CurrentRow?.Tag is not BagRecord[] bags || bags.Length == 0)
        {
            MessageBox.Show(this, UiText.ReportPick, UiText.TitleReceipt, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

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
            OperatorName = "",
            TrackCode = first.TrackCode,
        };
        try
        {
            CargoPrinters.PrintReceipts(_store.GetSettings(), session, bags, includeFirmCopy: false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, UiText.ReceiptFailed + ex.Message, UiText.TitleReceipt, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
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
        stack.Controls.Add(MenuButton(() => UiText.AcceptCargo, ShowWork));
        stack.Controls.Add(MenuButton(() => UiText.Today, ShowToday));
        stack.Controls.Add(MenuButton(() => UiText.Reports, ShowReports));
        stack.Controls.Add(MenuButton(() => UiText.Unsynced, ShowUnsynced));
        stack.Controls.Add(MenuButton(() => UiText.CheckUpdates, () => _ = UpdateCheck.CheckAsync(this, CloseForUpdate, true)));
        var note = new Label
        {
            AutoSize = true,
            Margin = new Padding(4, 18, 0, 0),
            Font = Theme.Ui,
            ForeColor = Theme.Muted,
            MaximumSize = new Size(520, 0),
        };
        _bind.Add(() => note.Text = UiText.HomeNote);
        stack.Controls.Add(note);

        var split = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Bg,
        };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 620));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        split.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        stack.Dock = DockStyle.Fill;
        split.Controls.Add(stack, 0, 0);

        var title = new Label
        {
            Height = 36,
            Font = Theme.UiBold,
            ForeColor = Theme.Header,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _bind.Add(() => title.Text = UiText.TodayReport);
        _homeLogo.Dock = DockStyle.Top;
        _homeCompany.Dock = DockStyle.Top;
        title.Dock = DockStyle.Top;
        _homeReport.Dock = DockStyle.Fill;
        var card = Theme.CardPanel();
        card.Dock = DockStyle.Fill;
        card.Padding = new Padding(20, 12, 20, 16);
        card.Controls.Add(_homeReport);
        card.Controls.Add(title);
        card.Controls.Add(_homeCompany);
        card.Controls.Add(_homeLogo);
        var side = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 36, 36, 24),
            BackColor = Theme.Bg,
        };
        side.Controls.Add(card);
        split.Controls.Add(side, 1, 0);
        page.Controls.Add(split);
        return page;
    }

    private void RefreshHomeReport()
    {
        if (_home is null)
            return;

        var settings = _store.GetSettings();
        _homeCompany.Text = settings.Company;
        var image = BrandLogo.TryLoad(settings.LogoPath);
        var previous = _homeLogo.Image;
        _homeLogo.Image = image;
        previous?.Dispose();
        _homeLogo.Height = image is null ? 0 : 150;

        var bags = _store.GetBagsSince(DateTime.Today);
        if (bags.Count == 0)
        {
            _homeReport.Text = UiText.TodayEmpty;
            return;
        }

        var insured = bags.Count(bag => bag.Sigorta > 0);
        var plain = bags.Count - insured;
        var dollars = bags.Where(bag => bag.Sigorta > 0).Sum(bag => bag.Sigorta);
        var kilos = bags.Sum(bag => CargoRules.ParseKilo(bag.Kilo));
        var lines = new List<string>
        {
            UiText.TodayBags(bags.Count),
            UiText.TodayKilo(CargoRules.FormatWeight(kilos)),
            "",
            UiText.TodayInsured(insured, dollars),
            UiText.TodayUninsured(plain),
            "",
        };

        var groups = bags.GroupBy(bag => bag.Bolum).ToDictionary(group => group.Key, group => group.ToArray());
        foreach (var choice in BolumChoice.All)
        {
            if (groups.Remove(choice.Code, out var rows))
                lines.Add(CategoryLine(choice.ToString(), rows));
        }

        foreach (var extra in groups.OrderBy(pair => pair.Key))
            lines.Add(CategoryLine(BolumChoice.TitleOf(extra.Key), extra.Value));

        _homeReport.Text = string.Join(Environment.NewLine, lines);
    }

    private static string CategoryLine(string title, IReadOnlyList<BagRecord> rows)
    {
        var insured = rows.Count(bag => bag.Sigorta > 0);
        var kilos = rows.Sum(bag => CargoRules.ParseKilo(bag.Kilo));
        return UiText.TodayLine(title, rows.Count, CargoRules.FormatWeight(kilos), insured, rows.Count - insured);
    }

    private Panel BuildBrowse()
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Visible = false };
        var back = Theme.Command(UiText.BackMenu, Theme.Header, 180);
        _bind.Add(() => back.Text = UiText.BackMenu);
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

    private Button MenuButton(Func<string> text, Action click)
    {
        var button = Theme.Command(text(), Theme.Accent, 520);
        _bind.Add(() => button.Text = text());
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
                ? UiText.OnServer
                : string.IsNullOrEmpty(fresh.SyncError) ? UiText.InQueue : UiText.SyncError;
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

    private void KiloChar(object? sender, KeyPressEventArgs e)
    {
        if (e.KeyChar == '.')
            e.KeyChar = ',';
        if (e.KeyChar == ',' && _kilo.Text.Contains(','))
        {
            e.Handled = true;
            return;
        }

        if (char.IsControl(e.KeyChar) || char.IsDigit(e.KeyChar) || e.KeyChar == ',')
            return;
        e.Handled = true;
    }
}
