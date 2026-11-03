using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Epipharm.Data;
using Epipharm.App.Theme;

namespace Epipharm.App
{
    /// <summary>
    /// "Parametres d'Exploitation" (Administration/MENU_MAP.md section 9)
    /// — edit the single PARPHARM settings row. This rebuild's own
    /// addition, no legacy screenshot of this exact screen, styled
    /// consistently with the rest of the app anyway (Form-B blue body,
    /// typed fields, no buttons, Esc asks "Enregistrer ? (O/N)").
    ///
    /// Only facility_code/facility_name/authority_name/current_year/
    /// expiry_warning_days are editable — see SettingsInfo's doc comment
    /// for why the next_*_no counters are shown read-only instead.
    /// </summary>
    public class SettingsForm : Form
    {
        private readonly SettingsRepository _repo;
        private readonly int _accountId;

        private readonly DosHeaderBar _header;
        private readonly DosFooterBar _footer;
        private readonly TextBox _facilityCodeBox;
        private readonly TextBox _facilityNameBox;
        private readonly TextBox _authorityNameBox;
        private readonly TextBox _currentYearBox;
        private readonly TextBox _expiryWarningDaysBox;
        private readonly Label _errorLabel;

        private SettingsInfo _original;

        public SettingsForm(SettingsRepository repo, int accountId)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _accountId = accountId;

            Text = "EPIPHARM — Parametres d'Exploitation";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(760, 640);
            MinimumSize = new Size(640, 560);
            KeyPreview = true;
            BackColor = RetroTheme.MenuBackground;

            _header = new DosHeaderBar { Subtitle = "Parametres d'Exploitation" };
            _footer = new DosFooterBar();
            _footer.SetHints(("Esc", "Sortie"));

            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, BackColor = RetroTheme.MenuBackground,
                ColumnCount = 2, RowCount = 12, Padding = new Padding(20, 16, 20, 16)
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (var i = 0; i < 12; i++) body.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

            body.Controls.Add(FieldLabel("Code Etablissement :"), 0, 0);
            _facilityCodeBox = EditableBox();
            body.Controls.Add(_facilityCodeBox, 1, 0);

            body.Controls.Add(FieldLabel("Nom Etablissement :"), 0, 1);
            _facilityNameBox = EditableBox();
            body.Controls.Add(_facilityNameBox, 1, 1);

            body.Controls.Add(FieldLabel("Tutelle :"), 0, 2);
            _authorityNameBox = EditableBox();
            body.Controls.Add(_authorityNameBox, 1, 2);

            body.Controls.Add(FieldLabel("Annee en Cours :"), 0, 3);
            _currentYearBox = EditableBox();
            body.Controls.Add(_currentYearBox, 1, 3);

            body.Controls.Add(FieldLabel("Delai Alerte Peremption (jours) :"), 0, 4);
            _expiryWarningDaysBox = EditableBox();
            body.Controls.Add(_expiryWarningDaysBox, 1, 4);

            // Row 5 blank, matching the visual separation elsewhere between
            // an editable field block and a read-only reference block.
            body.Controls.Add(FieldLabel("Archive jusqu'a :"), 0, 6);
            body.Controls.Add(ReadOnlyLabel(out var archivedLabel), 1, 6);
            body.Controls.Add(FieldLabel("Prochain No Operation :"), 0, 7);
            body.Controls.Add(ReadOnlyLabel(out var nextOpLabel), 1, 7);
            body.Controls.Add(FieldLabel("Prochain No Bon Livraison :"), 0, 8);
            body.Controls.Add(ReadOnlyLabel(out var nextDeliveryLabel), 1, 8);
            body.Controls.Add(FieldLabel("Prochain No Bon Reception :"), 0, 9);
            body.Controls.Add(ReadOnlyLabel(out var nextReceiptLabel), 1, 9);
            body.Controls.Add(FieldLabel("Prochain No Commande :"), 0, 10);
            body.Controls.Add(ReadOnlyLabel(out var nextOrderLabel), 1, 10);
            body.Controls.Add(FieldLabel("Prochain No Inventaire :"), 0, 11);
            body.Controls.Add(ReadOnlyLabel(out var nextInventoryLabel), 1, 11);

            _archivedLabel = archivedLabel;
            _nextOpLabel = nextOpLabel;
            _nextDeliveryLabel = nextDeliveryLabel;
            _nextReceiptLabel = nextReceiptLabel;
            _nextOrderLabel = nextOrderLabel;
            _nextInventoryLabel = nextInventoryLabel;

