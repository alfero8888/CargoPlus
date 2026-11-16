namespace CargoPlus;

internal sealed class BagContentsPanel : Panel
{
    private readonly FlowLayoutPanel _rows = new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        BackColor = Color.White,
    };
    private readonly AutoCompleteStringCollection _names = new();
    private readonly List<ItemRow> _lines = [];
    private readonly Button _add = new() { Text = "+", Width = 36, Height = 32, Font = Theme.UiBold };

    public int LineCount => _lines.Count;
    public event Action? LeaveToKilos;
    public event Action? RowsChanged;

    public BagContentsPanel()
    {
        AutoSize = false;
        BackColor = Color.White;
        Padding = new Padding(0, 4, 0, 4);
        _add.Click += (_, _) => AddEmpty();
        Controls.Add(_add);
        Controls.Add(_rows);
        _add.Dock = DockStyle.Top;
        Resize += (_, _) =>
        {
            foreach (var row in _lines)
                row.Width = Math.Max(280, _rows.ClientSize.Width - 8);
        };
    }

    public void ApplyLanguage()
    {
        foreach (var row in _lines)
            row.ApplyLanguage();
    }

    public void SetNames(IEnumerable<string> names)
    {
        _names.Clear();
        foreach (var name in names)
            _names.Add(name);
        foreach (var row in _lines)
            row.Names = _names;
    }

    public void FocusFirst()
    {
        if (_lines.Count == 0)
            AddEmpty();
        _lines[0].FocusName();
    }

    public void Clear()
    {
        _rows.Controls.Clear();
        _lines.Clear();
        AddEmpty();
    }

    public void LoadLines(IReadOnlyList<BagItem> items)
    {
        _rows.Controls.Clear();
        _lines.Clear();
        foreach (var item in items.Take(CargoRules.ItemLineMax))
            AddRow(item.Name, item.Qty, item.Unit);
        if (_lines.Count == 0)
            AddRow("", "", "adet");
        Reflow();
    }

    public bool TryRead(out List<BagItem> items, out string error)
    {
        items = [];
        var line = 1;
        foreach (var row in _lines)
        {
            var name = row.NameText.Trim();
            var qtyText = row.QtyText.Trim();
            if (name.Length == 0 && qtyText.Length == 0)
                continue;
            if (name.Length == 0)
            {
                error = UiText.ContentsRequired;
                row.FocusName();
                return false;
            }

            if (name.Length > CargoRules.ItemNameMax)
            {
                error = UiText.ItemNameLong;
                row.FocusName();
                return false;
            }

            if (!CargoRules.TryItemQty(qtyText, out var qty, out error))
            {
                row.FocusQty();
                return false;
            }

            items.Add(new BagItem { Line = line, Name = name, Qty = qty, Unit = row.UnitCode });
            line++;
        }

        if (items.Count > CargoRules.ItemLineMax)
        {
            error = UiText.ItemLinesMax;
            return false;
        }

        error = "";
        return true;
    }

    private void AddEmpty()
    {
        if (_lines.Count >= CargoRules.ItemLineMax)
        {
            MessageBox.Show(this, UiText.ItemLinesMax, UiText.ContentsTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var row = AddRow("", "", _lines.Count == 0 ? "adet" : _lines[^1].UnitCode);
        Reflow();
        row.FocusName();
    }

    private ItemRow AddRow(string name, string qty, string unit)
    {
        var row = new ItemRow(_names);
        row.Width = Math.Max(280, _rows.ClientSize.Width - 8);
        row.NameText = name;
        row.QtyText = qty;
        row.UnitCode = unit;
        row.EnterName += () => OnNameEnter(row);
        row.EnterQty += () => row.FocusUnit();
        row.EnterUnit += () => OnUnitEnter(row);
        row.RemoveRequested += () => Remove(row);
        _lines.Add(row);
        _rows.Controls.Add(row);
        return row;
    }

    private void OnNameEnter(ItemRow row)
    {
        if (row.NameText.Trim().Length == 0)
        {
            LeaveToKilos?.Invoke();
            return;
        }

        row.FocusQty();
    }

    private void OnUnitEnter(ItemRow row)
    {
        var index = _lines.IndexOf(row);
        if (index >= 0 && index < _lines.Count - 1)
            _lines[index + 1].FocusName();
        else
            LeaveToKilos?.Invoke();
    }

    private void Remove(ItemRow row)
    {
        if (_lines.Count == 1)
        {
            row.NameText = "";
            row.QtyText = "";
            row.FocusName();
            return;
        }

        var index = _lines.IndexOf(row);
        _lines.Remove(row);
        _rows.Controls.Remove(row);
        row.Dispose();
        Reflow();
        _lines[Math.Max(0, Math.Min(index, _lines.Count - 1))].FocusName();
    }

    private void Reflow()
    {
        var width = Math.Max(280, ClientSize.Width - 8);
        foreach (var row in _lines)
            row.Width = width;
        Height = Math.Max(44, _lines.Count * 40 + _add.Height + 8);
        RowsChanged?.Invoke();
    }

    private sealed class ItemRow : Panel
    {
        private readonly TextBox _name = Theme.Box();
        private readonly TextBox _qty = Theme.Box();
        private readonly ComboBox _unit = new() { DropDownStyle = ComboBoxStyle.DropDownList, Font = Theme.Input, Width = 110 };
        private readonly Button _drop = new() { Text = "×", Width = 36, Font = Theme.UiBold };
        private bool _qtyEdit;

        public event Action? EnterName;
        public event Action? EnterQty;
        public event Action? EnterUnit;
        public event Action? RemoveRequested;

        public ItemRow(AutoCompleteStringCollection names)
        {
            Height = 36;
            Margin = new Padding(0, 2, 0, 2);
            _name.Width = 280;
            _name.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _name.AutoCompleteSource = AutoCompleteSource.CustomSource;
            _name.AutoCompleteCustomSource = names;
            _qty.Width = 90;
            FillUnits();
            _unit.SelectedIndex = 0;
            _name.KeyDown += (_, e) =>
            {
                if (e.KeyCode != Keys.Enter)
                    return;
                e.SuppressKeyPress = true;
                EnterName?.Invoke();
            };
            _qty.KeyPress += QtyChar;
            _qty.TextChanged += (_, _) => NormalizeQty();
            _qty.KeyDown += (_, e) =>
            {
                if (e.KeyCode != Keys.Enter)
                    return;
                e.SuppressKeyPress = true;
                EnterQty?.Invoke();
            };
            _unit.KeyDown += (_, e) =>
            {
                if (e.KeyCode != Keys.Enter)
                    return;
                e.SuppressKeyPress = true;
                EnterUnit?.Invoke();
            };
            _drop.Click += (_, _) => RemoveRequested?.Invoke();
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
            };
            flow.Controls.Add(_name);
            flow.Controls.Add(_qty);
            flow.Controls.Add(_unit);
            flow.Controls.Add(_drop);
            Controls.Add(flow);
            Resize += (_, _) => _name.Width = Math.Max(120, Width - 260);
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public AutoCompleteStringCollection? Names
        {
            set => _name.AutoCompleteCustomSource = value;
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string NameText
        {
            get => _name.Text;
            set => _name.Text = value;
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string QtyText
        {
            get => _qty.Text;
            set => _qty.Text = value;
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string UnitCode
        {
            get => _unit.SelectedIndex switch
            {
                1 => "kg",
                2 => "litre",
                _ => "adet",
            };
            set => _unit.SelectedIndex = CargoRules.ItemUnit(value) switch
            {
                "kg" => 1,
                "litre" => 2,
                _ => 0,
            };
        }

        public void FocusName() => _name.Focus();
        public void FocusQty() => _qty.Focus();
        public void FocusUnit() => _unit.Focus();

        public void ApplyLanguage()
        {
            var code = UnitCode;
            FillUnits();
            UnitCode = code;
        }

        private void FillUnits()
        {
            _unit.Items.Clear();
            _unit.Items.Add(UiText.UnitAdet);
            _unit.Items.Add(UiText.UnitKg);
            _unit.Items.Add(UiText.UnitLitre);
        }

        private void QtyChar(object? sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == ',')
                e.KeyChar = '.';
            if (e.KeyChar == '.' && _qty.Text.Contains('.'))
            {
                e.Handled = true;
                return;
            }

            if (char.IsControl(e.KeyChar) || char.IsDigit(e.KeyChar) || e.KeyChar == '.')
                return;
            e.Handled = true;
        }

        private void NormalizeQty()
        {
            if (_qtyEdit || !_qty.Text.Contains(','))
                return;
            _qtyEdit = true;
            var caret = _qty.SelectionStart;
            var text = _qty.Text.Replace(',', '.');
            _qty.Text = text;
            _qty.SelectionStart = Math.Min(caret, text.Length);
            _qtyEdit = false;
        }
    }
}
