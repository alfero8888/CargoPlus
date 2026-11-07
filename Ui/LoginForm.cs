namespace CargoPlus;

internal sealed class LoginForm : FullScreenForm
{
    private readonly LocalStore _store;
    private readonly List<AppUser> _users;
    private readonly ComboBox _level = new() { DropDownStyle = ComboBoxStyle.DropDownList, Font = Theme.Input, Height = 36 };
    private readonly ComboBox _category = new() { DropDownStyle = ComboBoxStyle.DropDownList, Font = Theme.Input, Height = 36 };
    private readonly ComboBox _name = new() { DropDownStyle = ComboBoxStyle.DropDownList, Font = Theme.Input, Height = 36 };
    private readonly TextBox _pin = new() { Font = Theme.Input, MaxLength = 4, UseSystemPasswordChar = true, BorderStyle = BorderStyle.FixedSingle };
    private readonly Panel _card = new() { Width = 500, Height = 640, BackColor = Theme.Card };

    private bool _closing;
    public AppUser? EnteredUser { get; private set; }

    public LoginForm(LocalStore store)
    {
        _store = store;
        _users = store.Operators().ToList();
        var company = _store.GetSettings().Company;
        Text = UiText.WindowTitle(company);
        Build(company);
        _level.SelectedIndexChanged += (_, _) => FillCategories();
        _category.SelectedIndexChanged += (_, _) => FillNames();
        _pin.KeyPress += DigitsOnly;
        _pin.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true;
            TryEnter();
        };
        FillLevels();
    }

    private void Build(string company)
    {
        var title = new Label
        {
            Text = string.IsNullOrWhiteSpace(company) ? AppRelease.Product : company.Trim(),
            Dock = DockStyle.Top,
            Height = 52,
            Font = Theme.Title,
            ForeColor = Theme.Header,
            TextAlign = ContentAlignment.BottomLeft,
            AutoEllipsis = true,
        };
        var subtitle = new Label
        {
            Text = UiText.LoginSubtitle,
            Dock = DockStyle.Top,
            Height = 28,
            Font = Theme.Ui,
            ForeColor = Theme.Muted,
        };
        var enter = Theme.Command(UiText.Enter, Theme.Accent, 180);
        var exit = Theme.Command(UiText.Exit, Color.FromArgb(90, 104, 116), 140);
        enter.Click += (_, _) => TryEnter();
        exit.Click += (_, _) => Close();
        var buttons = new FlowLayoutPanel
        {
            Height = 62,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 0),
        };
        buttons.Controls.Add(enter);
        buttons.Controls.Add(exit);

        var hint = new Label
        {
            Height = 44,
            Dock = DockStyle.Top,
            Font = Theme.Small,
            ForeColor = Theme.WarnFg,
            Text = _store.DefaultAdminPinStillSet() ? UiText.FirstPin : "",
        };

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(28, 8, 28, 16) };
        Theme.Stack(
            body,
            title,
            subtitle,
            Theme.Field(UiText.AccessLevel, _level),
            Theme.Field(UiText.Category, _category),
            Theme.Field(UiText.OperatorName, _name),
            Theme.Field(UiText.Pin, _pin),
            buttons,
            hint);
        _card.Controls.Add(body);
        Controls.Add(_card);
        Resize += (_, _) => CenterCard();
        CenterCard();
    }

    private void CenterCard()
    {
        _card.Left = Math.Max(0, (ClientSize.Width - _card.Width) / 2);
        _card.Top = Math.Max(0, (ClientSize.Height - _card.Height) / 2);
    }

    private void FillLevels()
    {
        _level.Items.Clear();
        foreach (var level in _users.Select(user => user.AccessLevel).Distinct())
            _level.Items.Add(new LevelItem(level));
        if (_level.Items.Count > 0)
            _level.SelectedIndex = 0;
    }

    private void FillCategories()
    {
        var level = (_level.SelectedItem as LevelItem)?.Code;
        _category.Items.Clear();
        foreach (var category in _users.Where(user => user.AccessLevel == level).Select(user => user.Category).Distinct())
            _category.Items.Add(new CategoryItem(category));
        if (_category.Items.Count > 0)
            _category.SelectedIndex = 0;
        else
            FillNames();
    }

    private void FillNames()
    {
        var level = (_level.SelectedItem as LevelItem)?.Code;
        var category = (_category.SelectedItem as CategoryItem)?.Value;
        _name.Items.Clear();
        foreach (var user in _users.Where(user => user.AccessLevel == level && user.Category == category))
            _name.Items.Add(user.Name);
        if (_name.Items.Count > 0)
            _name.SelectedIndex = 0;
    }

    private void TryEnter()
    {
        if (_closing)
            return;

        var name = _name.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(this, UiText.PickOperator, UiText.TitleLogin, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var user = _store.Authenticate(name, _pin.Text.Trim());
        if (user is null)
        {
            MessageBox.Show(this, UiText.BadPin, UiText.TitleLogin, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _pin.SelectAll();
            _pin.Focus();
            return;
        }

        _closing = true;
        EnteredUser = user;
        Close();
    }

    private sealed class CategoryItem
    {
        public CategoryItem(string value) => Value = value;
        public string Value { get; }
        public override string ToString() => UiText.CategoryTitle(Value);
    }

    private static void DigitsOnly(object? sender, KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            e.Handled = true;
    }
}
