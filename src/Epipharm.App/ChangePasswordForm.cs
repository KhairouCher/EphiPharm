using System;
using System.Drawing;
using System.Windows.Forms;
using Epipharm.Data;

namespace Epipharm.App
{
    /// <summary>Forced on first login for every reset account (see scripts/generate_accounts.py).</summary>
    public class ChangePasswordForm : Form
    {
        private const int MinPasswordLength = 6;

        private readonly AccountRepository _repo;
        private readonly Account _account;
        private readonly TextBox _newPasswordBox;
        private readonly TextBox _confirmBox;
        private readonly Label _errorLabel;

        public ChangePasswordForm(AccountRepository repo, Account account)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _account = account ?? throw new ArgumentNullException(nameof(account));

            Text = "EPIPHARM — Nouveau mot de passe";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(440, 340);
            Font = new Font("Segoe UI", 12F);

            var info = new Label
            {
                Text = $"Bonjour {_account.Username}.\nChoisissez un nouveau mot de passe\n(au moins {MinPasswordLength} caracteres).",
                Location = new Point(24, 20),
                Size = new Size(392, 70)
            };

            var newLabel = new Label { Text = "Nouveau mot de passe :", Location = new Point(24, 96), AutoSize = true };
            _newPasswordBox = new TextBox
            {
                Location = new Point(24, 124), Width = 392, Font = new Font("Segoe UI", 14F),
                UseSystemPasswordChar = true, TabIndex = 0
            };

            var confirmLabel = new Label { Text = "Confirmez :", Location = new Point(24, 172), AutoSize = true };
            _confirmBox = new TextBox
            {
                Location = new Point(24, 200), Width = 392, Font = new Font("Segoe UI", 14F),
                UseSystemPasswordChar = true, TabIndex = 1
            };

            _errorLabel = new Label
            {
                Location = new Point(24, 240), Size = new Size(392, 40), ForeColor = Color.Firebrick
            };

            var saveButton = new Button
            {
                Text = "Enregistrer", Location = new Point(24, 284), Size = new Size(392, 44),
                Font = new Font("Segoe UI", 13F), TabIndex = 2
            };
            saveButton.Click += (s, e) => AttemptSave();

            Controls.AddRange(new Control[]
            {
                info, newLabel, _newPasswordBox, confirmLabel, _confirmBox, _errorLabel, saveButton
            });

            AcceptButton = saveButton;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _newPasswordBox.Focus();
        }

        private void AttemptSave()
        {
            var p1 = _newPasswordBox.Text;
            var p2 = _confirmBox.Text;

            if (p1.Length < MinPasswordLength)
            {
                _errorLabel.Text = $"Le mot de passe doit contenir au moins {MinPasswordLength} caracteres.";
                return;
            }
            if (p1 != p2)
            {
                _errorLabel.Text = "Les deux mots de passe ne correspondent pas.";
                _confirmBox.Clear();
                _confirmBox.Focus();
                return;
            }

            _repo.SetPassword(_account.Id, p1);
            _account.MustChangePassword = false;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
