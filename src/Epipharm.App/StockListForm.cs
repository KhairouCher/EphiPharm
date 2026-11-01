using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Epipharm.Data;
using Epipharm.App.Theme;

namespace Epipharm.App
{
    /// <summary>
    /// Read-only stock list: search by drug name or lot, expired/expiring-soon
    /// rows highlighted. Styled as the legacy "browse" screen archetype
    /// (UI_SPEC.md section C — pale background, blue text, red selected
    /// row), matching the DCI-structure and dotations grid screenshots.
    /// Editing (legacy "Corrections de Stock", "Initialisation Stock") is
    /// deliberately not here yet; this proves out the data layer and the
    /// list-first UX pattern before more screens are built on top of it.
    /// The expired/expiring-soon highlight colors are this rebuild's own
    /// choice, not copied from a screenshot — no screenshot of that
    /// particular screen state exists yet to match against.
    /// </summary>
    public class StockListForm : Form
    {
        private readonly StockRepository _repo;
        private readonly int _expiryWarningDays;
        private readonly DosHeaderBar _header;
        private readonly DosFooterBar _footer;
        private readonly TextBox _searchBox;
        private readonly DataGridView _grid;
        private readonly Label _statusLabel;

        public StockListForm(StockRepository repo)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _expiryWarningDays = SafeGetExpiryWarningDays();

            Text = "EPIPHARM — Stock";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1000, 620);
            MinimumSize = new Size(800, 460);
            KeyPreview = true;
            BackColor = RetroTheme.BrowseBackground;

            _header = new DosHeaderBar { Subtitle = "Saisie/Modification du Fichier Stock" };

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
                Text = "Rechercher (nom du produit, DCI ou lot) :",
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

            var legend = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                Padding = new Padding(12, 2, 0, 0),
                Font = RetroTheme.DosFont(9F),
                Text = $"Rouge = perime.   Jaune = perime dans {_expiryWarningDays} jours ou moins.",
                ForeColor = Dos16.DarkGray,
                BackColor = RetroTheme.BrowseBackground
            };

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
            Controls.Add(legend);
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
            { Name = "product", HeaderText = "Produit", DataPropertyName = "ProductName", FillWeight = 28 });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "dci", HeaderText = "DCI", DataPropertyName = "DciName", FillWeight = 20 });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "form", HeaderText = "Forme", DataPropertyName = "FormName", FillWeight = 14 });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "lot", HeaderText = "Lot", DataPropertyName = "LotNumber", FillWeight = 12 });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "expiry", HeaderText = "Peremption", DataPropertyName = "ExpiresOn", FillWeight = 12,
                DefaultCellStyle = { Format = RetroTheme.DosDateFormat, Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "qty", HeaderText = "Quantite", DataPropertyName = "Quantity", FillWeight = 10,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "price", HeaderText = "Prix Unit.", DataPropertyName = "UnitPrice", FillWeight = 12,
                DefaultCellStyle = { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            grid.RowPrePaint += Grid_RowPrePaint;
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

            var expiredCount = rows.Count(r => r.IsExpired);
            var soonCount = rows.Count(r => r.IsExpiringSoon(_expiryWarningDays));
            var totalQty = rows.Sum(r => r.Quantity);
            _statusLabel.Text = $"{rows.Count} lot(s)   -   {totalQty} unite(s) au total   -   " +
                                 $"{expiredCount} perime(s)   -   {soonCount} a surveiller";
        }

        private void Grid_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (!(_grid.Rows[e.RowIndex].DataBoundItem is StockLotView row))
            {
                return;
            }

            if (row.IsExpired)
            {
                _grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Dos16.LightRed;
                _grid.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Dos16.Black;
            }
            else if (row.IsExpiringSoon(_expiryWarningDays))
            {
                _grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Dos16.Yellow;
                _grid.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Dos16.Black;
            }
            else
            {
                _grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = RetroTheme.BrowseBackground;
                _grid.Rows[e.RowIndex].DefaultCellStyle.ForeColor = RetroTheme.BrowseText;
            }
        }

        private int SafeGetExpiryWarningDays()
        {
            try
            {
                return _repo.GetExpiryWarningDays();
            }
            catch
            {
                return 60;
            }
        }
    }
}
