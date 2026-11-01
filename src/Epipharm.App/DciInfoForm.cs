using System;
using System.Drawing;
using System.Windows.Forms;
using Epipharm.Data;
using Epipharm.App.Theme;

namespace Epipharm.App
{
    /// <summary>
    /// "( Informations D.C.I. )" (MENU_MAP.md section 6) — read-only DCI
    /// reference lookup. Code D.C.I is typed (F5 for the picker), same
    /// convention as every other screen; there's nothing to save here, so
    /// Esc just closes, no confirmation prompt.
    ///
    /// Only shows fields with a confirmed backing column — see
    /// DciLookup's doc comment for exactly which fields on the real
    /// screenshot (Tableau, Stockage, Production, Affectation,
    /// N° Décision, Saison, Appartenance, Date Introduction, Retiré,
    /// Date de Retrait) have no matching data anywhere in the raw import
    /// and are shown as labels with a blank value instead of an invented
    /// one.
    /// </summary>
    public class DciInfoForm : Form
    {
        private readonly DciRepository _dciRepo;

        private readonly DosHeaderBar _header;
        private readonly DosFooterBar _footer;
        private readonly TextBox _codeBox;
        private readonly Label _subclassFlagLabel;
        private readonly Label _classNameLabel;
        private readonly Label _subclassNameLabel;
        private readonly Label _designationLabel;
        private readonly Label _formeDosageLabel;
        private readonly Label _tableauLabel;
        private readonly Label _retireLabel;
        private readonly Label _typeMedicamentLabel;
        private readonly Label _dateRetraitLabel;
        private readonly Label _errorLabel;

        public DciInfoForm(DciRepository dciRepo)
        {
            _dciRepo = dciRepo ?? throw new ArgumentNullException(nameof(dciRepo));

            Text = "EPIPHARM — Informations D.C.I.";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(820, 620);
            MinimumSize = new Size(700, 500);
            KeyPreview = true;
            BackColor = RetroTheme.MenuBackground;

            _header = new DosHeaderBar { Subtitle = "( Informations D.C.I. )" };
            _footer = new DosFooterBar();
            _footer.SetHints(("F5", "Chercher"), ("Esc", "Sortie"));

            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, BackColor = RetroTheme.MenuBackground,
                ColumnCount = 4, RowCount = 11, Padding = new Padding(20, 16, 20, 16)
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            for (var i = 0; i < 13; i++) body.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

            body.Controls.Add(FieldLabel("Code D.C.I"), 0, 0);
            _codeBox = new TextBox { Dock = DockStyle.Fill, Font = RetroTheme.DosFont(12F) };
            _codeBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.F5) { PickDci(); e.Handled = true; } };
            _codeBox.Leave += (s, e) => LoadTypedCode();
            body.Controls.Add(_codeBox, 1, 0);
            body.Controls.Add(FieldLabel("S/Classe :"), 2, 0);
            _subclassFlagLabel = ValueLabel(RetroTheme.FieldValue);
            body.Controls.Add(_subclassFlagLabel, 3, 0);

            _classNameLabel = ValueLabel(RetroTheme.SecondaryLabel);
            body.SetColumnSpan(_classNameLabel, 3);
            body.Controls.Add(_classNameLabel, 1, 1);

            _subclassNameLabel = ValueLabel(Dos16.White);
            body.SetColumnSpan(_subclassNameLabel, 3);
            body.Controls.Add(_subclassNameLabel, 1, 3);

            body.Controls.Add(FieldLabel("Désignation :"), 0, 4);
            _designationLabel = ValueLabel(RetroTheme.FieldValue);
            body.SetColumnSpan(_designationLabel, 3);
            body.Controls.Add(_designationLabel, 1, 4);

            body.Controls.Add(FieldLabel("Forme/Dosage :"), 0, 5);
            _formeDosageLabel = ValueLabel(RetroTheme.FieldValue);
            body.SetColumnSpan(_formeDosageLabel, 3);
            body.Controls.Add(_formeDosageLabel, 1, 5);

            body.Controls.Add(FieldLabel("Date Introduction:"), 0, 6);
            body.Controls.Add(FieldLabel("N° Decision :"), 2, 6);

