using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Epipharm.Data;
using Epipharm.App.Theme;

namespace Epipharm.App
{
    /// <summary>
    /// "Comptes Utilisateurs" (Administration) — this rebuild's own
    /// addition, not confirmed from a legacy screenshot (account
    /// management wasn't part of the original app's own menu strings
    /// either — see MENU_MAP.md section 9, "user accounts are separate").
    /// Styled consistently with the rest of the app anyway: pale Browse
    /// list, keyboard-driven, no buttons. F2 resets the selected account's
    /// password to a new random one and shows it once — the same reset
    /// scripts/generate_accounts.py originally did for all 6 accounts,
    /// now available without re-running a script.
    /// </summary>
    public class AccountsAdminForm : Form
    {
        private readonly AccountRepository _repo;
        private readonly DosHeaderBar _header;
        private readonly DosFooterBar _footer;
        private readonly DataGridView _grid;
        private readonly Label _statusLabel;

        public AccountsAdminForm(AccountRepository repo)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));

            Text = "EPIPHARM — Comptes Utilisateurs";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(760, 520);
            MinimumSize = new Size(600, 400);
            KeyPreview = true;
            BackColor = RetroTheme.BrowseBackground;

            _header = new DosHeaderBar { Subtitle = "Comptes Utilisateurs" };
            _footer = new DosFooterBar();
            _footer.SetHints(("F2", "Reinitialiser Mot de Passe"), ("F5", "Rafraichir"), ("Esc", "Sortie"));

            _grid = BuildGrid();

            _statusLabel = new Label
            {
                Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(12, 4, 12, 4),
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
            { Name = "username", HeaderText = "Utilisateur", DataPropertyName = "Username", FillWeight = 35 });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "role", HeaderText = "Droits", DataPropertyName = "RoleDisplay", FillWeight = 30 });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "pwd", HeaderText = "Mot de Passe", DataPropertyName = "PasswordStatusDisplay", FillWeight = 35 });
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
            if (keyData == Keys.F2) { AttemptResetPassword(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void RunRefresh()
        {
            _statusLabel.Text = string.Empty;
            _grid.DataSource = _repo.ListAccounts().Select(a => new AccountRow(a)).ToList();
        }

        private void AttemptResetPassword()
        {
            if (!(_grid.CurrentRow?.DataBoundItem is AccountRow row)) return;

            if (!DosConfirmForm.Ask(this, $"Reinitialiser le Mot de Passe de {row.Username}"))
            {
                return;
            }

            var newPassword = _repo.ResetPassword(row.Id);
            // RunRefresh clears _statusLabel first (it's meant to clear a
            // stale message on a plain refresh) — call it before setting
            // the actual password message, not after, or this would wipe
            // out the one thing the admin needs to read.
            var username = row.Username;
            RunRefresh();
            _statusLabel.Text = $"Nouveau mot de passe pour {username} : {newPassword}" +
                "  (a transmettre a l'utilisateur ; non enregistre ailleurs)";
        }

        /// <summary>Display row for the grid — a real class, not an
        /// anonymous type, so AttemptResetPassword can read it back
        /// without reflection.</summary>
        private class AccountRow
        {
            public int Id { get; }
            public string Username { get; }
            public string RoleDisplay { get; }
            public string PasswordStatusDisplay { get; }

            public AccountRow(Account account)
            {
                Id = account.Id;
                Username = account.Username;
                RoleDisplay = account.IsAdmin ? "Administrateur" : "Utilisateur";
                PasswordStatusDisplay = account.MustChangePassword ? "A definir (reinitialise)" : "Deja definis";
            }
        }
    }
}
