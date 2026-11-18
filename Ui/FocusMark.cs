using System.Runtime.InteropServices;

namespace CargoPlus;

internal sealed class FocusMark : IMessageFilter
{
    public static readonly Color Paper = Color.FromArgb(255, 236, 176);
    private static readonly Color Ink = Color.FromArgb(0, 255, 40);
    private static readonly Dictionary<Control, Color> Idle = [];
    private static IntPtr _bitmap;
    private static int _bitmapHeight;
    private static int _bitmapWidth;

    public static void Install() => Application.AddMessageFilter(new FocusMark());

    public bool PreFilterMessage(ref Message message)
    {
        if (message.Msg == 0x0007)
            Queue(message.HWnd, show: true);
        else if (message.Msg == 0x0008)
            Queue(message.HWnd, show: false);
        return false;
    }

    private static void Queue(IntPtr handle, bool show)
    {
        var control = Control.FromHandle(handle) ?? Control.FromChildHandle(handle);
        if (control is not (TextBox or ComboBox) || control.IsDisposed)
            return;
        var caret = handle;
        try
        {
            control.BeginInvoke(() =>
            {
                if (control.IsDisposed)
                    return;
                if (show && !control.ContainsFocus)
                    return;
                if (show)
                    Show(control, caret);
                else if (Idle.TryGetValue(control, out var color))
                    control.BackColor = color;
            });
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void Show(Control control, IntPtr caret)
    {
        if (!Idle.ContainsKey(control))
            Idle[control] = control.BackColor.ToArgb() == Paper.ToArgb() ? SystemColors.Window : control.BackColor;
        control.BackColor = Paper;
        if (control is TextBox box)
        {
            if (!box.ReadOnly)
                Paint(caret, box.Font.Height, box.DeviceDpi);
            return;
        }

        if (control is ComboBox combo && combo.DropDownStyle != ComboBoxStyle.DropDownList)
            Paint(caret, combo.Font.Height, combo.DeviceDpi);
    }

    public static void Paint(IntPtr handle, int height, int dpi)
    {
        if (handle == IntPtr.Zero)
            return;
        height = Math.Clamp(height, 16, 120);
        var width = Math.Clamp((int)Math.Round(1.5 * dpi / 25.4), 4, 8);
        if (_bitmap == IntPtr.Zero || _bitmapHeight != height || _bitmapWidth != width)
        {
            if (_bitmap != IntPtr.Zero)
                DeleteObject(_bitmap);
            using var image = new Bitmap(width, height);
            using (var graphics = Graphics.FromImage(image))
                graphics.Clear(Ink);
            _bitmap = image.GetHbitmap();
            _bitmapHeight = height;
            _bitmapWidth = width;
        }

        DestroyCaret();
        CreateCaret(handle, _bitmap, 0, 0);
        ShowCaret(handle);
    }

    [DllImport("user32.dll")]
    private static extern bool CreateCaret(IntPtr hwnd, IntPtr bitmap, int width, int height);

    [DllImport("user32.dll")]
    private static extern bool ShowCaret(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool DestroyCaret();

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);
}

internal static class FieldTip
{
    private static readonly ToolTip Tip = new()
    {
        ShowAlways = true,
        UseAnimation = false,
        UseFading = false,
    };
    private static readonly Dictionary<Control, List<(Control control, Func<string> text)>> Watching = [];

    public static void Bind(Control control, Func<string> text)
    {
        control.MouseHover += (_, _) => Show(control, text());
        control.MouseLeave += (_, _) => Tip.Hide(control);
        if (control.Parent != null)
            Watch(control.Parent, control, text);
        control.ParentChanged += (_, _) =>
        {
            if (control.Parent != null)
                Watch(control.Parent, control, text);
        };
    }

    private static void Watch(Control parent, Control control, Func<string> text)
    {
        if (!Watching.TryGetValue(parent, out var list))
        {
            list = [];
            Watching[parent] = list;
            parent.MouseHover += (_, _) =>
            {
                var point = parent.PointToClient(Cursor.Position);
                foreach (var (child, tip) in list)
                {
                    if (child.Enabled || !child.Visible || !child.Bounds.Contains(point))
                        continue;
                    Show(child, tip());
                    return;
                }
            };
        }

        foreach (var item in list)
        {
            if (item.control == control)
                return;
        }
        list.Add((control, text));
    }

    private static void Show(Control control, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !control.IsHandleCreated)
            return;
        Tip.Show(value, control, 0, -Math.Max(24, control.Font.Height + 8), 5000);
    }
}

internal sealed class MarkedTextBox : TextBox
{
    private Color _idle = SystemColors.Window;
    private bool _haveIdle;

    public MarkedTextBox()
    {
        BorderStyle = BorderStyle.FixedSingle;
        Font = Theme.Input;
    }

    protected override void OnEnter(EventArgs e)
    {
        if (!_haveIdle)
        {
            _idle = BackColor.ToArgb() == FocusMark.Paper.ToArgb() ? SystemColors.Window : BackColor;
            _haveIdle = true;
        }

        BackColor = FocusMark.Paper;
        base.OnEnter(e);
    }

    protected override void OnLeave(EventArgs e)
    {
        BackColor = _haveIdle ? _idle : SystemColors.Window;
        base.OnLeave(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        BeginInvoke(() =>
        {
            if (!IsDisposed && Focused)
                FocusMark.Paint(Handle, Font.Height, DeviceDpi);
        });
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if ((m.Msg == 0x0007 || m.Msg == 0x0202) && Focused && !ReadOnly)
            FocusMark.Paint(Handle, Font.Height, DeviceDpi);
    }
}
