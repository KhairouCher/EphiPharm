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
    /// "Livraison sur Bon de Commande (Medicaments)" (MENU_MAP.md section
    /// 2) — pick a service, the screen pre-lists every DCI in that
    /// service's current weekly dotation with its stock/remaining-dotation/
    /// already-delivered, and the operator types a quantity to deliver
    /// directly into the grid (not an "add line" flow like
    /// ReceiptEntryForm — the real screenshot's own footer hint,
    /// "Introduire directement la Qte Demandee ou &lt;Rc&gt; pour la
    /// Modifiee", describes exactly this: edit a pre-filled grid cell).
    ///
    /// Field layout (Code Service / Date Operation / No de Piece, then
    /// No Operation, then No Semaine / Annee Dotation) and grid columns
    /// (DCI / Libelle DCI/Forme / Stock / Dota. / Q.Liv / Observ. / Q.Dem)
    /// match the real screenshot of this screen exactly — see
    /// DeliveryAllocationRow for what each column means and which parts
    /// are confirmed versus this rebuild's own reasonable choice.
    /// </summary>
    public class DeliveryEntryForm : Form
    {
        private readonly DeliveryEntryRepository _entryRepo;
        private readonly ServiceRepository _serviceRepo;
        private readonly int _accountId;

        private readonly DosHeaderBar _header;
        private readonly DosFooterBar _footer;
        private readonly Label _operationLabel;
        private readonly Label _serviceLabel;
        private readonly DateTimePicker _operationDatePicker;
        private readonly TextBox _documentNoBox;
        private readonly Label _weekLabel;
        private readonly Label _yearLabel;

        private readonly DataGridView _grid;
        private readonly Label _totalLabel;
        private readonly Label _errorLabel;

        private ServiceLookup _service;
        private BindingList<DeliveryAllocationRow> _rows = new BindingList<DeliveryAllocationRow>();

        public DeliveryEntryForm(DeliveryEntryRepository entryRepo, ServiceRepository serviceRepo, int accountId)
        {
            _entryRepo = entryRepo ?? throw new ArgumentNullException(nameof(entryRepo));
            _serviceRepo = serviceRepo ?? throw new ArgumentNullException(nameof(serviceRepo));
            _accountId = accountId;

            Text = "EPIPHARM — Livraison sur Bon de Commande";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1080, 700);
            MinimumSize = new Size(900, 600);
            KeyPreview = true;
            BackColor = RetroTheme.MenuBackground;

            _header = new DosHeaderBar { Subtitle = "Livraison sur Bon de Commande (Medicaments)" };
            _footer = new DosFooterBar();
            _footer.SetHints(("F5", "Choisir Service"), ("F2", "Enregistrer"), ("Esc", "Sortie"));

            // Header field grouping/order matches the real screenshot
            // exactly: row 1 is Code Service / Date Operation / No de
            // Piece, row 2 is No Operation alone, row 3 is No Semaine /
            // Annee Dotation.
            var headerFields = new TableLayoutPanel
            {
                Dock = DockStyle.Top, Height = 110, BackColor = RetroTheme.MenuBackground,
                ColumnCount = 6, RowCount = 3, Padding = new Padding(16, 8, 16, 8)
            };
            headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
            headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18F));
            headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18F));
            for (var i = 0; i < 3; i++) headerFields.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / 3));

            headerFields.Controls.Add(FieldLabel("Code Service :"), 0, 0);
            var serviceRow = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = RetroTheme.MenuBackground };
            _serviceLabel = new Label
            {
                Text = "(non choisi)", AutoSize = false, Width = 260, Height = 26,
                ForeColor = RetroTheme.FieldLookupValue, Font = RetroTheme.DosFont(11F), TextAlign = ContentAlignment.MiddleLeft
            };
            var serviceButton = SmallButton("Choisir (F5)");
            serviceButton.Click += (s, e) => PickService();
            serviceRow.Controls.Add(_serviceLabel);
            serviceRow.Controls.Add(serviceButton);
            headerFields.Controls.Add(serviceRow, 1, 0);

            headerFields.Controls.Add(FieldLabel("Date Operation :"), 2, 0);
            _operationDatePicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short, Dock = DockStyle.Fill, Font = RetroTheme.DosFont(10F)
            };
            _operationDatePicker.ValueChanged += (s, e) => RefreshComputedFields();
            headerFields.Controls.Add(_operationDatePicker, 3, 0);

            headerFields.Controls.Add(FieldLabel("No de Piece :"), 4, 0);
            _documentNoBox = new TextBox { Dock = DockStyle.Fill, Font = RetroTheme.DosFont(11F) };
            headerFields.Controls.Add(_documentNoBox, 5, 0);

            headerFields.Controls.Add(FieldLabel("No Operation :"), 0, 1);
            _operationLabel = new Label
            {
                Text = _entryRepo.PeekNextOperationNo().ToString(CultureInfo.InvariantCulture),
                Dock = DockStyle.Fill, ForeColor = RetroTheme.FieldValue, Font = RetroTheme.DosFont(11F)
            };
            headerFields.Controls.Add(_operationLabel, 1, 1);

            headerFields.Controls.Add(FieldLabel("No Semaine :"), 0, 2);
            _weekLabel = new Label
            {
                Dock = DockStyle.Fill, ForeColor = RetroTheme.FieldValue, Font = RetroTheme.DosFont(11F)
            };
            headerFields.Controls.Add(_weekLabel, 1, 2);

            headerFields.Controls.Add(FieldLabel("Annee Dotation :"), 2, 2);
            _yearLabel = new Label
            {
                Dock = DockStyle.Fill, ForeColor = RetroTheme.FieldValue, Font = RetroTheme.DosFont(11F)
            };
            headerFields.Controls.Add(_yearLabel, 3, 2);

            _grid = BuildGrid();

            var bottomBar = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom, Height = 64, BackColor = RetroTheme.MenuBackground,
                ColumnCount = 3, RowCount = 1, Padding = new Padding(16, 10, 16, 10)
            };
            bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            _errorLabel = new Label
            {
                Dock = DockStyle.Fill, ForeColor = Dos16.LightRed, Font = RetroTheme.DosFont(10F, FontStyle.Bold)
            };
            _totalLabel = new Label
            {
                Dock = DockStyle.Fill, ForeColor = RetroTheme.FieldValue, Font = RetroTheme.DosFont(12F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight, Text = "Total : 0,00 DZD"
            };
            var saveButton = SmallButton("Enregistrer (F2)");
            saveButton.Dock = DockStyle.Fill;
            saveButton.Click += (s, e) => AttemptSave();
            bottomBar.Controls.Add(_errorLabel, 0, 0);
            bottomBar.Controls.Add(_totalLabel, 1, 0);
            bottomBar.Controls.Add(saveButton, 2, 0);

            Controls.Add(_grid);
            Controls.Add(bottomBar);
            Controls.Add(headerFields);
            Controls.Add(_footer);
            Controls.Add(_header);

            RefreshComputedFields();
        }

        private DataGridView BuildGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect, AutoGenerateColumns = false,
                RowHeadersVisible = false, BorderStyle = BorderStyle.FixedSingle,
                BackgroundColor = RetroTheme.FormGridBackground, GridColor = RetroTheme.BoxBorder,
                DefaultCellStyle =
                {
                    BackColor = RetroTheme.FormGridBackground, ForeColor = RetroTheme.FormGridText,
                    Font = RetroTheme.DosFont(11F), SelectionBackColor = RetroTheme.MenuItemSelectedBack,
                    SelectionForeColor = RetroTheme.MenuItemSelectedText
                },
                ColumnHeadersDefaultCellStyle =
                {
                    BackColor = RetroTheme.FormGridHeaderBack, ForeColor = RetroTheme.FormGridHeaderText,
                    Font = RetroTheme.DosFont(11F, FontStyle.Bold)
                },
                EnableHeadersVisualStyles = false
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "dci", HeaderText = "DCI", DataPropertyName = "DciCode", FillWeight = 10, ReadOnly = true
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "libelle", HeaderText = "Libelle DCI/Forme", DataPropertyName = "Label", FillWeight = 32, ReadOnly = true
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "stock", HeaderText = "Stock", DataPropertyName = "StockQty", FillWeight = 12, ReadOnly = true,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "dota", HeaderText = "Dota.", DataPropertyName = "DotationRemaining", FillWeight = 12, ReadOnly = true,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "qliv", HeaderText = "Q.Liv", DataPropertyName = "AlreadyDelivered", FillWeight = 12, ReadOnly = true,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "observ", HeaderText = "Observ.", DataPropertyName = "ObservationDisplay", FillWeight = 12, ReadOnly = true,
                DefaultCellStyle = { ForeColor = Dos16.Red }
            });
            // The one editable column — "Q.Dem" (Quantite Demandee), named
            // in the real screenshot's footer hint. Pre-filled per row by
            // DeliveryEntryRepository.GetAllocationRows; the operator can
            // type over it directly, matching that hint exactly.
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "qdem", HeaderText = "Q.Dem", DataPropertyName = "QuantityToDeliver", FillWeight = 12, ReadOnly = false,
                DefaultCellStyle =
                {
                    BackColor = RetroTheme.FieldActiveBack, ForeColor = RetroTheme.FieldActiveText,
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });
            grid.CellEndEdit += (s, e) => RefreshTotal();
            // Q.Dem is bound directly to an int property and edited as
            // free text — typing something non-numeric would otherwise
            // surface as an unhandled .NET exception dialog (a real
            // WinForms DataGridView behavior, not hypothetical: the same
            // failure mode as the Int32-overflow crash found earlier in
            // ReceiptsListForm, just triggered by bad input instead of
            // big data). Reject it quietly instead and say so in-theme.
            grid.DataError += (s, e) =>
            {
                e.ThrowException = false;
                _errorLabel.Text = "Quantite Invalide";
            };
            return grid;
        }

        private static Label FieldLabel(string text) => new Label
        {
            Text = text, Dock = DockStyle.Fill, ForeColor = RetroTheme.FieldLabel, Font = RetroTheme.DosFont(10F),
            TextAlign = ContentAlignment.MiddleLeft
        };

        private static Button SmallButton(string text) => new Button
        {
            Text = text, AutoSize = true, Font = RetroTheme.DosFont(9F),
            BackColor = RetroTheme.BoxBorder, ForeColor = RetroTheme.MenuBackground, Padding = new Padding(4)
        };

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            PickService();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            if (keyData == Keys.F2) { AttemptSave(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void RefreshComputedFields()
        {
            var date = _operationDatePicker.Value.Date;
            _weekLabel.Text = DeliveryEntryRepository.WeekOfYear(date).ToString(CultureInfo.InvariantCulture);
            _yearLabel.Text = (date.Year % 100).ToString("D2", CultureInfo.InvariantCulture);
            if (_service != null)
            {
                LoadRows();
            }
        }

        private void PickService()
        {
            using (var picker = new PickerForm<ServiceLookup>(
                "Liste des Services Utilisateurs", "Rechercher (code ou nom du service) :",
                q => _serviceRepo.Search(q), s => s.Label))
            {
                if (picker.ShowDialog(this) == DialogResult.OK)
                {
                    _service = picker.Selected;
                    _serviceLabel.Text = $"{_service.Code}  {_service.Name}";
                    LoadRows();
                }
            }
        }

        private void LoadRows()
        {
            if (_service == null) return;
            var date = _operationDatePicker.Value.Date;
            var week = DeliveryEntryRepository.WeekOfYear(date);
            var rows = _entryRepo.GetAllocationRows(_service.Id, date.Year, week);
            _rows = new BindingList<DeliveryAllocationRow>(rows);
            _grid.DataSource = _rows;
            _errorLabel.Text = rows.Count == 0
                ? "Aucune dotation configuree pour ce service/cette semaine."
                : string.Empty;
            RefreshTotal();
        }

        private void RefreshTotal()
        {
            var total = _rows.Sum(r => (long)r.QuantityToDeliver * (r.UnitPriceCentimes ?? 0));
            _totalLabel.Text = $"Total : {total / 100m:N2} DZD";
        }

        private void AttemptSave()
        {
            _errorLabel.Text = string.Empty;
            try
            {
                var deliveryId = _entryRepo.Save(_service, _documentNoBox.Text, _operationDatePicker.Value.Date,
                    _rows.ToList(), _accountId);
                MessageBox.Show(this, $"Livraison enregistree (No {deliveryId}).", "EPIPHARM",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (InvalidOperationException ex)
            {
                _errorLabel.Text = ex.Message;
            }
        }
    }
}
