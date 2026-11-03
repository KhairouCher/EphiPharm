using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Epipharm.Data;
using Epipharm.App.Theme;

namespace Epipharm.App
{
    /// <summary>
    /// "Mise a Jour des Dotations en Medicaments Usuels" (MENU_MAP.md
    /// section 5) — set each active Medicaments Usuels DCI's weekly
    /// allocation for a service. Matches the real screenshot's own
    /// archetype exactly: unlike the Form-B entry screens (blue body),
    /// this one is the pale Browse archetype (UI_SPEC.md archetype C) —
    /// "Code Service :" sits directly on the pale background, not a blue
    /// header band, and the grid uses Browse colors, not FormGrid colors.
    ///
    /// Code Service is typed (F5 for the picker), same convention as
    /// every other screen; the grid's Dotation column is the one editable
    /// field, typed directly like DeliveryEntryForm's Q.Dem. No "Choisir"
    /// or "Enregistrer" button — Esc asks "Enregistrer ? (O/N)".
    ///
    /// The real screenshot's footer also shows "&lt;F2&gt; Menu
    /// d'utilisation" and "&lt;I,i&gt; Impression" — not shown here since
    /// neither a help menu nor printing exists in this rebuild yet; a
    /// footer hint for something that doesn't work would be worse than
    /// one that's merely incomplete.
    /// </summary>
    public class DotationEntryForm : Form
    {
        private readonly DotationEntryRepository _entryRepo;
        private readonly ServiceRepository _serviceRepo;
        private readonly int _accountId;
        private readonly int _year;
        private readonly int _week;

        private readonly DosHeaderBar _header;
        private readonly DosFooterBar _footer;
        private readonly TextBox _serviceCodeBox;
        private readonly Label _serviceNameLabel;
        private readonly DataGridView _grid;
        private readonly Label _errorLabel;

        private ServiceLookup _service;
        private BindingList<DotationRow> _rows = new BindingList<DotationRow>();

        /// <param name="windowTitle">Short title for the window/taskbar.</param>
        /// <param name="subtitle">The exact menu wording for this
        /// category — MENU_MAP.md's four dotation leaves are irregular
        /// ("...en Medicaments Usuels", "...en Psychotropes", "...Autres
        /// Medicaments" with no "en", "...Dotations Stupefiants" with no
        /// "des"), not a template with the category substituted in, so
        /// each caller passes its own exact confirmed wording.</param>
        public DotationEntryForm(DotationEntryRepository entryRepo, ServiceRepository serviceRepo, int accountId,
            string windowTitle, string subtitle)
        {
            _entryRepo = entryRepo ?? throw new ArgumentNullException(nameof(entryRepo));
            _serviceRepo = serviceRepo ?? throw new ArgumentNullException(nameof(serviceRepo));
            _accountId = accountId;
            var today = DateTime.Today;
            _year = today.Year;
            _week = DeliveryEntryRepository.WeekOfYear(today);

            Text = "EPIPHARM — " + windowTitle;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(900, 680);
            MinimumSize = new Size(700, 500);
            KeyPreview = true;
            BackColor = RetroTheme.BrowseBackground;

            _header = new DosHeaderBar { Subtitle = subtitle };
            _footer = new DosFooterBar();
            _footer.SetHints(("F5", "Choisir Service"), ("Esc", "Sortie"));

            var serviceBand = new TableLayoutPanel
            {
                Dock = DockStyle.Top, Height = 44, BackColor = RetroTheme.BrowseBackground,
                ColumnCount = 3, RowCount = 1, Padding = new Padding(12, 8, 12, 8)
            };
            serviceBand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            serviceBand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
            serviceBand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            serviceBand.Controls.Add(new Label
            {
                Text = "Code Service :", Dock = DockStyle.Fill, ForeColor = RetroTheme.BrowseText,
                Font = RetroTheme.DosFont(11F), TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            _serviceCodeBox = new TextBox { Dock = DockStyle.Fill, Font = RetroTheme.DosFont(11F) };
            _serviceCodeBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.F5) { PickService(); e.Handled = true; } };
            _serviceCodeBox.Leave += (s, e) => ResolveTypedService();
            serviceBand.Controls.Add(_serviceCodeBox, 1, 0);
            _serviceNameLabel = new Label
            {
                Dock = DockStyle.Fill, ForeColor = RetroTheme.BrowseText, Font = RetroTheme.DosFont(11F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            serviceBand.Controls.Add(_serviceNameLabel, 2, 0);

            _grid = BuildGrid();

            var bottomBar = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom, Height = 30, BackColor = RetroTheme.BrowseBackground,
                ColumnCount = 1, RowCount = 1, Padding = new Padding(12, 4, 12, 4)
            };
            _errorLabel = new Label
            {
                Dock = DockStyle.Fill, ForeColor = Dos16.Red, Font = RetroTheme.DosFont(10F, FontStyle.Bold)
            };
            bottomBar.Controls.Add(_errorLabel, 0, 0);

            Controls.Add(_grid);
            Controls.Add(bottomBar);
            Controls.Add(serviceBand);
            Controls.Add(_footer);
            Controls.Add(_header);
        }

        private DataGridView BuildGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect, AutoGenerateColumns = false,
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
            { Name = "dci", HeaderText = "DCI", DataPropertyName = "DciCode", FillWeight = 14, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "libelle", HeaderText = "Libelle DCI/Forme", DataPropertyName = "Label", FillWeight = 56, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "dotation", HeaderText = "Dotation", DataPropertyName = "Dotation", FillWeight = 20, ReadOnly = false,
                DefaultCellStyle =
                {
                    BackColor = RetroTheme.FieldActiveBack, ForeColor = RetroTheme.FieldActiveText,
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });
            grid.DataError += (s, e) => { e.ThrowException = false; _errorLabel.Text = "Valeur Invalide"; };
            return grid;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _serviceCodeBox.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { AttemptCloseWithConfirm(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void PickService()
        {
            using (var picker = new PickerForm<ServiceLookup>(
                "Liste des Services Utilisateurs", "Rechercher (code ou nom du service) :",
                q => _serviceRepo.Search(q), s => s.Label))
            {
                if (picker.ShowDialog(this) == DialogResult.OK)
                {
                    SetService(picker.Selected);
                }
            }
        }

        private void ResolveTypedService()
        {
            var typed = _serviceCodeBox.Text.Trim();
            if (string.IsNullOrEmpty(typed))
            {
                _service = null;
                _serviceNameLabel.Text = string.Empty;
                return;
            }
            var found = _serviceRepo.FindByCode(typed);
            if (found == null)
            {
                _service = null;
                _serviceNameLabel.Text = string.Empty;
                _errorLabel.Text = "Service Inexistant";
            }
            else
            {
                SetService(found);
            }
        }

        private void SetService(ServiceLookup service)
        {
            _service = service;
            _serviceCodeBox.Text = service.Code;
            _serviceNameLabel.Text = service.Name;
            _errorLabel.Text = string.Empty;
            _rows = new BindingList<DotationRow>(_entryRepo.GetRows(service.Id, _year, _week));
            _grid.DataSource = _rows;
        }

        private void AttemptCloseWithConfirm()
        {
            if (_service == null)
            {
                Close();
                return;
            }
            if (!DosConfirmForm.Ask(this, "Enregistrer"))
            {
                Close();
                return;
            }
            try
            {
                _entryRepo.Save(_service.Id, _year, _week, _rows.ToList(), _accountId);
                Close();
            }
            catch (InvalidOperationException ex)
            {
                _errorLabel.Text = ex.Message;
            }
        }
    }
}
