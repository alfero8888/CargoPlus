namespace CargoPlus;

internal sealed class LoginForm : FullScreenForm
{
    private readonly LocalStore _store;
    private readonly List<AppUser> _users;
    private readonly ComboBox _level = new() { DropDownStyle = ComboBoxStyle.DropDownList, Font = Theme.Input, Height = 36 };
    private readonly ComboBox _name = new() { DropDownStyle = ComboBoxStyle.DropDownList, Font = Theme.Input, Height = 36 };
    private readonly TextBox _pin = new() { Font = Theme.Input, MaxLength = 4, UseSystemPasswordChar = true, BorderStyle = BorderStyle.FixedSingle };
    private readonly Panel _card = new() { Width = 520, Height = 520, BackColor = Theme.Card };

    private bool _closing;
    private readonly string _savedLevel;
    private readonly string _savedName;
    public AppUser? EnteredUser { get; private set; }

    public LoginForm(LocalStore store)
    {
        _store = store;
        _users = store.Operators().ToList();
        (_savedLevel, _, _savedName) = store.LastLogin();
        var company = _store.GetSettings().Company;
        Text = UiText.WindowTitle(company);
        Build(company);
        _level.SelectedIndexChanged += (_, _) => FillNames();
        _pin.KeyPress += DigitsOnly;
        _pin.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true;
            TryEnter();
        };
        FillLevels();
        Shown += (_, _) => _pin.Focus();
    }

    private void Build(string company)
    {
        var mark = new PictureBox
        {
            Image = BrandLogo.ProgramMark(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Width = 56,
            Height = 56,
            Margin = new Padding(0, 8, 12, 0),
        };
        var title = new Label
        {
            Text = string.IsNullOrWhiteSpace(company) ? AppRelease.Product : company.Trim(),
            AutoSize = false,
            Width = 360,
            Height = 56,
            Font = Theme.Title,
            ForeColor = Theme.Header,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Margin = new Padding(0, 8, 0, 0),
        };
        var heading = new FlowLayoutPanel
        {
            Height = 72,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Dock = DockStyle.Top,
        };
        heading.Controls.Add(mark);
        heading.Controls.Add(title);
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
            heading,
            subtitle,
            Theme.Field(UiText.AccessLevel, _level),
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
        SelectSaved(_level, item => item is LevelItem level && level.Code == _savedLevel);
        FillNames();
    }

    private void FillNames()
    {
        var level = (_level.SelectedItem as LevelItem)?.Code;
        _name.Items.Clear();
        foreach (var user in _users.Where(user => user.AccessLevel == level))
            _name.Items.Add(user.Name);
        SelectSaved(
            _name,
            item => item is string name
                && level == _savedLevel
                && string.Equals(name, _savedName, StringComparison.OrdinalIgnoreCase));
    }

    private static void SelectSaved(ComboBox box, Func<object, bool> match)
    {
        if (box.Items.Count == 0)
            return;

        for (var i = 0; i < box.Items.Count; i++)
        {
            if (match(box.Items[i]!))
            {
                box.SelectedIndex = i;
                return;
            }
        }

        if (box.SelectedIndex < 0)
            box.SelectedIndex = 0;
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

        _store.RememberLogin(user.AccessLevel, user.Category, user.Name);
        _closing = true;
        EnteredUser = user;
        Close();
    }

    private static void DigitsOnly(object? sender, KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            e.Handled = true;
    }
}
