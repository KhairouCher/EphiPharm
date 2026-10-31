using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Epipharm.App.Theme;

namespace Epipharm.App
{
    /// <summary>
    /// The F5 "pick from a list instead of typing a code" pattern used
    /// throughout the legacy app (see MENU_MAP.md "UI conventions") —
    /// one reusable picker instead of a bespoke list per lookup (products,
    /// suppliers, and anything else an entry screen needs to look up).
    /// Styled as the pale "browse" archetype, same as StockListForm.
    /// </summary>
    public class PickerForm<T> : Form where T : class
    {
        private readonly Func<string, List<T>> _search;
        private readonly Func<T, string> _label;
        private readonly TextBox _searchBox;
        private readonly ListBox _list;

        public T Selected { get; private set; }

        public PickerForm(string title, string searchHint, Func<string, List<T>> search, Func<T, string> label)
        {
            _search = search ?? throw new ArgumentNullException(nameof(search));
            _label = label ?? throw new ArgumentNullException(nameof(label));

            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(640, 480);
            MinimumSize = new Size(480, 360);
            KeyPreview = true;
            BackColor = RetroTheme.BrowseBackground;

            var searchBand = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = RetroTheme.BrowseBackground,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(12, 6, 12, 6)
            };
            searchBand.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            searchBand.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            searchBand.Controls.Add(new Label
            {
                Text = searchHint,
                Dock = DockStyle.Fill,
                ForeColor = RetroTheme.BrowseText,
                Font = RetroTheme.DosFont(9F)
            }, 0, 0);
            _searchBox = new TextBox { Dock = DockStyle.Fill, Font = RetroTheme.DosFont(12F) };
            _searchBox.TextChanged += (s, e) => RunSearch();
            searchBand.Controls.Add(_searchBox, 0, 1);

            _list = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = RetroTheme.DosFont(11F),
                BackColor = RetroTheme.BrowseBackground,
                ForeColor = RetroTheme.BrowseText,
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false
            };
            _list.DoubleClick += (s, e) => Choose();
            _list.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) Choose(); };

            var footer = new DosFooterBar();
            footer.SetHints(("Entree", "Choisir"), ("Esc", "Annuler"));

            Controls.Add(_list);
            Controls.Add(footer);
            Controls.Add(searchBand);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RunSearch();
            _searchBox.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void RunSearch()
        {
            _list.Items.Clear();
            foreach (var item in _search(_searchBox.Text))
            {
                _list.Items.Add(new Entry(item, _label(item)));
            }
            if (_list.Items.Count > 0)
            {
                _list.SelectedIndex = 0;
            }
        }

        private void Choose()
        {
            if (_list.SelectedItem is Entry entry)
            {
                Selected = entry.Value;
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private class Entry
        {
            public T Value { get; }
            private readonly string _text;
            public Entry(T value, string text) { Value = value; _text = text; }
            public override string ToString() => _text;
        }
    }
}
