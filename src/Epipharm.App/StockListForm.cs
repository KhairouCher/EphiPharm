using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Epipharm.Data;

namespace Epipharm.App
{
    /// <summary>
    /// Read-only stock list: search by drug name or lot, expired/expiring-soon
    /// rows highlighted. First real screen of the rebuild — editing (legacy
    /// "Corrections de Stock", "Initialisation Stock") is deliberately not
    /// here yet; this proves out the data layer and the list-first UX pattern
    /// (MENU_MAP.md: staff search a list rather than type a code) before more
    /// screens are built on top of it.
    /// </summary>
    public class StockListForm : Form
    {
        private readonly StockRepository _repo;
        private readonly int _expiryWarningDays;
        private readonly TextBox _searchBox;
        private readonly DataGridView _grid;
        private readonly Label _statusLabel;

        public StockListForm(StockRepository repo)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _expiryWarningDays = SafeGetExpiryWarningDays();

            Text = "EPIPHARM — Stock";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(900, 560);
            Font = new Font("Segoe UI", 11F);
            MinimumSize = new Size(700, 400);
            KeyPreview = true;

            var searchLabel = new Label
            {
                Text = "Rechercher (nom du produit, DCI ou lot) :",
                Dock = DockStyle.Top,
                Height = 28,
                Padding = new Padding(12, 6, 0, 0)
            };
            _searchBox = new TextBox
            {
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 13F),
                Margin = new Padding(12, 0, 12, 0)
            };
            _searchBox.TextChanged += (s, e) => RunSearch();

            var legend = new Label
            {
                Dock = DockStyle.Top,
                Height = 26,
                Padding = new Padding(12, 4, 0, 0),
                Text = $"Rouge = perime.   Orange = perime dans {_expiryWarningDays} jours ou moins.",
                ForeColor = Color.DimGray
            };

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoGenerateColumns = false,
                RowHeadersVisible = false,
                BackgroundColor = SystemColors.Window
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "product", HeaderText = "Produit", DataPropertyName = "ProductName", FillWeight = 28 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "dci", HeaderText = "DCI", DataPropertyName = "DciName", FillWeight = 20 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "form", HeaderText = "Forme", DataPropertyName = "FormName", FillWeight = 14 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "lot", HeaderText = "Lot", DataPropertyName = "LotNumber", FillWeight = 12 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "expiry", HeaderText = "Peremption", DataPropertyName = "ExpiresOn", FillWeight = 12,
                DefaultCellStyle = { Format = "dd/MM/yyyy", Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "qty", HeaderText = "Quantite", DataPropertyName = "Quantity", FillWeight = 10,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "price", HeaderText = "Prix Unit.", DataPropertyName = "UnitPrice", FillWeight = 12,
                DefaultCellStyle = { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            _grid.RowPrePaint += Grid_RowPrePaint;

            _statusLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                Padding = new Padding(12, 6, 0, 0),
                ForeColor = Color.DimGray
            };

            var closeButton = new Button
            {
                Text = "Fermer (Esc)",
                Dock = DockStyle.Bottom,
                Height = 40,
                Font = new Font("Segoe UI", 12F)
            };
            closeButton.Click += (s, e) => Close();

            Controls.Add(_grid);
            Controls.Add(legend);
            Controls.Add(_searchBox);
            Controls.Add(searchLabel);
            Controls.Add(_statusLabel);
            Controls.Add(closeButton);
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
                _grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.MistyRose;
            }
            else if (row.IsExpiringSoon(_expiryWarningDays))
            {
                _grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.LightGoldenrodYellow;
            }
            else
            {
                _grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = SystemColors.Window;
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
