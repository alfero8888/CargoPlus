namespace CargoPlus;

internal class FullScreenForm : Form
{
    protected FullScreenForm()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimizeBox = true;
        MaximizeBox = true;
        ShowInTaskbar = true;
        MinimumSize = new Size(1100, 700);
        BackColor = Theme.Bg;
        Font = Theme.Ui;
        KeyPreview = true;
        DoubleBuffered = true;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        BrandLogo.Apply(this);
    }
}

internal static class Theme
{
    public static readonly Color Header = Color.FromArgb(16, 48, 74);
    public static readonly Color Bg = Color.FromArgb(232, 238, 242);
    public static readonly Color Card = Color.White;
    public static readonly Color Line = Color.FromArgb(214, 222, 228);
    public static readonly Color Accent = Color.FromArgb(15, 111, 176);
    public static readonly Color Ok = Color.FromArgb(22, 122, 78);
    public static readonly Color Danger = Color.FromArgb(166, 52, 46);
    public static readonly Color WarnBg = Color.FromArgb(255, 244, 214);
    public static readonly Color WarnFg = Color.FromArgb(120, 72, 0);
    public static readonly Color Muted = Color.FromArgb(90, 104, 116);
    public static readonly Color Ink = Color.FromArgb(28, 40, 52);

    public static readonly Font Ui = new("Segoe UI", 11f);
    public static readonly Font UiBold = new("Segoe UI", 11f, FontStyle.Bold);
    public static readonly Font Small = new("Segoe UI", 9.5f);
    public static readonly Font Input = new("Segoe UI", 16f);
    public static readonly Font Title = new("Segoe UI", 18f, FontStyle.Bold);
    public static readonly Font Big = new("Segoe UI", 22f, FontStyle.Bold);

    public static Label Caption(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = false,
            Height = 22,
            Dock = DockStyle.Top,
            Font = Small,
            ForeColor = Muted,
            TextAlign = ContentAlignment.BottomLeft,
        };
    }

    public static Label Value(string text, float size = 16)
    {
        return new Label
        {
            Text = text,
            AutoSize = false,
            Height = size >= 18 ? 36 : 30,
            Dock = DockStyle.Top,
            Font = new Font("Segoe UI", size, FontStyle.Bold),
            ForeColor = Ink,
        };
    }

    public static TextBox Box()
    {
        return new TextBox
        {
            BorderStyle = BorderStyle.FixedSingle,
            Font = Input,
            Dock = DockStyle.Fill,
        };
    }

    public static Button Command(string text, Color back, int width)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 46,
            BackColor = back,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = UiBold,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = ControlPaint.Dark(back, 0.08f);
        return button;
    }

    public static Panel CardPanel()
    {
        var panel = new Panel
        {
            BackColor = Card,
            Padding = new Padding(16, 12, 16, 12),
        };
        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(Line);
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };
        return panel;
    }

    public static void Stack(Control parent, params Control[] rowsFromTop)
    {
        for (var i = rowsFromTop.Length - 1; i >= 0; i--)
        {
            rowsFromTop[i].Dock = DockStyle.Top;
            parent.Controls.Add(rowsFromTop[i]);
        }
    }

    public static Panel Field(string caption, Control input, int height = 68) =>
        Field(Caption(caption), input, height);

    public static Panel Field(Label caption, Control input, int height = 68)
    {
        var panel = new Panel { Height = height };
        input.Dock = DockStyle.Top;
        input.Height = height - 26;
        caption.Dock = DockStyle.Top;
        panel.Controls.Add(input);
        panel.Controls.Add(caption);
        return panel;
    }
}
