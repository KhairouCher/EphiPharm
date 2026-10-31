using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Epipharm.Data;
using Epipharm.App.Theme;

namespace Epipharm.App
{
    /// <summary>
    /// Read-only browse of supplier receipts (legacy "Liste Globale des
    /// Bons de Reception" — MENU_MAP.md section 3). Same pale "browse"
    /// archetype and list-first search pattern as StockListForm.
    /// Tiers shows the resolved supplier name where the migration could
    /// decode one (confirmed "213"+supplier-code TIERS shape — see
    /// db/schema.sql's note on RECEPT.DBF), falling back to the raw
    /// legacy code for the other TIERS shapes, which are a different
    /// thing (establishment transfers, etc.) and not guessed at.
    /// </summary>
    public class ReceiptsListForm : Form
    {
        private readonly ReceiptRepository _repo;
        private readonly DosHeaderBar _header;
        private readonly DosFooterBar _footer;
        private readonly TextBox _searchBox;
        private readonly DataGridView _grid;
        private readonly Label _statusLabel;

        public ReceiptsListForm(ReceiptRepository repo)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));

            Text = "EPIPHARM — Bons de Reception";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1000, 620);
            MinimumSize = new Size(800, 460);
            KeyPreview = true;
            BackColor = RetroTheme.BrowseBackground;

            _header = new DosHeaderBar { Subtitle = "Liste Globale des Bons de Reception" };

            _footer = new DosFooterBar();
            _footer.SetHints(("F5", "Rechercher a Nouveau"), ("Esc", "Sortie"));

            var searchBand = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = RetroTheme.BrowseBackground,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(12, 6, 12, 6)
            };
            searchBand.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            searchBand.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            var searchLabel = new Label
            {
                Text = "Rechercher (numero de piece ou code tiers) :",
                Dock = DockStyle.Fill,
                ForeColor = RetroTheme.BrowseText,
                Font = RetroTheme.DosFont(10F)
            };
            _searchBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = RetroTheme.DosFont(13F),
                ForeColor = RetroTheme.BrowseText
            };
            _searchBox.TextChanged += (s, e) => RunSearch();
            searchBand.Controls.Add(searchLabel, 0, 0);
            searchBand.Controls.Add(_searchBox, 0, 1);

            _grid = BuildGrid();

            _statusLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 26,
                Padding = new Padding(12, 4, 0, 0),
                Font = RetroTheme.DosFont(10F),
                ForeColor = RetroTheme.BrowseText,
                BackColor = RetroTheme.BrowseBackground
            };

            // Fill (_grid) added first, then Top-docked bands above it and
            // the Bottom-docked footer — same ordering convention used
            // throughout Epipharm.App.
            Controls.Add(_grid);
            Controls.Add(_statusLabel);
            Controls.Add(searchBand);
            Controls.Add(_footer);
            Controls.Add(_header);
        }

        private DataGridView BuildGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoGenerateColumns = false,
                RowHeadersVisible = false,
                BorderStyle = BorderStyle.None,
                BackgroundColor = RetroTheme.BrowseBackground,
                GridColor = RetroTheme.BrowseText,
                DefaultCellStyle =
                {
                    BackColor = RetroTheme.BrowseBackground,
                    ForeColor = RetroTheme.BrowseText,
                    Font = RetroTheme.DosFont(11F),
                    SelectionBackColor = RetroTheme.BrowseSelectedBack,
                    SelectionForeColor = RetroTheme.BrowseSelectedText
                },
                ColumnHeadersDefaultCellStyle =
                {
                    BackColor = RetroTheme.BrowseBackground,
                    ForeColor = RetroTheme.BrowseText,
                    Font = RetroTheme.DosFont(11F, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleLeft
                },
                EnableHeadersVisualStyles = false
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "date", HeaderText = "Date", DataPropertyName = "ReceivedOn", FillWeight = 12,
                DefaultCellStyle = { Format = "dd/MM/yyyy", Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "doc", HeaderText = "N. Piece", DataPropertyName = "DocumentNo", FillWeight = 16 });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "tiers", HeaderText = "Tiers (Fournisseur)", DataPropertyName = "Tiers", FillWeight = 20 });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "type", HeaderText = "Type", DataPropertyName = "MovementType", FillWeight = 10,
              DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "amount", HeaderText = "Montant", DataPropertyName = "Amount", FillWeight = 16,
                DefaultCellStyle = { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });
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
            if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }
            if (keyData == Keys.F5)
            {
                _searchBox.Clear();
                RunSearch();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void RunSearch()
        {
            var rows = _repo.Search(_searchBox.Text);
            _grid.DataSource = rows;

            var totalAmount = rows.Sum(r => r.Amount);
            _statusLabel.Text = $"{rows.Count} bon(s) de reception   -   {totalAmount:N2} DZD au total";
        }
    }
}
