using System.Globalization;

namespace CargoPlus;

internal sealed class AboutForm : Form
{
    public AboutForm(Action closeApplication)
    {
        Text = UiText.AboutTitle;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(520, 360);
        Font = Theme.Ui;
        BackColor = Theme.Bg;

        var culture = AppLanguage.Turkish ? new CultureInfo("tr-TR") : new CultureInfo("ru-RU");
        var date = AppRelease.Released.ToString("d MMMM yyyy", culture);
        var facts = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(28, 24, 28, 8),
            BackColor = Theme.Bg,
        };
        facts.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        facts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddFact(facts, UiText.AboutProgram, AppRelease.Product);
        AddFact(facts, UiText.AboutDeveloper, AppRelease.Author);
        AddFact(facts, UiText.AboutDate, date);
        AddFact(facts, UiText.AboutVersion, AppRelease.Current.ToString());

        var check = Theme.Command(UiText.CheckUpdates, Theme.Accent, 260);
        var close = Theme.Command(UiText.Close, Color.FromArgb(90, 104, 116), 140);
        check.Click += (_, _) => _ = UpdateCheck.CheckAsync(this, () =>
        {
            var host = Owner;
            Close();
            if (host is not null && !host.IsDisposed)
                host.BeginInvoke(closeApplication);
        }, true);
        close.Click += (_, _) => Close();
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 72,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(16, 12, 16, 12),
            BackColor = Color.White,
        };
        bar.Controls.Add(close);
        bar.Controls.Add(check);
        Controls.Add(facts);
        Controls.Add(bar);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        BrandLogo.Apply(this);
    }

    private static void AddFact(TableLayoutPanel grid, string caption, string value)
    {
        var row = grid.RowCount;
        grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        var name = new Label
        {
            Text = caption,
            Dock = DockStyle.Fill,
            Font = Theme.Ui,
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        var text = new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            Font = Theme.UiBold,
            ForeColor = Theme.Ink,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };
        grid.Controls.Add(name, 0, row);
        grid.Controls.Add(text, 1, row);
    }
}