            var errorBar = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = RetroTheme.MenuBackground };
            _errorLabel = new Label
            {
                Dock = DockStyle.Fill, ForeColor = Dos16.LightRed, Font = RetroTheme.DosFont(10F, FontStyle.Bold),
                Padding = new Padding(20, 0, 0, 0)
            };
            errorBar.Controls.Add(_errorLabel);

            Controls.Add(body);
            Controls.Add(errorBar);
            Controls.Add(_footer);
            Controls.Add(_header);
        }

        private readonly Label _archivedLabel;
        private readonly Label _nextOpLabel;
        private readonly Label _nextDeliveryLabel;
        private readonly Label _nextReceiptLabel;
        private readonly Label _nextOrderLabel;
        private readonly Label _nextInventoryLabel;

        private static Label FieldLabel(string text) => new Label
        {
            Text = text, Dock = DockStyle.Fill, ForeColor = RetroTheme.FieldLabel, Font = RetroTheme.DosFont(10F),
            TextAlign = ContentAlignment.MiddleLeft
        };

        private static TextBox EditableBox() => new TextBox
        {
            Dock = DockStyle.Fill, Font = RetroTheme.DosFont(11F)
        };

        private static Label ReadOnlyLabel(out Label label)
        {
            label = new Label
            {
                Dock = DockStyle.Fill, ForeColor = RetroTheme.FieldLookupValue, Font = RetroTheme.DosFont(11F),
                TextAlign = ContentAlignment.MiddleLeft
            };
            return label;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LoadSettings();
            _facilityCodeBox.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { AttemptCloseWithConfirm(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void LoadSettings()
        {
            _original = _repo.Get();
            if (_original == null)
            {
                _errorLabel.Text = "Aucune Ligne de Parametres (settings) Trouvee";
                return;
            }
            _facilityCodeBox.Text = _original.FacilityCode;
            _facilityNameBox.Text = _original.FacilityName;
            _authorityNameBox.Text = _original.AuthorityName;
            _currentYearBox.Text = _original.CurrentYear.ToString(CultureInfo.InvariantCulture);
            _expiryWarningDaysBox.Text = _original.ExpiryWarningDays?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            _archivedLabel.Text = _original.ArchivedThroughDate ?? string.Empty;
            _nextOpLabel.Text = _original.NextOperationNo.ToString(CultureInfo.InvariantCulture);
            _nextDeliveryLabel.Text = _original.NextDeliverySlipNo.ToString(CultureInfo.InvariantCulture);
            _nextReceiptLabel.Text = _original.NextReceiptSlipNo.ToString(CultureInfo.InvariantCulture);
            _nextOrderLabel.Text = _original.NextOrderNo.ToString(CultureInfo.InvariantCulture);
            _nextInventoryLabel.Text = _original.NextInventoryNo.ToString(CultureInfo.InvariantCulture);
        }

        private void AttemptCloseWithConfirm()
        {
            if (_original == null)
            {
                Close();
                return;
            }

            var changed = _facilityCodeBox.Text.Trim() != (_original.FacilityCode ?? string.Empty) ||
                          _facilityNameBox.Text.Trim() != (_original.FacilityName ?? string.Empty) ||
                          _authorityNameBox.Text.Trim() != (_original.AuthorityName ?? string.Empty) ||
                          _currentYearBox.Text.Trim() != _original.CurrentYear.ToString(CultureInfo.InvariantCulture) ||
                          _expiryWarningDaysBox.Text.Trim() != (_original.ExpiryWarningDays?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);

            if (!changed)
            {
                Close();
                return;
            }

            if (!DosConfirmForm.Ask(this, "Enregistrer"))
            {
                Close();
                return;
            }

            if (TrySave())
            {
                Close();
            }
        }

        private bool TrySave()
        {
            _errorLabel.Text = string.Empty;

            if (!int.TryParse(_currentYearBox.Text.Trim(), out var year) || year < 2000 || year > 2100)
            {
                _errorLabel.Text = "Annee en Cours Invalide";
                return false;
            }
            int? expiryDays = null;
            if (!string.IsNullOrWhiteSpace(_expiryWarningDaysBox.Text))
            {
                if (!int.TryParse(_expiryWarningDaysBox.Text.Trim(), out var days) || days < 0)
                {
                    _errorLabel.Text = "Delai Alerte Peremption Invalide";
                    return false;
                }
                expiryDays = days;
            }

            try
            {
                _repo.Save(new SettingsInfo
                {
                    FacilityCode = _facilityCodeBox.Text,
                    FacilityName = _facilityNameBox.Text,
                    AuthorityName = _authorityNameBox.Text,
                    CurrentYear = year,
                    ExpiryWarningDays = expiryDays
                }, _accountId);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                _errorLabel.Text = ex.Message;
                return false;
            }
        }
    }
}
