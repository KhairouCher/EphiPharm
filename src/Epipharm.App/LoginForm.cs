using System;
using System.Drawing;
using System.Windows.Forms;
using Epipharm.Data;

namespace Epipharm.App
{
    /// <summary>
    /// Deliberately simple: two fields, big text, Enter submits from either
    /// field, Escape cancels. Built for staff who are not comfortable typing,
    /// not for keyboard power users — see MENU_MAP.md "UI conventions".
    /// </summary>
    public class LoginForm : Form
    {
        private readonly AccountRepository _repo;
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
            ClientSize = new Size(420, 300);
            Font = new Font("Segoe UI", 12F);

            var userLabel = new Label
            {
                Text = "Nom d'utilisateur :",
                Location = new Point(24, 24),
                AutoSize = true
            };
            _usernameBox = new TextBox
            {
                Location = new Point(24, 52),
                Width = 372,
                Font = new Font("Segoe UI", 14F),
                TabIndex = 0
            };

            var passLabel = new Label
            {
                Text = "Mot de passe :",
                Location = new Point(24, 100),
                AutoSize = true
            };
            _passwordBox = new TextBox
            {
                Location = new Point(24, 128),
                Width = 372,
                Font = new Font("Segoe UI", 14F),
                UseSystemPasswordChar = true,
                TabIndex = 1
            };

            _errorLabel = new Label
            {
                Location = new Point(24, 168),
                Size = new Size(372, 48),
                ForeColor = Color.Firebrick,
                Text = string.Empty
            };

            var loginButton = new Button
            {
                Text = "Connexion",
                Location = new Point(24, 224),
                Size = new Size(180, 48),
                Font = new Font("Segoe UI", 13F),
                TabIndex = 2
            };
            loginButton.Click += (s, e) => AttemptLogin();

            var quitButton = new Button
            {
                Text = "Quitter",
                Location = new Point(216, 224),
                Size = new Size(180, 48),
                Font = new Font("Segoe UI", 13F),
                TabIndex = 3
            };
            quitButton.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.AddRange(new Control[]
            {
                userLabel, _usernameBox, passLabel, _passwordBox,
                _errorLabel, loginButton, quitButton
            });

            AcceptButton = loginButton;
            CancelButton = quitButton;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _usernameBox.Focus();
        }

        private void AttemptLogin()
        {
            var account = _repo.TryLogin(_usernameBox.Text.Trim(), _passwordBox.Text);
            if (account == null)
            {
                _errorLabel.Text = "Nom d'utilisateur ou mot de passe incorrect.";
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
