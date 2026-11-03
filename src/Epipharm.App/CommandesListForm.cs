using System;
using System.Drawing;
using System.Windows.Forms;
using Epipharm.Data;
using Epipharm.App.Theme;

namespace Epipharm.App
{
    /// <summary>
    /// "Liste des Commandes" + "Annulation Commande" (MENU_MAP.md section
    /// 4) combined into one screen: F2 cancels the selected order, the
    /// same way AccountsAdminForm's F2 resets a password — one keyboard
    /// action on a browse list instead of a whole separate screen for
    /// what MENU_MAP.md itself lists as a single-purpose action, not a
    /// multi-field form. No legacy screenshot of either; pale Browse
    /// archetype, same as every other plain list in this rebuild.
    /// </summary>
    public class CommandesListForm : Form
    {
        private readonly CommandeRepository _repo;
        private readonly int _accountId;
        private readonly DosHeaderBar _header;
        private readonly DosFooterBar _footer;
        private readonly DataGridView _grid;
        private readonly Label _statusLabel;

        public CommandesListForm(CommandeRepository repo, int accountId)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _accountId = accountId;

            Text = "EPIPHARM — Liste des Commandes";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(820, 560);
            MinimumSize = new Size(640, 420);
            KeyPreview = true;
            BackColor = RetroTheme.BrowseBackground;

            _header = new DosHeaderBar { Subtitle = "Liste des Commandes" };
            _footer = new DosFooterBar();
            _footer.SetHints(("F2", "Annuler la Commande"), ("F5", "Rafraichir"), ("Esc", "Sortie"));

            _grid = BuildGrid();

            _statusLabel = new Label
            {
                Dock = DockStyle.Bottom, Height = 30, Padding = new Padding(12, 4, 12, 4),
                Font = RetroTheme.DosFont(10F, FontStyle.Bold), ForeColor = RetroTheme.FieldValue,
                BackColor = RetroTheme.BrowseBackground
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
            { Name = "no", HeaderText = "No Commande", DataPropertyName = "OrderNo", FillWeight = 18 });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "date", HeaderText = "Date", DataPropertyName = "OrderedOn", FillWeight = 18 });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "supplier", HeaderText = "Fournisseur", DataPropertyName = "SupplierSigle", FillWeight = 34 });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "status", HeaderText = "Statut", DataPropertyName = "StatusDisplay", FillWeight = 30 });
            return grid;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RunRefresh();
            _grid.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            if (keyData == Keys.F5) { RunRefresh(); return true; }
            if (keyData == Keys.F2) { AttemptCancel(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void RunRefresh()
        {
            _statusLabel.ForeColor = RetroTheme.FieldValue;
            _statusLabel.Text = string.Empty;
            var rows = _repo.List();
            _grid.DataSource = rows;
            _statusLabel.Text = $"{rows.Count} commande(s)";
        }

        private void AttemptCancel()
        {
            if (!(_grid.CurrentRow?.DataBoundItem is CommandeRow row)) return;

            if (!DosConfirmForm.Ask(this, $"Annuler la Commande No {row.OrderNo}"))
            {
                return;
            }

            try
            {
                _repo.Cancel(row.Id, DateTime.Today, _accountId);
                RunRefresh();
                _statusLabel.Text = $"Commande No {row.OrderNo} annulee.";
            }
            catch (InvalidOperationException ex)
            {
                _statusLabel.ForeColor = Dos16.LightRed;
                _statusLabel.Text = ex.Message;
            }
        }
    }
}
