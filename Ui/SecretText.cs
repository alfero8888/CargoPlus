namespace CargoPlus;

internal sealed class SecretText : IDisposable
{
    private readonly TextBox _box;
    private readonly System.Windows.Forms.Timer _timer;
    private string _value = "";
    private int _caret;

    public SecretText(TextBox box)
    {
        _box = box;
        _box.UseSystemPasswordChar = false;
        _box.ShortcutsEnabled = false;
        _timer = new System.Windows.Forms.Timer { Interval = 650 };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            Show(peek: false);
        };
        _box.KeyPress += OnPress;
        _box.KeyDown += OnKey;
        _box.MouseUp += (_, _) => _caret = _box.SelectionStart;
        _box.Leave += (_, _) =>
        {
            _timer.Stop();
            Show(peek: false);
        };
    }

    public string Value => _value;

    public void Set(string? value)
    {
        _value = value ?? "";
        _caret = _value.Length;
        _timer.Stop();
        Show(peek: false);
    }

    public void Dispose() => _timer.Dispose();

    private void OnPress(object? sender, KeyPressEventArgs e)
    {
        if (char.IsControl(e.KeyChar))
            return;
        e.Handled = true;
        Insert(e.KeyChar.ToString());
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Back)
        {
            e.SuppressKeyPress = true;
            Delete(backward: true);
        }
        else if (e.KeyCode == Keys.Delete)
        {
            e.SuppressKeyPress = true;
            Delete(backward: false);
        }
        else if (e.Control && e.KeyCode == Keys.A)
        {
            e.SuppressKeyPress = true;
            _box.SelectAll();
            _caret = _value.Length;
        }
        else if (e.Control && e.KeyCode == Keys.V)
        {
            e.SuppressKeyPress = true;
            if (Clipboard.ContainsText())
                Insert(Clipboard.GetText());
        }
        else if (e.Control && e.KeyCode is Keys.C or Keys.X)
        {
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode is Keys.Left or Keys.Right or Keys.Home or Keys.End)
        {
            _caret = _box.SelectionStart;
        }
    }

    private void Insert(string text)
    {
        text = text.Replace("\r", "").Replace("\n", "");
        if (text.Length == 0)
            return;
        var start = Math.Clamp(_box.SelectionStart, 0, _value.Length);
        var length = Math.Clamp(_box.SelectionLength, 0, _value.Length - start);
        _value = _value.Remove(start, length).Insert(start, text);
        _caret = start + text.Length;
        Show(peek: true);
        _timer.Stop();
        _timer.Start();
    }

    private void Delete(bool backward)
    {
        var start = Math.Clamp(_box.SelectionStart, 0, _value.Length);
        var length = Math.Clamp(_box.SelectionLength, 0, _value.Length - start);
        if (length > 0)
        {
            _value = _value.Remove(start, length);
            _caret = start;
        }
        else if (backward && start > 0)
        {
            _value = _value.Remove(start - 1, 1);
            _caret = start - 1;
        }
        else if (!backward && start < _value.Length)
        {
            _value = _value.Remove(start, 1);
            _caret = start;
        }

        _timer.Stop();
        Show(peek: false);
    }

    private void Show(bool peek)
    {
        var shown = new string('●', _value.Length);
        if (peek && _caret > 0 && _caret <= shown.Length)
            shown = shown[..(_caret - 1)] + _value[_caret - 1] + shown[_caret..];
        _box.Text = shown;
        _box.SelectionStart = Math.Clamp(_caret, 0, _box.Text.Length);
        _box.SelectionLength = 0;
    }
}