            body.Controls.Add(FieldLabel("Tableau :"), 0, 7);
            _tableauLabel = ValueLabel(RetroTheme.FieldValue);
            body.Controls.Add(_tableauLabel, 1, 7);
            body.Controls.Add(FieldLabel("Type Medicament :"), 2, 7);
            _typeMedicamentLabel = ValueLabel(RetroTheme.FieldValue);
            body.Controls.Add(_typeMedicamentLabel, 3, 7);

            body.Controls.Add(FieldLabel("Stockage :"), 0, 8);
            body.Controls.Add(FieldLabel("Saison :"), 2, 8);

            body.Controls.Add(FieldLabel("Production :"), 0, 9);
            body.Controls.Add(FieldLabel("Appartenance :"), 2, 9);

            body.Controls.Add(FieldLabel("Affectation :"), 0, 10);

            // Row 11 left blank, matching the real screenshot's gap before
            // Retiré/Date de Retrait.
            body.Controls.Add(FieldLabel("Retiré :"), 0, 12);
            _retireLabel = ValueLabel(RetroTheme.FieldValue);
            body.Controls.Add(_retireLabel, 1, 12);
            body.Controls.Add(FieldLabel("Date de Retrait :"), 2, 12);
            _dateRetraitLabel = ValueLabel(RetroTheme.FieldValue);
            body.Controls.Add(_dateRetraitLabel, 3, 12);

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

        private static Label FieldLabel(string text) => new Label
        {
            Text = text, Dock = DockStyle.Fill, ForeColor = RetroTheme.FieldLabel, Font = RetroTheme.DosFont(10F),
            TextAlign = ContentAlignment.MiddleLeft
        };

        private static Label ValueLabel(Color color) => new Label
        {
            Dock = DockStyle.Fill, ForeColor = color, Font = RetroTheme.DosFont(11F),
            TextAlign = ContentAlignment.MiddleLeft
        };

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _codeBox.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            if (keyData == Keys.F5 && !_codeBox.Focused) { PickDci(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void PickDci()
        {
            using (var picker = new PickerForm<DciLookup>(
                "Liste des Classes Therapeutiques / DCI", "Rechercher (nom ou code) :",
                q => _dciRepo.Search(q), d => $"{d.FormattedCode}  {d.Name}"))
            {
                if (picker.ShowDialog(this) == DialogResult.OK)
                {
                    ShowDci(picker.Selected);
                }
            }
        }

        private void LoadTypedCode()
        {
            var typed = _codeBox.Text.Trim();
            if (string.IsNullOrEmpty(typed)) return;

            var found = _dciRepo.FindByCode(typed);
            if (found == null)
            {
                _errorLabel.Text = "D.C.I Inexistant(e)";
                ClearFields();
            }
            else
            {
                ShowDci(found);
            }
        }

        private void ShowDci(DciLookup dci)
        {
            _errorLabel.Text = string.Empty;
            _codeBox.Text = dci.FormattedCode;
            _subclassFlagLabel.Text = dci.SubclassFlag;
            _classNameLabel.Text = dci.ClassName;
            _subclassNameLabel.Text = dci.SubclassName;
            _designationLabel.Text = dci.Name;
            _formeDosageLabel.Text = dci.FormeDosageDisplay;
            _tableauLabel.Text = dci.TableauB ? "O" : string.Empty;
            // Retire/Date de Retrait: DCI has no real "discontinued" data
            // at all (see DciLookup's doc comment) — shown blank like the
            // other unconfirmed fields, not guessed from d_rupture, which
            // is a different thing (projected stock-rupture date).
            _retireLabel.Text = string.Empty;
            _typeMedicamentLabel.Text = dci.DrugCategoryDisplay;
            _dateRetraitLabel.Text = string.Empty;
        }

        private void ClearFields()
        {
            _subclassFlagLabel.Text = string.Empty;
            _classNameLabel.Text = string.Empty;
            _subclassNameLabel.Text = string.Empty;
            _designationLabel.Text = string.Empty;
            _formeDosageLabel.Text = string.Empty;
            _tableauLabel.Text = string.Empty;
            _retireLabel.Text = string.Empty;
            _typeMedicamentLabel.Text = string.Empty;
            _dateRetraitLabel.Text = string.Empty;
        }
    }
}
