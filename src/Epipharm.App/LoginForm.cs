using System;
using System.Drawing;
using System.Windows.Forms;
using Epipharm.Data;
using Epipharm.App.Theme;

namespace Epipharm.App
{
    /// <summary>
    /// Styled to match the rest of the app's recovered look (UI_SPEC.md),
    /// but NOT a confirmed match for a specific screenshot — no photo of
    /// the legacy login prompt itself was sent, only the main menu after
    /// logging in. The focused field getting a red highlight follows the
    /// same convention seen on every data-entry screenshot.
    /// </summary>
    public class LoginForm : Form
    {
        private readonly AccountRepository _repo;
        private readonly DosHeaderBar _header;
        private readonly DosFooterBar _footer;
        private readonly TextBox _usernameBox;
        private readonly TextBox _passwordBox;
        private readonly Label _errorLabel;

        public Account LoggedInAccount { get; private set; }

        public LoginForm(AccountRepository repo)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));

            Text = "EPIPHARM — Connexion";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(480, 420);
            KeyPreview = true;

            _header = new DosHeaderBar { Subtitle = "Connexion" };
            _footer = new DosFooterBar();
            _footer.SetHints(("Esc", "Quitter"));

            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = RetroTheme.MenuBackground,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(32, 28, 32, 0)
            };
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var userLabel = FieldLabel("Nom d'Utilisateur :");
            _usernameBox = FieldBox();
            var passLabel = FieldLabel("Mot de Passe :");
            _passwordBox = FieldBox();
            _passwordBox.UseSystemPasswordChar = true;

            _errorLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Dos16.LightRed,
                Font = RetroTheme.DosFont(10F, FontStyle.Bold),
                TextAlign = ContentAlignment.TopLeft
            };

            var buttonRow = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = RetroTheme.MenuBackground };
            var loginButton = NewButton("Connexion (Entree)");
            loginButton.Click += (s, e) => AttemptLogin();
            var quitButton = NewButton("Quitter (Esc)");
            quitButton.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            buttonRow.Controls.Add(loginButton);
            buttonRow.Controls.Add(quitButton);

            body.Controls.Add(userLabel, 0, 0);
            body.Controls.Add(_usernameBox, 0, 1);
            body.Controls.Add(passLabel, 0, 2);
            body.Controls.Add(_passwordBox, 0, 3);
            body.Controls.Add(buttonRow, 0, 4);
            body.Controls.Add(_errorLabel, 0, 5);

            Controls.Add(body);
            Controls.Add(_footer);
            Controls.Add(_header);

            AcceptButton = loginButton;
            CancelButton = quitButton;
        }

        private Label FieldLabel(string text) => new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            ForeColor = RetroTheme.FieldLabel,
            BackColor = RetroTheme.MenuBackground,
            Font = RetroTheme.DosFont(11F)
        };

        private TextBox FieldBox()
        {
            var box = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = RetroTheme.DosFont(14F),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = RetroTheme.MenuBackground,
                ForeColor = RetroTheme.FieldValue
            };
            // Legacy screens show the field currently being typed into as a
            // solid red bar — approximated here with a focus color swap.
            box.Enter += (s, e) => { box.BackColor = RetroTheme.FieldActiveBack; box.ForeColor = RetroTheme.FieldActiveText; };
            box.Leave += (s, e) => { box.BackColor = RetroTheme.MenuBackground; box.ForeColor = RetroTheme.FieldValue; };
            return box;
        }

        private Button NewButton(string text) => new Button
        {
            Text = text,
            AutoSize = true,
            Font = RetroTheme.DosFont(11F),
            BackColor = RetroTheme.BoxBorder,
            ForeColor = RetroTheme.MenuBackground,
            Margin = new Padding(0, 0, 16, 0),
            Padding = new Padding(8, 6, 8, 6)
        };

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _usernameBox.Focus();
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

        private void AttemptLogin()
        {
            var account = _repo.TryLogin(_usernameBox.Text.Trim(), _passwordBox.Text);
            if (account == null)
            {
                _errorLabel.Text = "Nom d'Utilisateur ou Mot de Passe Incorrect.";
                _passwordBox.Clear();
                _passwordBox.Focus();
                return;
            }

            LoggedInAccount = account;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
