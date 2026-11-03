using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Epipharm.Data;
using Epipharm.App.Theme;

namespace Epipharm.App
{
    /// <summary>
    /// Generic read-only reference-table browse — one reusable screen for
    /// the 8 simple DONNEES NATIONALES lists (see ReferenceRepository's
    /// doc comment for why these use the pale Browse archetype with no
    /// screenshot to match pixel-for-pixel). Same search-as-you-type
    /// pattern as StockListForm.
    /// </summary>
    public class ReferenceListForm : Form
    {
        private readonly Func<string, System.Collections.Generic.List<ReferenceRow>> _search;
        private readonly DosHeaderBar _header;
        private readonly DosFooterBar _footer;
        private readonly TextBox _searchBox;
        private readonly DataGridView _grid;
        private readonly Label _statusLabel;

        public ReferenceListForm(string windowTitle, string subtitle, string codeHeader, string nameHeader,
            string extraHeader, Func<string, System.Collections.Generic.List<ReferenceRow>> search)
        {
            _search = search ?? throw new ArgumentNullException(nameof(search));

            Text = "EPIPHARM — " + windowTitle;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(900, 620);
            MinimumSize = new Size(700, 460);
            KeyPreview = true;
            BackColor = RetroTheme.BrowseBackground;

            _header = new DosHeaderBar { Subtitle = subtitle };
            _footer = new DosFooterBar();
            _footer.SetHints(("F5", "Rechercher a Nouveau"), ("Esc", "Sortie"));

            var searchBand = new TableLayoutPanel
            {
                Dock = DockStyle.Top, Height = 56, BackColor = RetroTheme.BrowseBackground,
                ColumnCount = 1, RowCount = 2, Padding = new Padding(12, 6, 12, 6)
            };
            searchBand.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            searchBand.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            searchBand.Controls.Add(new Label
            {
                Text = "Rechercher (code ou libelle) :", Dock = DockStyle.Fill,
                ForeColor = RetroTheme.BrowseText, Font = RetroTheme.DosFont(9F)
            }, 0, 0);
            _searchBox = new TextBox { Dock = DockStyle.Fill, Font = RetroTheme.DosFont(12F) };
            _searchBox.TextChanged += (s, e) => RunSearch();
            searchBand.Controls.Add(_searchBox, 0, 1);

            _grid = BuildGrid(codeHeader, nameHeader, extraHeader);

            _statusLabel = new Label
            {
                Dock = DockStyle.Top, Height = 24, Padding = new Padding(12, 2, 0, 0),
                Font = RetroTheme.DosFont(9F), ForeColor = RetroTheme.BrowseText, BackColor = RetroTheme.BrowseBackground
            };

            Controls.Add(_grid);
            Controls.Add(_statusLabel);
            Controls.Add(searchBand);
            Controls.Add(_footer);
            Controls.Add(_header);
        }

        private DataGridView BuildGrid(string codeHeader, string nameHeader, string extraHeader)
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoGenerateColumns = false,
                RowHeadersVisible = false, BorderStyle = BorderStyle.None,
                BackgroundColor = RetroTheme.BrowseBackground, GridColor = RetroTheme.BrowseText,
                DefaultCellStyle =
                {
                    BackColor = RetroTheme.BrowseBackground, ForeColor = RetroTheme.BrowseText,
                    Font = RetroTheme.DosFont(11F), SelectionBackColor = RetroTheme.BrowseSelectedBack,
                    SelectionForeColor = RetroTheme.BrowseSelectedText
                },
                ColumnHeadersDefaultCellStyle =
                {
                    BackColor = RetroTheme.BrowseBackground, ForeColor = RetroTheme.BrowseText,
                    Font = RetroTheme.DosFont(11F, FontStyle.Bold)
                },
                EnableHeadersVisualStyles = false
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "code", HeaderText = codeHeader, DataPropertyName = "Code", FillWeight = 18 });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "name", HeaderText = nameHeader, DataPropertyName = "Name", FillWeight = extraHeader == null ? 82 : 55 });
            if (extraHeader != null)
            {
                grid.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "extra", HeaderText = extraHeader, DataPropertyName = "Extra", FillWeight = 27 });
            }
            return grid;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RunSearch();
            _searchBox.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            if (keyData == Keys.F5) { _searchBox.Clear(); RunSearch(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void RunSearch()
        {
            var rows = _search(_searchBox.Text);
            _grid.DataSource = rows;
            _statusLabel.Text = $"{rows.Count} ligne(s)";
        }
    }
}
