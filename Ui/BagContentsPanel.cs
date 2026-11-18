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
        Padding = new Padding(0, 0, 0, 4);
        _add.Click += (_, _) => AddEmpty();
        FieldTip.Bind(_add, () => UiText.AddGoodsTip);
        _add.Dock = DockStyle.None;
        Controls.Add(_rows);
        Controls.Add(_add);
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
        _lines[0].FocusInvoice();
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
            AddRow(item.Name, item.Qty, item.Unit, item.InvoiceNo, CargoRules.InvoiceDateText(item.InvoiceDate));
        if (_lines.Count == 0)
            AddRow("", "", "adet", "", "");
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

            if (!CargoRules.TryInvoice(row.InvoiceText, row.InvoiceDateText, out var invoiceNo, out var invoiceDate, out error))
            {
                if (error == UiText.InvoiceDateBad)
                    row.FocusInvoiceDate();
                else
                    row.FocusInvoice();
                return false;
            }

            row.InvoiceDateText = CargoRules.InvoiceDateText(invoiceDate);
            items.Add(new BagItem
            {
                Line = line,
                Name = name,
                Qty = qty,
                Unit = row.UnitCode,
                InvoiceNo = invoiceNo,
                InvoiceDate = invoiceDate,
            });
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

        var row = AddRow("", "", _lines.Count == 0 ? "adet" : _lines[^1].UnitCode, "", "");
        Reflow();
        row.FocusInvoice();
    }

    private ItemRow AddRow(string name, string qty, string unit, string invoice, string invoiceDate)
    {
        var row = new ItemRow(_names);
        row.Width = Math.Max(280, _rows.ClientSize.Width - 8);
        row.NameText = name;
        row.QtyText = qty;
        row.UnitCode = unit;
        row.InvoiceText = invoice;
        row.InvoiceDateText = invoiceDate;
        row.EnterInvoice += () => row.FocusInvoiceDate();
        row.EnterDate += () => row.FocusName();
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
            _lines[index + 1].FocusInvoice();
        else
            LeaveToKilos?.Invoke();
    }

    private void Remove(ItemRow row)
    {
        if (_lines.Count == 1)
        {
            row.NameText = "";
            row.QtyText = "";
            row.InvoiceText = "";
            row.InvoiceDateText = "";
            row.FocusInvoice();
            return;
        }

        var index = _lines.IndexOf(row);
        _lines.Remove(row);
        _rows.Controls.Remove(row);
        row.Dispose();
        Reflow();
        _lines[Math.Max(0, Math.Min(index, _lines.Count - 1))].FocusInvoice();
    }

    private void Reflow()
    {
        var width = Math.Max(640, ClientSize.Width - 8);
        for (var i = 0; i < _lines.Count; i++)
        {
            _lines[i].ShowCaptions = false;
            _lines[i].Width = width;
        }

        _rows.PerformLayout();
        var gap = Math.Max(8, (int)Math.Round(3.0 * DeviceDpi / 25.4));
        _add.Location = new Point(0, _rows.Bottom + gap);
        Height = _add.Bottom + Padding.Bottom + 4;
        RowsChanged?.Invoke();
    }

    private sealed class ItemRow : Panel
    {
        private readonly TextBox _name = Theme.Box();
        private readonly TextBox _qty = Theme.Box();
        private readonly ComboBox _unit = new() { DropDownStyle = ComboBoxStyle.DropDownList, Font = Theme.Input, Width = 110, FlatStyle = FlatStyle.Flat };
        private readonly TextBox _invoice = Theme.Box();
        private readonly DateTimePicker _invoiceDate = new()
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd.MM.yyyy",
            ShowCheckBox = true,
            Checked = false,
            Width = 158,
            Font = Theme.Input,
        };
        private readonly Button _drop = new() { Text = "×", Width = 36, Font = Theme.UiBold };
        private readonly FlowLayoutPanel _captions = new() { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Height = 18, Margin = new Padding(0) };
        private readonly Label _capInvoice = Caption();
        private readonly Label _capDate = Caption();
        private readonly Label _capName = Caption();
        private readonly Label _capQty = Caption();
        private readonly Label _capUnit = Caption();
        private bool _qtyEdit;

        public event Action? EnterName;
        public event Action? EnterQty;
        public event Action? EnterUnit;
        public event Action? EnterInvoice;
        public event Action? EnterDate;
        public event Action? RemoveRequested;

        public ItemRow(AutoCompleteStringCollection names)
        {
            Height = 44;
            Margin = new Padding(0, 2, 0, 2);
            _name.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _name.AutoCompleteSource = AutoCompleteSource.CustomSource;
            _name.AutoCompleteCustomSource = names;
            _name.Dock = DockStyle.None;
            _qty.Dock = DockStyle.None;
            _invoice.Dock = DockStyle.None;
            _invoiceDate.Dock = DockStyle.None;
            _invoice.Width = 160;
            _invoiceDate.Width = 158;
            _invoiceDate.Height = 32;
            _qty.Width = 90;
            _qty.Font = Theme.Digits;
            _invoice.Font = Theme.Digits;
            _name.Width = 220;
            foreach (var box in new Control[] { _invoice, _invoiceDate, _name, _qty, _unit, _drop })
                box.Margin = new Padding(0, 0, 6, 0);
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
            _invoice.KeyDown += (_, e) =>
            {
                if (e.KeyCode != Keys.Enter)
                    return;
                e.SuppressKeyPress = true;
                EnterInvoice?.Invoke();
            };
            _invoiceDate.KeyDown += (_, e) =>
            {
                if (e.KeyCode != Keys.Enter)
                    return;
                e.SuppressKeyPress = true;
                EnterDate?.Invoke();
            };
            _drop.Click += (_, _) => RemoveRequested?.Invoke();
            _unit.Enter += (_, _) => _unit.BackColor = FocusMark.Paper;
            _unit.Leave += (_, _) => _unit.BackColor = Color.White;
            _captions.Controls.AddRange([_capInvoice, _capDate, _capName, _capQty, _capUnit]);
            var boxes = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0),
            };
            boxes.Controls.Add(_invoice);
            boxes.Controls.Add(_invoiceDate);
            boxes.Controls.Add(_name);
            boxes.Controls.Add(_qty);
            boxes.Controls.Add(_unit);
            boxes.Controls.Add(_drop);
            _captions.Dock = DockStyle.Top;
            _captions.Visible = false;
            Controls.Add(boxes);
            Controls.Add(_captions);
            FieldTip.Bind(_invoice, () => UiText.TipInvoice);
            FieldTip.Bind(_invoiceDate, () => UiText.InvoiceDateFull);
            FieldTip.Bind(_name, () => UiText.TipGoodsName);
            FieldTip.Bind(_qty, () => UiText.TipQty);
            FieldTip.Bind(_unit, () => UiText.TipUnit);
            FieldTip.Bind(_drop, () => UiText.RemoveLineTip);
            ApplyLanguage();
            Resize += (_, _) => FitName();
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool ShowCaptions
        {
            get => _captions.Visible;
            set
            {
                _captions.Visible = value;
                Height = (value ? _captions.Height : 0) + 44;
            }
        }

        private void FitName()
        {
            var used = _invoice.Width + _invoiceDate.Width + _qty.Width + _unit.Width + _drop.Width + 48;
            var name = Math.Max(140, Width - used);
            _name.Width = name;
            _capInvoice.Width = _invoice.Width;
            _capDate.Width = _invoiceDate.Width;
            _capName.Width = name;
            _capQty.Width = _qty.Width;
            _capUnit.Width = _unit.Width;
        }

        private static Label Caption() => new()
        {
            Height = 18,
            AutoEllipsis = true,
            Font = new Font("Segoe UI", 8f),
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.BottomLeft,
            Margin = new Padding(0, 0, 6, 0),
        };

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
        public string InvoiceText
        {
            get => _invoice.Text;
            set => _invoice.Text = value;
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string InvoiceDateText
        {
            get => _invoiceDate.Checked
                ? _invoiceDate.Value.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture)
                : "";
            set
            {
                if (string.IsNullOrWhiteSpace(value)
                    || !DateTime.TryParseExact(value.Trim(), ["dd.MM.yyyy", "yyyy-MM-dd"], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var day))
                {
                    _invoiceDate.Checked = false;
                    return;
                }

                _invoiceDate.Value = day;
                _invoiceDate.Checked = true;
            }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string UnitCode
        {
            get => _unit.SelectedIndex switch
            {
                1 => "kg",
                2 => "litre",
                3 => "mt",
                4 => "cift",
                _ => "adet",
            };
            set => _unit.SelectedIndex = CargoRules.ItemUnit(value) switch
            {
                "kg" => 1,
                "litre" => 2,
                "mt" => 3,
                "cift" => 4,
                _ => 0,
            };
        }

        public void FocusName() => _name.Focus();
        public void FocusQty() => _qty.Focus();
        public void FocusUnit() => _unit.Focus();
        public void FocusInvoice() => _invoice.Focus();
        public void FocusInvoiceDate() => _invoiceDate.Focus();

        public void ApplyLanguage()
        {
            var code = UnitCode;
            FillUnits();
            UnitCode = code;
            _capInvoice.Text = UiText.InvoiceNoLabel;
            _capDate.Text = UiText.InvoiceDateFull;
            _capName.Text = UiText.GoodsName;
            _capQty.Text = UiText.GoodsQty;
            _capUnit.Text = UiText.GoodsUnit;
            _invoice.PlaceholderText = UiText.InvoiceNoLabel;
            _name.PlaceholderText = UiText.GoodsName;
            _qty.PlaceholderText = UiText.GoodsQty;
            FitName();
        }

        private void FillUnits()
        {
            _unit.Items.Clear();
            _unit.Items.Add(UiText.UnitAdet);
            _unit.Items.Add(UiText.UnitKg);
            _unit.Items.Add(UiText.UnitLitre);
            _unit.Items.Add(UiText.UnitMetre);
            _unit.Items.Add(UiText.UnitPair);
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
