namespace CargoPlus;

internal static class AppTray
{
    private static NotifyIcon? _notify;
    private static Icon? _owned;
    private static Form? _owner;

    public static void Attach(Form form)
    {
        _owner = form;
        _notify ??= Build();
        Use(form.Icon);
        _notify.Visible = true;
    }

    public static void Use(Icon? icon)
    {
        if (_notify is null || icon is null)
            return;

        var copy = (Icon)icon.Clone();
        _notify.Icon = copy;
        var previous = _owned;
        _owned = copy;
        previous?.Dispose();
    }

    public static void Dispose()
    {
        _notify?.Dispose();
        _notify = null;
        _owned?.Dispose();
        _owned = null;
    }

    private static NotifyIcon Build()
    {
        var notify = new NotifyIcon
        {
            Text = "Cargo Plus",
            Visible = true,
        };
        notify.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                Show();
        };

        var menu = new ContextMenuStrip();
        var open = new ToolStripMenuItem("Cargo Plus");
        open.Click += (_, _) => Show();
        var exit = new ToolStripMenuItem("Выход");
        exit.Click += (_, _) => _owner?.Close();
        menu.Opening += (_, _) =>
        {
            open.Text = UiText.OpenApp;
            exit.Text = UiText.Exit;
        };
        menu.Items.Add(open);
        menu.Items.Add(exit);
        notify.ContextMenuStrip = menu;
        return notify;
    }

    private static void Show()
    {
        if (_owner is not { IsDisposed: false } form)
            return;

        if (form.WindowState == FormWindowState.Minimized)
            form.WindowState = FormWindowState.Maximized;
        form.Show();
        form.Activate();
    }
}
