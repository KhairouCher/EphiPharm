using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Epipharm.Data;
using Epipharm.App.Theme;

namespace Epipharm.App
{
    /// <summary>
    /// "Liste des Produits Perimes a Retirer Immediatement du Stock"
    /// (MENU_MAP.md section 1) — every lot already past its expiry date.
    /// Same pale Browse archetype and grid styling as StockListForm (which
    /// already highlights expired rows in red); this is that same
    /// definition of "expired" as its own dedicated, pre-filtered list,
    /// not search results someone has to go looking for.
    /// </summary>
    public class ExpiredStockListForm : Form
    {
        private readonly StockRepository _repo;
        private readonly DosHeaderBar _header;
        private readonly DosFooterBar _footer;
        private readonly DataGridView _grid;
        private readonly Label _statusLabel;

        public ExpiredStockListForm(StockRepository repo)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));

            Text = "EPIPHARM — Produits Perimes";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(900, 560);
            MinimumSize = new Size(700, 420);
            KeyPreview = true;
            BackColor = RetroTheme.BrowseBackground;

            _header = new DosHeaderBar { Subtitle = "Liste des Produits Perimes a Retirer" };
            _footer = new DosFooterBar();
            _footer.SetHints(("F5", "Rafraichir"), ("Esc", "Sortie"));

            _grid = BuildGrid();

            _statusLabel = new Label
            {
                Dock = DockStyle.Top, Height = 26, Padding = new Padding(12, 4, 0, 0),
                Font = RetroTheme.DosFont(10F), ForeColor = RetroTheme.BrowseText, BackColor = RetroTheme.BrowseBackground
            };

            Controls.Add(_grid);
            Controls.Add(_statusLabel);
            Controls.Add(_footer);
            Controls.Add(_header);
        }

        private DataGridView BuildGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoGenerateColumns = false,
                RowHeadersVisible = false, BorderStyle = BorderStyle.None,
                BackgroundColor = Dos16.LightRed, GridColor = RetroTheme.BrowseText,
                DefaultCellStyle =
                {
                    BackColor = Dos16.LightRed, ForeColor = Dos16.Black,
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
            { Name = "product", HeaderText = "Produit", DataPropertyName = "ProductName", FillWeight = 28 });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "dci", HeaderText = "DCI", DataPropertyName = "DciName", FillWeight = 20 });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "lot", HeaderText = "Lot", DataPropertyName = "LotNumber", FillWeight = 14 });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "expiry", HeaderText = "Peremption", DataPropertyName = "ExpiresOn", FillWeight = 14,
                DefaultCellStyle = { Format = RetroTheme.DosDateFormat, Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "qty", HeaderText = "Quantite", DataPropertyName = "Quantity", FillWeight = 12,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            return grid;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RunRefresh();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            if (keyData == Keys.F5) { RunRefresh(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void RunRefresh()
        {
            var rows = _repo.SearchExpired();
            _grid.DataSource = rows;
            var totalQty = rows.Sum(r => r.Quantity);
            _statusLabel.Text = $"{rows.Count} lot(s) perime(s)   -   {totalQty} unite(s) a retirer";
        }
    }
}
