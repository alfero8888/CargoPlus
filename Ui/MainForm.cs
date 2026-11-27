using System.Globalization;
using System.Net.NetworkInformation;

namespace CargoPlus;

internal sealed partial class MainForm : FullScreenForm
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
    private readonly Label _headerMeta = new() { ForeColor = Color.White, Font = Theme.Small, AutoEllipsis = true };
    private readonly Label _clock = new() { ForeColor = Color.White, Font = Theme.UiBold, AutoSize = false, Width = 90, TextAlign = ContentAlignment.MiddleRight };
    private readonly Label _warn = new()
    {
        Dock = DockStyle.Top,
        Height = 36,
        BackColor = Theme.Danger,
        ForeColor = Color.White,
        Font = Theme.UiBold,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(16, 0, 16, 0),
        Visible = false,
    };
    private readonly Label _status = new() { Font = Theme.Ui, ForeColor = Theme.Muted, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
    private readonly Label _count = new() { Font = Theme.UiBold, ForeColor = Theme.Ink, AutoSize = false, Width = 180, TextAlign = ContentAlignment.MiddleLeft, Text = "Мешков: 0" };
    private readonly DataGridView _grid = new();
    private readonly BagContentsPanel _contents = new() { Dock = DockStyle.Top, Visible = false, BackColor = Color.White };
    private readonly CheckBox _pickAll = new() { AutoSize = true, Left = 10, Top = 12, Font = Theme.Ui };
    private readonly Button _deletePicked = Theme.Command(UiText.DeletePicked, Color.FromArgb(168, 40, 40), 250);
    private readonly CheckBox _printStore = new() { Checked = true, AutoSize = true, Font = Theme.Ui, Margin = new Padding(0, 14, 18, 0) };
    private readonly CheckBox _printCargo = new() { Checked = true, AutoSize = true, Font = Theme.Ui, Margin = new Padding(0, 14, 0, 0) };
    private readonly Label _codeWarn = new()
    {
        Dock = DockStyle.Top,
        Height = 40,
        BackColor = Theme.Danger,
        ForeColor = Color.White,
        Font = Theme.UiBold,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(16, 0, 16, 0),
        Visible = false,
        Cursor = Cursors.Hand,
    };
    private bool _syncingPick;
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
    private readonly PictureBox _homeMark = new()
    {
        SizeMode = PictureBoxSizeMode.Zoom,
        Width = 48,
        Height = 48,
        BackColor = Color.White,
        Margin = new Padding(0, 4, 10, 0),
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
    private readonly List<BagRecord> _reportBags = new();
    private Panel _right = null!;
    private TableLayoutPanel _entry = null!;
    private readonly List<Action> _bind = new();
    private List<BagRecord[]>? _visits;
    private Action? _browseBack;
    private string _browseFile = "spisok.xlsx";
    private CancellationTokenSource? _flash;
    private DateTime _visitFrom;
    private string _visitTitle = "";
    private ClientInfo? _client;
    private SessionInfo? _session;
    private bool _busy;
    private bool _blinkOn;
    private bool _offlineBlink;
    private bool _fixingCode;
    private bool _codeConfirmed;
    private string _shownDuplicate = "";
    private uint[] _missing = [];
    private readonly HashSet<uint> _promptedMissing = new();
    private SyncState? _lastSync;
    private bool _allowClose;
    private bool _updating;

    public void ShowUpdateStatus(string text)
    {
        _updating = true;
        _syncBanner.Text = text;
        _syncBanner.BackColor = Theme.Header;
        _syncBanner.ForeColor = Color.White;
    }

    public bool LogoutRequested { get; private set; }

    public MainForm(LocalStore store, AppUser user)
    {
        _store = store;
        _user = user;
        _sync = new SyncService(store, _remote) { OperatorName = user.Name };
        Text = UiText.WindowTitle(_store.GetSettings().Company);
        _code.MaxLength = 10;
        _code.Font = Theme.Digits;
        _storePhone.Font = Theme.Digits;
        _insurance.Font = Theme.Digits;
        _kilo.Font = Theme.Digits;
        _note.MaxLength = CargoRules.NoteMax;
        _insurance.MaxLength = 6;
        _storePhone.MaxLength = 24;
        _storeName.Enabled = false;
        _note.Enabled = false;
        _kilo.Enabled = false;
        _storeName.MaxLength = CargoRules.StoreNameMax;
        _kilo.MaxLength = 11;
        _insurance.Enabled = false;
        _type.IntegralHeight = false;
        AppLanguage.Use(_store.GetSettings());

        _settings = Theme.Command(UiText.Settings, Theme.Accent, 160);
        _bind.Add(() => _settings.Text = UiText.Settings);
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
        GateFields();
        _codeWarn.Click += (_, _) =>
        {
            if (_missing.Length > 0)
                _ = FixCodeAsync(_missing[0]);
        };
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _sync.Start(ReportSync);
        UpdateRangeBanner(true);
        if (_session is not null)
            ShowWork(resume: true);
        else
            ShowHome();
        FocusIntake(_session is not null);
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

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_entry is null || _entry.IsDisposed)
            return;
        FitEntry();
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
        var find = Theme.Command(UiText.CheckCode, Theme.Accent, 160);
        find.AutoSize = true;
        find.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        find.Padding = new Padding(12, 0, 12, 0);
        find.Margin = new Padding(8, 1, 0, 1);
        find.Dock = DockStyle.None;
        find.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        _bind.Add(() => find.Text = UiText.CheckCode);
        find.Click += async (_, _) => await FindAsync();
        var codeRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        codeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        codeRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        codeRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        codeRow.Controls.Add(_code, 0, 0);
        codeRow.Controls.Add(find, 1, 0);

        var leftGrid = FormGrid(1);
        AddRow(leftGrid, 34);
        AddRow(leftGrid, 46);
        AddRow(leftGrid, 40);
        AddRow(leftGrid, 28);
        AddRow(leftGrid, 26);
        Cell(leftGrid, Section(() => UiText.ClientSection), 0, 0);
        Cell(leftGrid, codeRow, 0, 1);
        Cell(leftGrid, _clientName, 0, 2);
        Cell(leftGrid, _clientPhone, 0, 3);
        Cell(leftGrid, _clientState, 0, 4);
        left.Controls.Add(leftGrid);

        var rightGrid = FormGrid(3);
        rightGrid.ColumnStyles.Clear();
        rightGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        rightGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        rightGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        AddRow(rightGrid, 44);
        AddRow(rightGrid, 44);
        Cell(rightGrid, _storePhone, 0, 0, 3);
        Cell(rightGrid, _storeName, 0, 1);
        Cell(rightGrid, _type, 1, 1);
        Cell(rightGrid, _insurance, 2, 1);
        rightGrid.Dock = DockStyle.Top;
        rightGrid.Height = 92;
        var tail = FormGrid(2);
        tail.ColumnStyles.Clear();
        tail.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tail.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        AddRow(tail, 44);
        Cell(tail, _note, 0, 0);
        Cell(tail, _kilo, 1, 0);
        tail.Dock = DockStyle.Top;
        tail.Height = 48;
        var intakeStack = new Panel { BackColor = Color.White };
        intakeStack.Controls.Add(tail);
        intakeStack.Controls.Add(_contents);
        intakeStack.Controls.Add(rightGrid);
        var intakeScroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.White,
        };
        intakeScroll.Controls.Add(intakeStack);
        void LayoutIntake()
        {
            var width = Math.Max(240, intakeScroll.ClientSize.Width);
            var height = rightGrid.Height + (_contents.Visible ? _contents.Height : 0) + tail.Height;
            var scrolled = intakeScroll.AutoScrollPosition;
            intakeStack.SuspendLayout();
            intakeStack.SetBounds(0, 0, width, height);
            intakeStack.ResumeLayout(true);
            intakeScroll.AutoScrollMinSize = new Size(0, height);
            intakeScroll.AutoScrollPosition = new Point(-scrolled.X, -scrolled.Y);
        }
        _contents.LeaveToKilos += () =>
        {
            _kilo.Focus();
            intakeScroll.ScrollControlIntoView(_kilo);
        };
        _contents.RowsChanged += LayoutIntake;
        intakeScroll.Resize += (_, _) => LayoutIntake();
        _contents.SetNames(_store.RecentItemNames());
        _right.Controls.Add(intakeScroll);

        _entry = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 320,
            ColumnCount = 2,
            Padding = new Padding(12, 8, 12, 0),
            BackColor = Theme.Bg,
        };
        _entry.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        _entry.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66));
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
        var bagTools = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.White };
        _pickAll.Text = UiText.WholeBatch;
        _bind.Add(() => _pickAll.Text = UiText.WholeBatch);
        _pickAll.CheckedChanged += (_, _) => ToggleAllBags();
        var exportItems = Theme.Command(UiText.ExportContents, Color.FromArgb(36, 92, 130), 220);
        exportItems.Dock = DockStyle.Right;
        _bind.Add(() => exportItems.Text = UiText.ExportContents);
        exportItems.Click += (_, _) => ExportContents();
        _deletePicked.Dock = DockStyle.Right;
        _bind.Add(() => _deletePicked.Text = UiText.DeletePicked);
        _deletePicked.Click += (_, _) => DeletePicked();
        bagTools.Controls.Add(_deletePicked);
        bagTools.Controls.Add(exportItems);
        bagTools.Controls.Add(_pickAll);
        gridHost.Controls.Add(_grid);
        gridHost.Controls.Add(bagTools);
        var gridWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 12), BackColor = Theme.Bg };
        gridWrap.Controls.Add(gridHost);

        var finish = Theme.Command(UiText.Finish, Theme.Accent, 280);
        _bind.Add(() => finish.Text = UiText.Finish);
        _bind.Add(() => _printStore.Text = UiText.PrintStore);
        _bind.Add(() => _printCargo.Text = UiText.PrintCargo);
        finish.Click += (_, _) => Finish();
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 76, BackColor = Color.White, Padding = new Padding(16, 14, 16, 14) };
        var copies = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            Width = 340,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.White,
        };
        copies.Controls.Add(_printStore);
        copies.Controls.Add(_printCargo);
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 480,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
        };
        actions.Controls.Add(finish);
        actions.Controls.Add(_count);
        footer.Controls.Add(_status);
        footer.Controls.Add(actions);
        footer.Controls.Add(copies);

        _work = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Visible = false };
        _work.Controls.Add(gridWrap);
        _work.Controls.Add(_entry);
        _work.Controls.Add(footer);
        FieldTip.Bind(_code, () => UiText.TipClientCode);
        FieldTip.Bind(find, () => UiText.TipCheckCode);
        FieldTip.Bind(_storePhone, () => UiText.TipStorePhone);
        FieldTip.Bind(_storeName, () => UiText.TipStoreName);
        FieldTip.Bind(_type, () => UiText.TipType);
        FieldTip.Bind(_insurance, () => UiText.InsuranceNumber);
        FieldTip.Bind(_note, () => UiText.TipNote);
        FieldTip.Bind(_kilo, () => UiText.TipKilos);

        _home = BuildHome();
        _browse = BuildBrowse();
        _reports = BuildReports();
        Controls.Add(_home);
        Controls.Add(_work);
        Controls.Add(_browse);
        Controls.Add(_reports);
        Controls.Add(_codeWarn);
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
        SetHeader("mal", UiText.MalColumn);
        SetHeader("iqty", AppLanguage.Turkish ? "Miktar" : "Кол-во");
        SetHeader("iunit", AppLanguage.Turkish ? "Birim" : "Ед.");
        SetHeader("ifatura", UiText.InvoiceNoLabel);
        SetHeader("idate", UiText.InvoiceDateLabel);
        SetHeader("office", UiText.OfficeColumn);
        SetHeader("operator", UiText.OperatorColumn);
        SetHeader("insurance", UiText.ColInsurance);
        SetHeader("store", UiText.ColStore);
        SetHeader("time", UiText.ColTime);
        SetHeader("sync", UiText.ColServer);
        _code.PlaceholderText = UiText.ClientCode;
        _storePhone.PlaceholderText = UiText.StorePhone;
        _storeName.PlaceholderText = UiText.StoreName;
        _insurance.PlaceholderText = UiText.Insurance;
        _note.PlaceholderText = UiText.Note;
        _kilo.PlaceholderText = UiText.Kilos;
        if (_grid.Columns["drop"] is DataGridViewButtonColumn)
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Tag is BagRecord bag)
                {
                    row.Cells["type"].Value = BolumChoice.TitleOf(bag.Bolum);
                    row.Cells["drop"].Value = UiText.Delete;
                    row.Cells["drop"].ToolTipText = UiText.DeleteBagTip;
                    row.Cells["edit"].Value = CargoRules.NeedsContents(bag.Bolum) ? "+" : "";
                    row.Cells["edit"].ToolTipText = CargoRules.NeedsContents(bag.Bolum) ? UiText.AddGoodsTip : "";
                }
                else if (row.Tag is ItemSlot)
                {
                    row.Cells["drop"].Value = "×";
                    row.Cells["drop"].ToolTipText = UiText.RemoveLineTip;
                    row.Cells["iunit"].Value = UiText.UnitLabel(UnitCodeFromLabel(row.Cells["iunit"].Value?.ToString()));
                }
            }
        }

        UpdateCount();
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
        _grid.ReadOnly = false;
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "pick",
            HeaderText = "",
            FillWeight = 28,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            FalseValue = false,
            TrueValue = true,
        });
        AddColumn("kodid", "Мешок", 70);
        AddColumn("track", "Трекинг", 140);
        AddColumn("kilo", "Кг", 60);
        AddColumn("type", "Тип", 80);
        AddColumn("mal", UiText.MalColumn, 160);
        AddColumn("iqty", "Miktar", 70);
        AddColumn("iunit", "Birim", 80);
        AddColumn("ifatura", UiText.InvoiceNoLabel, 120);
        AddColumn("idate", UiText.InvoiceDateLabel, 110);
        AddColumn("office", "Отделение", 110);
        AddColumn("operator", "Принял", 110);
        AddColumn("insurance", "Страховка", 80);
        AddColumn("store", "Магазин", 140);
        AddColumn("time", "Время", 110);
        AddColumn("sync", "Сервер", 90);
        _grid.Columns.Add(new DataGridViewButtonColumn
        {
            Name = "edit",
            HeaderText = "",
            UseColumnTextForButtonValue = false,
            FillWeight = 78,
            FlatStyle = FlatStyle.Flat,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });
        _grid.Columns.Add(new DataGridViewButtonColumn
        {
            Name = "drop",
            HeaderText = "",
            UseColumnTextForButtonValue = false,
            FillWeight = 78,
            FlatStyle = FlatStyle.Flat,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle =
            {
                BackColor = Color.FromArgb(168, 40, 40),
                ForeColor = Color.White,
                SelectionBackColor = Color.FromArgb(140, 28, 28),
                SelectionForeColor = Color.White,
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                Font = Theme.UiBold,
            },
        });
        foreach (DataGridViewColumn column in _grid.Columns)
            column.ReadOnly = column.Name is not ("pick" or "drop" or "edit" or "mal" or "iqty" or "iunit" or "ifatura" or "idate");
        _grid.CellBeginEdit += (_, e) =>
        {
            if (e.RowIndex < 0)
                return;
            var column = _grid.Columns[e.ColumnIndex].Name;
            var tag = _grid.Rows[e.RowIndex].Tag;
            var itemEdit = tag is ItemSlot && column is "mal" or "iqty" or "iunit" or "ifatura" or "idate";
            var pick = tag is BagRecord && column == "pick";
            if (!itemEdit && !pick)
                e.Cancel = true;
        };
        _grid.CellEndEdit += (_, e) =>
        {
            if (e.RowIndex < 0 || _grid.Rows[e.RowIndex].Tag is not ItemSlot slot)
                return;
            SaveItemRow(slot.Bag);
        };
        _grid.DataError += (_, e) => e.ThrowException = false;
        _grid.CellContentClick += (_, e) =>
        {
            if (e.RowIndex < 0)
                return;
            var row = _grid.Rows[e.RowIndex];
            var name = _grid.Columns[e.ColumnIndex].Name;
            if (name == "drop")
            {
                if (row.Tag is ItemSlot slot)
                    RemoveItemRow(row, slot);
                else if (row.Tag is BagRecord)
                    DeleteBags([row]);
            }
            else if (name == "edit" && row.Tag is BagRecord bag && CargoRules.NeedsContents(bag.Bolum))
                AddItemLine(bag);
        };
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grid.CellValueChanged += (_, e) =>
        {
            if (_syncingPick || e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "pick")
                return;
            SyncPickAll();
        };
        _grid.CellPainting += PaintDeleteButton;
    }

    private void PaintDeleteButton(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "drop")
            return;
        if (e.Graphics is null)
            return;
        e.Paint(e.ClipBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);
        var bounds = e.CellBounds;
        bounds.Inflate(-6, -5);
        using var brush = new SolidBrush(Color.FromArgb(168, 40, 40));
        e.Graphics.FillRectangle(brush, bounds);
        TextRenderer.DrawText(
            e.Graphics,
            UiText.Delete,
            Theme.UiBold,
            bounds,
            Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        e.Handled = true;
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
        grid.ScrollBars = ScrollBars.Both;
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
        _storePhone.KeyPress += PhoneChar;
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
            if (string.IsNullOrWhiteSpace(_storeName.Text))
            {
                MessageBox.Show(this, UiText.NeedStoreName, UiText.TitleStore, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

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
            if (_contents.Visible)
                _contents.FocusFirst();
            else
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
        _storePhone.TextChanged += (_, _) => GateFields();
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
        else if (_contents.Visible)
            _contents.FocusFirst();
        else
            _note.Focus();
    }

    private bool _kiloEdit;

    private void NormalizeKilo()
    {
        if (_kiloEdit)
            return;
        if (!_kilo.Text.Contains(','))
            return;
        var text = _kilo.Text.Replace(',', '.');
        _kiloEdit = true;
        var caret = _kilo.SelectionStart;
        _kilo.Text = text;
        _kilo.SelectionStart = Math.Min(caret, text.Length);
        _kiloEdit = false;
        SetStatus(UiText.CommaNotAllowed, Theme.Danger);
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
        _codeConfirmed = _session.ClientName.Trim().Length > 0;
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
        GateFields();
        ShowOfflineNote(WorkingOffline());
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

        if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var kod) || kod == 0)
        {
            MessageBox.Show(this, UiText.ZeroForbidden, UiText.TitleCode, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            _codeConfirmed = true;
            if (_session is null)
            {
                _grid.Rows.Clear();
                ResetCopies();
            }

            ShowOfflineNote(false);
            ShowClient(client, UiText.CodeOk);
            _storePhone.Focus();
            GateFields();
        }
        catch (Exception ex)
        {
            if (RemoteDb.IsLinkDown(ex))
            {
                BeginOffline(kod);
                return;
            }

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
        if (string.IsNullOrEmpty(bolum))
        {
            MessageBox.Show(this, UiText.NeedType, UiText.TitleBag, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _type.Focus();
            BeginInvoke(() =>
            {
                if (_type.Enabled)
                    _type.DroppedDown = true;
            });
            return;
        }

        if (!PhoneEnough(bolum))
        {
            MessageBox.Show(this, UiText.NeedPhoneSeven, UiText.TitleStore, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _storePhone.Focus();
            return;
        }

        if (!CargoRules.TryStore(_storePhone.Text, _storeName.Text, out var teslim, out var storeError))
        {
            MessageBox.Show(this, storeError, UiText.TitleStore, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _storeName.Focus();
            return;
        }

        List<BagItem> items = [];
        if (CargoRules.NeedsContents(bolum))
        {
            if (!_contents.TryRead(out items, out var contentsError) || items.Count == 0)
            {
                MessageBox.Show(this, contentsError.Length == 0 ? UiText.ContentsRequired : contentsError, UiText.ContentsTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        if (!CargoRules.TryKilo(_kilo.Text, out var kilo, out var kiloError))
        {
            MessageBox.Show(this, kiloError, UiText.TitleKilo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _kilo.Focus();
            return;
        }

        if (!CargoRules.TryInsurance(bolum, _insurance.Text, _note.Text, out var sigorta, out var not1, out var insuranceError))
        {
            MessageBox.Show(this, insuranceError, UiText.TitleInsurance, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        not1 = string.IsNullOrWhiteSpace(not1) ? "0" : not1.Trim();

        _busy = true;
        UseWaitCursor = true;
        try
        {
            var result = _store.AddBag(_session, _user, _client, _storePhone.Text, _storeName.Text, teslim, bolum!, sigorta, not1, kilo, items);
            result.Bag.Items = items.Select(item => new BagItem
            {
                Line = item.Line,
                Name = item.Name,
                Qty = item.Qty,
                Unit = item.Unit,
                InvoiceNo = item.InvoiceNo,
                InvoiceDate = item.InvoiceDate,
            }).ToList();
            _contents.SetNames(_store.RecentItemNames());
            _session = result.Session;
            AddRow(result.Bag);
            UpdateCount();
            UpdateHeader();
            UpdateRangeBanner(false);
            _kilo.Clear();
            _note.Clear();
            ShowOfflineNote(WorkingOffline());
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
        _kilo.BackColor = _kilo.Focused ? FocusMark.Paper : SystemColors.Window;
    }

    private void ReleaseClient()
    {
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
        ResetCopies();
        LockType();
        GateFields();
        ShowClient(null, UiText.EnterCode);
        UpdateCount();
        ShowOfflineNote(WorkingOffline());
    }

    private void Finish()
    {
        if (_session is null || _grid.Rows.Count == 0)
        {
            MessageBox.Show(this, UiText.NoBags, UiText.TitleReceipt, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var bags = _store.GetBags(_session.Id);
        if (bags.Any(bag => bag.Bolum != "4" && _session.StorePhone.Count(char.IsDigit) < 7))
        {
            MessageBox.Show(this, UiText.NeedPhoneSeven, UiText.TitleStore, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _storePhone.Focus();
            return;
        }
        foreach (var bag in bags)
            bag.Items = _store.GetItems(bag.LocalId);
        try
        {
            if (_printStore.Checked || _printCargo.Checked)
                CargoPrinters.PrintReceipts(_store.GetSettings(), _session, bags, _printCargo.Checked, _printStore.Checked);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, UiText.ReceiptFailed + ex.Message, UiText.TitleReceipt, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // клиенту — чек приёма (Telegram / e-posta), уйдёт, когда мешки будут на сервере
        if (_printStore.Checked || _printCargo.Checked)
            TryQueueClientReceipt(_session, bags);
        _store.CloseSession(_session.Id);
        ReleaseClient();
        GateFields();
        SetStatus("", Theme.Muted);
        ShowClient(null, UiText.EnterCode);
        UpdateCount();
        BeginInvoke(() => _code.Focus());
    }

    private void OpenSettings()
    {
        using var settings = new SettingsForm(_store, _remote, _user);
        settings.ShowDialog(this);
        Enabled = true;
        Activate();
        ApplyLanguage();
        ApplyTexts();
        UpdateHeader();
        UpdateRangeBanner(false);
        _sync.Kick();
    }

    private void ApplyLanguage()
    {
        AppLanguage.Use(_store.GetSettings());
        FillTypes();
        _contents.ApplyLanguage();
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

    private void LockType() => _type.Enabled = _grid.Rows.Count == 0 && _client is not null;

    private bool PhoneEnough(string? bolum) => bolum == "4" || PhoneDigits >= 7;

    private int PhoneDigits => _storePhone.Text.Count(char.IsDigit);

    private bool PhoneReady() => _client is not null;

    private void GateFields()
    {
        var ready = PhoneReady();
        _storeName.Enabled = ready;
        _note.Enabled = ready;
        _kilo.Enabled = ready;
        if (!ready)
            _insurance.Enabled = false;
        else if (_type.SelectedItem is BolumChoice choice)
            _insurance.Enabled = !CargoRules.InsuranceLocked(choice.Code);
        LockType();
    }

    private void ResetCopies()
    {
        _printStore.Checked = true;
        _printCargo.Checked = true;
    }

    private static void PhoneChar(object? sender, KeyPressEventArgs e)
    {
        if (char.IsControl(e.KeyChar))
            return;
        if (char.IsDigit(e.KeyChar) || e.KeyChar is ' ' or '+' or '-' or '(' or ')')
            return;
        e.Handled = true;
    }

    private void BeginOffline(uint kod)
    {
        if (_session is null)
        {
            _client = new ClientInfo { Kod = kod, Name = "", Surname = "", Phone = "" };
            _grid.Rows.Clear();
            ResetCopies();
        }
        else if (_client is null)
        {
            _client = new ClientInfo
            {
                Kod = _session.Kod,
                Name = _session.ClientName,
                Surname = _session.ClientSurname,
                Phone = _session.ClientPhone,
            };
        }

        _codeConfirmed = false;
        ShowClient(_client, UiText.OfflineAccept);
        _clientState.ForeColor = Theme.Danger;
        _clientState.Text = UiText.OfflineAccept;
        ShowOfflineNote(true);
        _storePhone.Focus();
        GateFields();
    }

    private bool WorkingOffline()
    {
        if (!_codeConfirmed)
            return _client is not null;
        if (!NetworkInterface.GetIsNetworkAvailable())
            return true;
        return _lastSync is { Configured: true, Online: false };
    }

    private void ShowOfflineNote(bool offline)
    {
        var mark = UiText.AcceptedOffline;
        var text = _note.Text.Trim();
        if (offline)
        {
            if (text.Length == 0 || text == "0")
                _note.Text = mark;
            return;
        }

        if (text == mark)
            _note.Text = "";
    }

    private void RefreshOfflineClient()
    {
        if (_session is not { } open || open.ClientName.Trim().Length > 0)
            return;
        var fresh = _store.GetSession(open.Id);
        if (fresh is null || fresh.ClientName.Trim().Length == 0)
            return;
        _session = fresh;
        _client = new ClientInfo
        {
            Kod = fresh.Kod,
            Name = fresh.ClientName,
            Surname = fresh.ClientSurname,
            Phone = fresh.ClientPhone,
        };
        ShowClient(_client, UiText.CodeOk);
    }

    private void OfferMissing(IReadOnlyList<uint>? codes)
    {
        _missing = codes?.Distinct().ToArray() ?? [];
        _codeWarn.Visible = _missing.Length > 0;
        if (_missing.Length == 0)
        {
            _codeWarn.Text = "";
            return;
        }

        var extra = _missing.Length > 1 ? $"  +{_missing.Length - 1}" : "";
        _codeWarn.Text = UiText.CodeMissingHold(_missing[0]) + extra;
        if (_fixingCode || _promptedMissing.Contains(_missing[0]))
            return;
        _promptedMissing.Add(_missing[0]);
        _ = FixCodeAsync(_missing[0]);
    }

    private async Task FixCodeAsync(uint oldKod)
    {
        if (_fixingCode)
            return;
        _fixingCode = true;
        try
        {
            var entered = AskNewCode(oldKod);
            if (string.IsNullOrWhiteSpace(entered))
                return;
            if (!uint.TryParse(entered, NumberStyles.None, CultureInfo.InvariantCulture, out var kod) || kod == 0)
            {
                MessageBox.Show(this, UiText.CodeNumber, UiText.TitleCode, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _promptedMissing.Remove(oldKod);
                return;
            }

            ClientInfo? client;
            try
            {
                client = await _remote.FindClientAsync(_store.GetSettings(), kod);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, UiText.CodeNotConfirmed + RemoteDb.Human(ex), UiText.TitleDb, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _promptedMissing.Remove(oldKod);
                return;
            }

            if (client is null)
            {
                MessageBox.Show(this, UiText.CodeMissingBag, UiText.TitleCode, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _promptedMissing.Remove(oldKod);
                return;
            }

            var note = UiText.AcceptedOffline;
            _store.ReplaceUnsyncedCode(oldKod, client, note);
            _promptedMissing.Remove(oldKod);
            if (_session?.Kod == oldKod)
            {
                _session = _store.GetSession(_session.Id) ?? _session;
                _client = client;
                _code.Text = client.Kod.ToString(CultureInfo.InvariantCulture);
                if (string.IsNullOrWhiteSpace(_note.Text) || _note.Text.Trim() == "0")
                    _note.Text = note;
                ShowClient(client, UiText.CodeOk);
            }

            _sync.Kick();
        }
        finally
        {
            _fixingCode = false;
        }
    }

    private string? AskNewCode(uint oldKod)
    {
        using var dialog = new Form
        {
            Text = UiText.TitleCode,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(480, 210),
            Font = Theme.Ui,
        };
        var label = new Label
        {
            Left = 16,
            Top = 16,
            Width = 448,
            Height = 72,
            Text = UiText.CodeMissingHold(oldKod),
        };
        var caption = new Label
        {
            Left = 16,
            Top = 96,
            Width = 448,
            Height = 22,
            Text = UiText.NewClientCode,
        };
        var box = new TextBox
        {
            Left = 16,
            Top = 122,
            Width = 180,
            Font = Theme.Input,
            MaxLength = 10,
        };
        box.KeyPress += DigitsOnly;
        var ok = new Button
        {
            Text = UiText.CheckCode,
            Left = 210,
            Top = 118,
            Width = 150,
            Height = 32,
            DialogResult = DialogResult.OK,
        };
        var cancel = new Button
        {
            Text = UiText.Cancel,
            Left = 370,
            Top = 118,
            Width = 90,
            Height = 32,
            DialogResult = DialogResult.Cancel,
        };
        dialog.Controls.Add(label);
        dialog.Controls.Add(caption);
        dialog.Controls.Add(box);
        dialog.Controls.Add(ok);
        dialog.Controls.Add(cancel);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;
        dialog.Shown += (_, _) => box.Focus();
        return dialog.ShowDialog(this) == DialogResult.OK ? box.Text.Trim() : null;
    }

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
            false,
            bag.KodId,
            bag.TrackCode,
            bag.Kilo,
            BolumChoice.TitleOf(bag.Bolum),
            CargoRules.ContentsBrief(bag.Items),
            "",
            "",
            "",
            "",
            bag.Office,
            bag.OperatorName,
            bag.Sigorta.ToString(CultureInfo.InvariantCulture),
            bag.TeslimEden,
            bag.Tarih.ToString("dd.MM.yyyy HH:mm"),
            UiText.InQueue);
        var row = _grid.Rows[index];
        row.Tag = bag;
        row.Cells["mal"].ToolTipText = CargoRules.ContentsText(bag.Items);
        row.Cells["edit"].Value = CargoRules.NeedsContents(bag.Bolum) ? "+" : "";
        row.Cells["edit"].ToolTipText = CargoRules.NeedsContents(bag.Bolum) ? UiText.AddGoodsTip : "";
        row.Cells["drop"].Value = UiText.Delete;
        row.Cells["drop"].ToolTipText = UiText.DeleteBagTip;
        foreach (var item in bag.Items)
            InsertItemRow(bag, item);
        if (_pickAll.Checked)
        {
            _syncingPick = true;
            _pickAll.Checked = false;
            _syncingPick = false;
        }
    }

    private void ExportContents()
    {
        var rows = _store.ExportItemRows();
        if (rows.Count == 0)
        {
            MessageBox.Show(this, UiText.ExportContentsEmpty, UiText.ContentsTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx",
            FileName = $"mal-{DateTime.Now:yyyyMMdd-HHmm}.xlsx",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        var tr = AppLanguage.Turkish;
        ExcelSheet.Save(dialog.FileName,
        [
            UiText.ColBag,
            tr ? "Satır" : "Строка",
            tr ? "Ürün" : "Товар",
            tr ? "Miktar" : "Количество",
            tr ? "Birim" : "Единица",
            UiText.InvoiceNoLabel,
            UiText.InvoiceDateLabel,
            UiText.ColTrack,
            UiText.ColTime,
            UiText.ColType,
        ], rows);
        SetStatus(UiText.ExportContentsDone, Theme.Ok);
    }

    private sealed class ItemSlot
    {
        public ItemSlot(BagRecord bag) => Bag = bag;
        public BagRecord Bag { get; set; }
    }

    private static string UnitCodeFromLabel(string? text)
    {
        var value = text?.Trim().ToLowerInvariant() ?? "";
        if (value.StartsWith("kg", StringComparison.Ordinal) || value.StartsWith("кг", StringComparison.Ordinal) || value.StartsWith("кил", StringComparison.Ordinal))
            return "kg";
        if (value.Contains("lit") || value.Contains("литр") || value is "l" or "lt")
            return "litre";
        if (value is "mt" or "m" or "metre" or "meter" || value.Contains("метр"))
            return "mt";
        if (value.Contains("cift") || value.Contains("çift") || value.Contains("çft") || value.Contains("cft") || value.Contains("пар"))
            return "cift";
        return "adet";
    }

    private int LastSlot(BagRecord bag)
    {
        var index = -1;
        for (var i = 0; i < _grid.Rows.Count; i++)
        {
            var tag = _grid.Rows[i].Tag;
            if (tag == bag || (tag is ItemSlot slot && slot.Bag == bag))
                index = i;
        }

        return index;
    }

    private IEnumerable<DataGridViewRow> ItemRows(BagRecord bag) =>
        _grid.Rows.Cast<DataGridViewRow>().Where(row => row.Tag is ItemSlot slot && slot.Bag == bag);

    private void InsertItemRow(BagRecord bag, BagItem? item)
    {
        var at = LastSlot(bag) + 1;
        if (at <= 0)
            return;
        _grid.Rows.Insert(at,
            false, "", "", "", "",
            item?.Name ?? "",
            item?.Qty ?? "",
            item is null ? UiText.UnitAdet : UiText.UnitLabel(item.Unit),
            item?.InvoiceNo ?? "",
            item is null ? "" : CargoRules.InvoiceDateText(item.InvoiceDate),
            "", "", "", "", "", "");
        var row = _grid.Rows[at];
        row.Tag = new ItemSlot(bag);
        row.Cells["edit"].Value = "";
        row.Cells["drop"].Value = "×";
        row.Cells["drop"].ToolTipText = UiText.RemoveLineTip;
        row.ReadOnly = false;
    }

    private void AddItemLine(BagRecord bag)
    {
        if (ItemRows(bag).Count() >= CargoRules.ItemLineMax)
        {
            MessageBox.Show(this, UiText.ItemLinesMax, UiText.ContentsTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        InsertItemRow(bag, null);
        var row = _grid.Rows[LastSlot(bag)];
        var cell = row.Cells["mal"];
        _grid.CurrentCell = cell;
        _grid.BeginEdit(true);
    }

    private void RemoveItemRow(DataGridViewRow row, ItemSlot slot)
    {
        if (ItemRows(slot.Bag).Count() <= 1)
        {
            MessageBox.Show(this, UiText.ContentsRequired, UiText.ContentsTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _grid.Rows.Remove(row);
        SaveItemRow(slot.Bag);
    }

    private void SaveItemRow(BagRecord bag)
    {
        var items = new List<BagItem>();
        var line = 1;
        foreach (var row in ItemRows(bag))
        {
            var name = row.Cells["mal"].Value?.ToString()?.Trim() ?? "";
            var qtyText = row.Cells["iqty"].Value?.ToString()?.Trim() ?? "";
            if (name.Length == 0 && qtyText.Length == 0)
                continue;
            if (name.Length == 0 || name.Length > CargoRules.ItemNameMax)
            {
                SetStatus(name.Length == 0 ? UiText.ContentsRequired : UiText.ItemNameLong, Theme.Danger);
                return;
            }

            if (!CargoRules.TryItemQty(qtyText, out var qty, out var error))
            {
                SetStatus(error, Theme.Danger);
                return;
            }

            var invoiceText = row.Cells["ifatura"].Value?.ToString() ?? "";
            var dateText = row.Cells["idate"].Value?.ToString() ?? "";
            if (!CargoRules.TryInvoice(invoiceText, dateText, out var invoiceNo, out var invoiceDate, out var invoiceError))
            {
                SetStatus(invoiceError, Theme.Danger);
                return;
            }

            items.Add(new BagItem
            {
                Line = line,
                Name = name,
                Qty = qty,
                Unit = UnitCodeFromLabel(row.Cells["iunit"].Value?.ToString()),
                InvoiceNo = invoiceNo,
                InvoiceDate = invoiceDate,
            });
            row.Cells["iqty"].Value = qty;
            row.Cells["ifatura"].Value = invoiceNo;
            row.Cells["idate"].Value = CargoRules.InvoiceDateText(invoiceDate);
            line++;
        }

        if (items.Count == 0)
            return;
        _store.ReplaceItems(bag.LocalId, bag.KodId, items);
        bag.Items = items;
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag != bag)
                continue;
            row.Cells["mal"].Value = CargoRules.ContentsBrief(items);
            row.Cells["mal"].ToolTipText = CargoRules.ContentsText(items);
            break;
        }

        _contents.SetNames(_store.RecentItemNames());
        _sync.Kick();
    }

    private void ToggleAllBags()
    {
        if (_syncingPick)
            return;
        _syncingPick = true;
        var picked = _pickAll.Checked;
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is BagRecord)
                row.Cells["pick"].Value = picked;
        }
        _syncingPick = false;
        _grid.Invalidate();
    }

    private void SyncPickAll()
    {
        var all = _grid.Rows.Cast<DataGridViewRow>().Where(row => row.Tag is BagRecord).All(row => row.Cells["pick"].Value is true)
                  && _grid.Rows.Cast<DataGridViewRow>().Any(row => row.Tag is BagRecord);
        if (_pickAll.Checked == all)
            return;
        _syncingPick = true;
        _pickAll.Checked = all;
        _syncingPick = false;
    }

    private void DeletePicked()
    {
        var rows = _grid.Rows.Cast<DataGridViewRow>().Where(row => row.Cells["pick"].Value is true).ToList();
        if (rows.Count == 0)
        {
            MessageBox.Show(this, UiText.DeleteNone, UiText.TitleDelete, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        DeleteBags(rows);
    }

    private void DeleteBags(IReadOnlyList<DataGridViewRow> rows)
    {
        var bags = rows.Select(row => row.Tag as BagRecord).OfType<BagRecord>().ToList();
        if (bags.Count == 0)
            return;

        var ask = bags.Count == 1 ? UiText.DeleteBagAsk(bags[0].KodId) : UiText.DeleteBagsAsk(bags.Count);
        if (MessageBox.Show(this, ask, UiText.TitleDelete, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _store.RemoveBags(bags);
        foreach (var row in _grid.Rows.Cast<DataGridViewRow>().Where(row =>
                     (row.Tag is BagRecord bag && bags.Contains(bag)) || (row.Tag is ItemSlot slot && bags.Contains(slot.Bag))).ToList())
            _grid.Rows.Remove(row);
        _syncingPick = true;
        _pickAll.Checked = false;
        _syncingPick = false;
        UpdateCount();
        if (_grid.Rows.Count == 0)
        {
            if (_session is not null)
                _store.CloseSession(_session.Id);
            ReleaseClient();
        }
        else
            LockType();
        _sync.Kick();
    }

    private void UpdateCount() => _count.Text = UiText.Bags(_grid.Rows.Cast<DataGridViewRow>().Count(row => row.Tag is BagRecord));

    private void FitEntry()
    {
        var room = Math.Max(360, Height - 240);
        _entry.Height = Math.Clamp((int)(room * 0.42), 300, 380);
    }

    private void ApplyType()
    {
        var code = (_type.SelectedItem as BolumChoice)?.Code;
        UpdateHeader();
        var show = CargoRules.NeedsContents(code);
        _contents.Visible = show;
        if (show && _contents.LineCount == 0)
            _contents.LoadLines([]);
        FitEntry();
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
        var office = settings.StationLetter.Trim();
        if (office.Length is < 1 or > 2)
            office = settings.Branch;
        var next = (_type.SelectedItem as BolumChoice) is { } choice
            ? CargoRules.NumberPrefix(office, _user.Name, choice.Code)
            : CargoRules.FirstStationLetter(office) + CargoRules.FirstStationLetter(_user.Name);
        _headerMeta.Text = UiText.Header(company, _user.Name, UiText.LevelTitle(_user.AccessLevel), next);
    }

    private void UpdateRangeBanner(bool dialog)
    {
        _ = dialog;
        _warn.Visible = false;
    }

    private void ReportSync(SyncState state)
    {
        if (IsDisposed || !IsHandleCreated)
            return;
        BeginInvoke(() =>
        {
            if (IsDisposed || _updating || !Enabled)
                return;
            RefreshSyncColumn();
            ApplySyncBanner(state);
            RefreshOfflineClient();
            OfferMissing(state.MissingCodes);
            if (!state.Configured)
            {
                SetStatus(UiText.DbMissingStatus, Theme.Danger);
                return;
            }

            if (!state.Online)
            {
                ShowOfflineNote(true);
                SetStatus(UiText.Offline(state.Pending, state.Error), Theme.Danger);
                return;
            }

            ShowOfflineNote(false);
            if (state.RejectedBag is { Length: > 0 } rejected)
            {
                SetStatus(UiText.DuplicateBag(rejected), Theme.Danger);
                if (rejected != _shownDuplicate)
                {
                    _shownDuplicate = rejected;
                    MessageBox.Show(this, UiText.DuplicateBag(rejected), UiText.TitleBag, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

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

    private void ShowWork(bool resume = false)
    {
        ShowPanel(_work);
        if (!IsHandleCreated)
            return;
        BeginInvoke(() => FocusIntake(resume));
    }

    private void FocusIntake(bool resume)
    {
        if (!resume || _client is null)
        {
            _code.Focus();
            return;
        }

        if (!PhoneReady())
            _storePhone.Focus();
        else
            _kilo.Focus();
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
        _browseFile = $"priem-{from:yyyyMMdd}.xlsx";
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
        _browseFile = $"meshki-{first.Kod}.xlsx";
        ShowBrowse(
            UiText.VisitHead(first.Kod, visit.Length, first.TrackCode),
            [UiText.ColBag, UiText.ColKilo, UiText.ColType, UiText.ColInsurance, UiText.ColStore, UiText.ColTime],
            visit.Select(bag => new[]
            {
                bag.KodId,
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
        _browseFile = $"nesotpravleno-{DateTime.Now:yyyyMMdd-HHmm}.xlsx";
        var bags = _store.UnsyncedBags();
        ShowBrowse(
            UiText.UnsyncedTitle(bags.Count),
            [UiText.ColBag, UiText.ColCode, UiText.ColKilo, UiText.ColType, UiText.ColStore, UiText.ColTrack, UiText.ColTime, UiText.ColError],
            bags.Select(bag => new[]
            {
                bag.KodId,
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
        var today = Theme.Command(UiText.ReportToday, Theme.Header, 130);
        var week = Theme.Command(UiText.ReportWeek, Theme.Header, 130);
        var month = Theme.Command(UiText.ReportMonth, Theme.Header, 130);
        var excel = Theme.Command(UiText.ExcelSave, Theme.Ok, 200);
        var goods = Theme.Command(UiText.ExportContents, Color.FromArgb(36, 92, 130), 230);
        var reprint = Theme.Command(UiText.ReprintReceipt, Theme.Header, 280);
        var toClient = Theme.Command(UiText.SendReceipt, Color.FromArgb(34, 158, 217), 260);
        var back = Theme.Command(UiText.BackMenu, Color.FromArgb(90, 104, 116), 150);
        _bind.Add(() =>
        {
            search.Text = UiText.ReportSearch;
            today.Text = UiText.ReportToday;
            week.Text = UiText.ReportWeek;
            month.Text = UiText.ReportMonth;
            excel.Text = UiText.ExcelSave;
            goods.Text = UiText.ExportContents;
            reprint.Text = UiText.ReprintReceipt;
            toClient.Text = UiText.SendReceipt;
            back.Text = UiText.BackMenu;
        });
        search.Click += (_, _) => SearchReports();
        today.Click += (_, _) => ApplyReportPeriod(DateTime.Today, DateTime.Today);
        week.Click += (_, _) =>
        {
            var shift = ((int)DateTime.Today.DayOfWeek + 6) % 7;
            ApplyReportPeriod(DateTime.Today.AddDays(-shift), DateTime.Today);
        };
        month.Click += (_, _) => ApplyReportPeriod(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), DateTime.Today);
        excel.Click += (_, _) => SaveReportExcel();
        goods.Click += (_, _) => SaveReportItems();
        reprint.Click += (_, _) => ReprintStoreReceipt();
        toClient.Click += (_, _) => SendReceiptToClient();
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
        var periods = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 58,
            WrapContents = false,
            Padding = new Padding(12, 0, 12, 8),
            BackColor = Theme.Bg,
        };
        today.Margin = new Padding(0, 4, 8, 0);
        week.Margin = new Padding(0, 4, 8, 0);
        month.Margin = new Padding(0, 4, 8, 0);
        excel.Margin = new Padding(12, 4, 0, 0);
        periods.Controls.Add(today);
        periods.Controls.Add(week);
        periods.Controls.Add(month);
        periods.Controls.Add(excel);
        goods.Margin = new Padding(8, 4, 0, 0);
        periods.Controls.Add(goods);

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
        toClient.Margin = new Padding(0, 0, 8, 0);
        bar.Controls.Add(toClient);
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
        page.Controls.Add(periods);
        page.Controls.Add(filters);
        return page;
    }

    private void ApplyReportPeriod(DateTime from, DateTime to)
    {
        _reportFrom.Value = from;
        _reportTo.Value = to;
        SearchReports();
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
        var bags = _store.SearchBags(_reportFrom.Value, _reportTo.Value, _reportStore.Text, _reportCode.Text)
            .OrderByDescending(bag => bag.Tarih)
            .ThenBy(bag => bag.LocalId)
            .ToList();
        _reportBags.Clear();
        _reportBags.AddRange(bags);
        _reportTitle.Text = UiText.ReportTitle(bags.Count);
        _reportGrid.Columns.Clear();
        _reportGrid.Rows.Clear();
        foreach (var header in ReportHeaders())
        {
            var index = _reportGrid.Columns.Add(header, header);
            _reportGrid.Columns[index].SortMode = DataGridViewColumnSortMode.NotSortable;
        }

        foreach (var bag in bags)
        {
            var row = _reportGrid.Rows.Add(ReportCells(bag).Cast<object>().ToArray());
            _reportGrid.Rows[row].Tag = bag;
        }
    }

    private static string[] ReportHeaders() =>
    [
        UiText.ColTime,
        UiText.ColBag,
        UiText.ColCode,
        UiText.ColKilo,
        UiText.ColType,
        UiText.ColStore,
        UiText.ColTrack,
        UiText.ColInsurance,
        UiText.Note,
        UiText.OfficeColumn,
        UiText.OperatorColumn,
        UiText.ColServer,
    ];

    private static string[] ReportCells(BagRecord bag) =>
    [
        bag.Tarih.ToString("dd.MM.yyyy HH:mm"),
        bag.KodId.ToString(CultureInfo.InvariantCulture),
        bag.Kod.ToString(CultureInfo.InvariantCulture),
        bag.Kilo,
        BolumChoice.TitleOf(bag.Bolum),
        bag.StoreName,
        bag.TrackCode,
        bag.Sigorta.ToString(CultureInfo.InvariantCulture),
        bag.Not1,
        bag.Office,
        bag.OperatorName,
        bag.Synced ? UiText.OnServer : string.IsNullOrEmpty(bag.SyncError) ? UiText.InQueue : bag.SyncError,
    ];

    private void SaveReportExcel()
    {
        var name = $"otchet-{_reportFrom.Value:yyyyMMdd}-{_reportTo.Value:yyyyMMdd}.xlsx";
        SaveExcel(name, ReportHeaders(), _reportBags.Select(ReportCells));
    }

    private void SaveReportItems()
    {
        var rows = new List<string[]>();
        foreach (var bag in _reportBags)
        {
            foreach (var item in _store.GetItems(bag.LocalId))
            {
                rows.Add(
                [
                    bag.KodId,
                    item.Line.ToString(CultureInfo.InvariantCulture),
                    item.Name,
                    item.Qty,
                    item.Unit,
                    bag.TrackCode,
                    bag.Tarih.ToString("dd.MM.yyyy HH:mm"),
                    BolumChoice.TitleOf(bag.Bolum),
                    bag.Kod.ToString(CultureInfo.InvariantCulture),
                ]);
            }
        }

        var tr = AppLanguage.Turkish;
        SaveExcel(
            $"mal-{_reportFrom.Value:yyyyMMdd}-{_reportTo.Value:yyyyMMdd}.xlsx",
            [
                UiText.ColBag,
                tr ? "Satır" : "Строка",
                tr ? "Ürün" : "Товар",
                tr ? "Miktar" : "Количество",
                tr ? "Birim" : "Единица",
                UiText.ColTrack,
                UiText.ColTime,
                UiText.ColType,
                UiText.ColCode,
            ],
            rows);
    }

    private void ReprintStoreReceipt()
    {
        if (_reportGrid.CurrentRow?.Tag is not BagRecord picked)
        {
            MessageBox.Show(this, UiText.ReportPick, UiText.TitleReceipt, MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        stack.Controls.Add(MenuButton(() => UiText.AcceptCargo, () => ShowWork()));
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
        var brand = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.White };
        _homeMark.Dock = DockStyle.Left;
        _homeCompany.Dock = DockStyle.Fill;
        brand.Controls.Add(_homeCompany);
        brand.Controls.Add(_homeMark);
        title.Dock = DockStyle.Top;
        _homeReport.Dock = DockStyle.Fill;
        var card = Theme.CardPanel();
        card.Dock = DockStyle.Fill;
        card.Padding = new Padding(20, 12, 20, 16);
        card.Controls.Add(_homeReport);
        card.Controls.Add(title);
        card.Controls.Add(brand);
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
        _homeMark.Image ??= BrandLogo.ProgramMark();
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
        var excel = Theme.Command(UiText.ExcelSave, Theme.Ok, 200);
        _bind.Add(() => back.Text = UiText.BackMenu);
        _bind.Add(() => excel.Text = UiText.ExcelSave);
        back.Click += (_, _) =>
        {
            if (_browseBack is { } backTo)
                backTo();
            else
                ShowHome();
        };
        excel.Click += (_, _) => SaveBrowseExcel();
        back.Margin = new Padding(0, 4, 8, 0);
        excel.Margin = new Padding(0, 4, 0, 0);
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 64,
            WrapContents = false,
            Padding = new Padding(16, 8, 0, 8),
            BackColor = Theme.Bg,
        };
        bar.Controls.Add(back);
        bar.Controls.Add(excel);
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

    private void SaveBrowseExcel()
    {
        var headers = _browseGrid.Columns.Cast<DataGridViewColumn>().Select(column => column.HeaderText).ToArray();
        var rows = _browseGrid.Rows.Cast<DataGridViewRow>()
            .Where(row => !row.IsNewRow)
            .Select(row => row.Cells.Cast<DataGridViewCell>().Select(cell => cell.Value?.ToString() ?? "").ToArray());
        SaveExcel(_browseFile, headers, rows);
    }

    private void SaveExcel(string fileName, IReadOnlyList<string> headers, IEnumerable<string[]> rows)
    {
        var list = rows.ToList();
        if (headers.Count == 0 || list.Count == 0)
        {
            MessageBox.Show(this, UiText.ExcelEmpty, UiText.ExcelSave, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = UiText.ExcelFilter,
            FileName = fileName,
            AddExtension = true,
            DefaultExt = "xlsx",
            OverwritePrompt = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            ExcelSheet.Save(dialog.FileName, headers, list);
            MessageBox.Show(this, UiText.ExcelSaved(dialog.FileName), UiText.ExcelSave, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, UiText.ExcelFailed + ex.Message, UiText.ExcelSave, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
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
            fresh.Items = bag.Items;
            row.Tag = fresh;
            foreach (DataGridViewRow other in _grid.Rows)
            {
                if (other.Tag is ItemSlot slot && ReferenceEquals(slot.Bag, bag))
                    slot.Bag = fresh;
            }
            row.Cells["sync"].Value = fresh.Synced
                ? UiText.OnServer
                : string.IsNullOrEmpty(fresh.SyncError) ? UiText.InQueue : fresh.SyncError;
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
        if (e.KeyChar == ',')
        {
            e.KeyChar = '.';
            SetStatus(UiText.CommaNotAllowed, Theme.Danger);
        }
        if (e.KeyChar == '.' && _kilo.Text.Contains('.'))
        {
            e.Handled = true;
            return;
        }

        if (char.IsControl(e.KeyChar) || char.IsDigit(e.KeyChar) || e.KeyChar == '.')
            return;
        e.Handled = true;
    }
}
