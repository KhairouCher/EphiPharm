using System;
using System.Drawing;
using System.Windows.Forms;
using Epipharm.Data;
using Epipharm.App.Theme;

namespace Epipharm.App
{
    /// <summary>Forced on first login for every reset account (see scripts/generate_accounts.py).</summary>
    public class ChangePasswordForm : Form
    {
        private const int MinPasswordLength = 6;

        private readonly AccountRepository _repo;
        private readonly Account _account;
        private readonly DosHeaderBar _header;
        private readonly DosFooterBar _footer;
        private readonly TextBox _newPasswordBox;
        private readonly TextBox _confirmBox;
        private readonly Label _errorLabel;

        public ChangePasswordForm(AccountRepository repo, Account account)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _account = account ?? throw new ArgumentNullException(nameof(account));

            Text = "EPIPHARM — Nouveau Mot de Passe";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(500, 460);

            _header = new DosHeaderBar { Subtitle = "Changement de Mot de Passe" };
            _footer = new DosFooterBar();

            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = RetroTheme.MenuBackground,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(32, 20, 32, 0)
            };
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var info = new Label
            {
                Text = $"Bonjour {_account.Username}.\nChoisissez un nouveau mot de passe (au moins {MinPasswordLength} caracteres).",
                Dock = DockStyle.Fill,
                ForeColor = RetroTheme.FieldLabel,
                Font = RetroTheme.DosFont(11F)
            };

            var newLabel = new Label
            {
                Text = "Nouveau Mot de Passe :", Dock = DockStyle.Fill,
                ForeColor = RetroTheme.FieldLabel, Font = RetroTheme.DosFont(11F)
            };
            _newPasswordBox = FieldBox();
            var confirmLabel = new Label
            {
                Text = "Confirmez :", Dock = DockStyle.Fill,
                ForeColor = RetroTheme.FieldLabel, Font = RetroTheme.DosFont(11F)
            };
            _confirmBox = FieldBox();

            _errorLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Dos16.LightRed,
                Font = RetroTheme.DosFont(10F, FontStyle.Bold)
            };

            body.Controls.Add(info, 0, 0);
            body.Controls.Add(newLabel, 0, 1);
            body.Controls.Add(_newPasswordBox, 0, 2);
            body.Controls.Add(confirmLabel, 0, 3);
            body.Controls.Add(_confirmBox, 0, 4);

            var bottomArea = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                BackColor = RetroTheme.MenuBackground
            };
            var saveButton = new Button
            {
                Text = "Enregistrer", AutoSize = true,
                Font = RetroTheme.DosFont(11F),
                BackColor = RetroTheme.BoxBorder, ForeColor = RetroTheme.MenuBackground,
                Padding = new Padding(8, 6, 8, 6)
            };
            saveButton.Click += (s, e) => AttemptSave();
            bottomArea.Controls.Add(saveButton);
            bottomArea.Controls.Add(_errorLabel);
            body.Controls.Add(bottomArea, 0, 5);

            Controls.Add(body);
            Controls.Add(_footer);
            Controls.Add(_header);

            AcceptButton = saveButton;
        }

        private TextBox FieldBox()
        {
            var box = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = RetroTheme.DosFont(13F),
                BorderStyle = BorderStyle.FixedSingle,
                UseSystemPasswordChar = true,
                BackColor = RetroTheme.MenuBackground,
                ForeColor = RetroTheme.FieldValue
            };
            box.Enter += (s, e) => { box.BackColor = RetroTheme.FieldActiveBack; box.ForeColor = RetroTheme.FieldActiveText; };
            box.Leave += (s, e) => { box.BackColor = RetroTheme.MenuBackground; box.ForeColor = RetroTheme.FieldValue; };
            return box;
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
