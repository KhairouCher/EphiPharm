using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Epipharm.Data;
using Epipharm.App.Theme;

namespace Epipharm.App
{
    /// <summary>
    /// "Reception de Produits : Reception Fournisseur" (MENU_MAP.md section
    /// 3) — record a new supplier delivery: pick the supplier and a date/
    /// piece number, add one line per product/lot, save. Saving increases
    /// the matching stock_lots row(s) — see ReceiptEntryRepository for the
    /// legacy rule this follows ("a product can only enter stock through a
    /// supplier receipt", MENU_MAP.md section 1) and what's this rebuild's
    /// own choice versus confirmed from the real data.
    ///
    /// Field layout follows the real screenshot of this screen in
    /// UI_SPEC.md (Code Fournisseur / Date Mouvement / No Piece, then a
    /// grid of NLig/C.Produit/D.Perem/Numero Lot/D.Fabri/Qte Mut/Prix Uni) —
    /// the add-a-line sub-panel below the grid is this rebuild's own
    /// addition to make data entry possible without a WinForms designer or
    /// an in-place editable grid, not something the screenshot shows.
    /// </summary>
    public class ReceiptEntryForm : Form
    {
        private readonly ReceiptEntryRepository _entryRepo;
        private readonly SupplierRepository _supplierRepo;
        private readonly ProductRepository _productRepo;
        private readonly int _accountId;

        private readonly DosHeaderBar _header;
        private readonly DosFooterBar _footer;
        private readonly Label _operationLabel;
        private readonly Label _supplierLabel;
        private readonly DateTimePicker _receivedOnPicker;
        private readonly TextBox _orderNoBox;
        private readonly TextBox _documentNoBox;

        private readonly Label _productLabel;
        private readonly TextBox _lotBox;
        private readonly DateTimePicker _expiresOnPicker;
        private readonly DateTimePicker _manufacturedOnPicker;
        private readonly NumericUpDown _quantityBox;
        private readonly NumericUpDown _priceBox;

        private readonly DataGridView _linesGrid;
        private readonly Label _selectedProductLabel;
        private readonly Label _totalLabel;
        private readonly Label _errorLabel;

        private SupplierLookup _supplier;
        private ProductLookup _product;
        private readonly List<ReceiptEntryLine> _lines = new List<ReceiptEntryLine>();

        public ReceiptEntryForm(ReceiptEntryRepository entryRepo, SupplierRepository supplierRepo,
            ProductRepository productRepo, int accountId)
        {
            _entryRepo = entryRepo ?? throw new ArgumentNullException(nameof(entryRepo));
            _supplierRepo = supplierRepo ?? throw new ArgumentNullException(nameof(supplierRepo));
            _productRepo = productRepo ?? throw new ArgumentNullException(nameof(productRepo));
            _accountId = accountId;

            Text = "EPIPHARM — Reception de Produits";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1040, 700);
            MinimumSize = new Size(900, 600);
            KeyPreview = true;
            BackColor = RetroTheme.MenuBackground;

            // Exact subtitle text from the real screenshot, accent
            // inconsistency and all: the legacy app itself spells
            // "Reception" plain the first time and "Réception" accented
            // the second, in the same line — not a transcription slip here.
            _header = new DosHeaderBar { Subtitle = "Reception de Produits : Réception Fournisseur" };
            _footer = new DosFooterBar();
            _footer.SetHints(("F5", "Choisir Fournisseur/Produit"), ("Suppr", "Retirer la Ligne"),
                ("F2", "Enregistrer"), ("Esc", "Sortie"));

            // Field grouping/order/labels match the real screenshot of this
            // screen exactly: row 1 is Operation / Date Mouvement / No
            // Commande, row 2 is Code Fournisseur (code + resolved name
            // inline) / No Piece — not a generic 2x2 grid guessed at.
            var headerFields = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 90,
                BackColor = RetroTheme.MenuBackground,
                ColumnCount = 6,
                RowCount = 2,
                Padding = new Padding(16, 10, 16, 10)
            };
            headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24F));
            headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18F));
            headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18F));
            headerFields.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            headerFields.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            headerFields.Controls.Add(FieldLabel("Operation :"), 0, 0);
            _operationLabel = new Label
            {
                Text = _entryRepo.PeekNextOperationNo().ToString(CultureInfo.InvariantCulture),
                Dock = DockStyle.Fill, ForeColor = RetroTheme.FieldValue, Font = RetroTheme.DosFont(11F)
            };
            headerFields.Controls.Add(_operationLabel, 1, 0);

            headerFields.Controls.Add(FieldLabel("Date Mouvement :"), 2, 0);
            _receivedOnPicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short, Dock = DockStyle.Fill, Font = RetroTheme.DosFont(10F)
            };
            headerFields.Controls.Add(_receivedOnPicker, 3, 0);

            headerFields.Controls.Add(FieldLabel("No Commande :"), 4, 0);
            // Disabled, not omitted: the real screen has this field, but
            // purchase_orders isn't implemented yet (COMMANDE.DBF was empty
            // in the source — see scripts/migrate_to_app_schema.py), so
            // there's nothing to link to. Keeping the box, greyed out,
            // matches "every box" without pretending it works.
            _orderNoBox = new TextBox
            {
                Dock = DockStyle.Fill, Font = RetroTheme.DosFont(10F), Enabled = false,
                Text = string.Empty
            };
            headerFields.Controls.Add(_orderNoBox, 5, 0);

            headerFields.Controls.Add(FieldLabel("Code Fournisseur :"), 0, 1);
            var supplierRow = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = RetroTheme.MenuBackground };
            _supplierLabel = new Label
            {
                Text = "(non choisi)", AutoSize = false, Width = 280, Height = 26,
                ForeColor = RetroTheme.FieldLookupValue, Font = RetroTheme.DosFont(11F), TextAlign = ContentAlignment.MiddleLeft
            };
            var supplierButton = SmallButton("Choisir (F5)");
            supplierButton.Click += (s, e) => PickSupplier();
            supplierRow.Controls.Add(_supplierLabel);
            supplierRow.Controls.Add(supplierButton);
            headerFields.SetColumnSpan(supplierRow, 3);
            headerFields.Controls.Add(supplierRow, 1, 1);

            headerFields.Controls.Add(FieldLabel("No Piece :"), 4, 1);
            _documentNoBox = new TextBox { Dock = DockStyle.Fill, Font = RetroTheme.DosFont(11F) };
            headerFields.Controls.Add(_documentNoBox, 5, 1);

            var lineEntry = BuildLineEntryPanel(out _productLabel, out _lotBox, out _expiresOnPicker,
                out _manufacturedOnPicker, out _quantityBox, out _priceBox);

            _linesGrid = BuildLinesGrid();

            // The real screenshot shows a "PRODUIT :" line directly under
            // the grid — the resolved name for the currently-selected
            // line, since the grid's own C.Produit column only shows the
            // code (see BuildLinesGrid / UpdateSelectedProductLabel).
            var productBar = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom, Height = 26, BackColor = RetroTheme.MenuBackground,
                ColumnCount = 2, RowCount = 1, Padding = new Padding(16, 2, 16, 2)
            };
            productBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            productBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            productBar.Controls.Add(FieldLabel("Produit :"), 0, 0);
            _selectedProductLabel = new Label
            {
                Dock = DockStyle.Fill, ForeColor = RetroTheme.FieldLookupValue, Font = RetroTheme.DosFont(10F)
            };
            productBar.Controls.Add(_selectedProductLabel, 1, 0);

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

            // Fill (_linesGrid) first, then the other Dock-anchored bands,
            // nearest-to-farthest from the fill area — same ordering
            // convention used throughout Epipharm.App.
            Controls.Add(_linesGrid);
            Controls.Add(productBar);
            Controls.Add(bottomBar);
            Controls.Add(lineEntry);
            Controls.Add(headerFields);
            Controls.Add(_footer);
            Controls.Add(_header);
        }

        private Panel BuildLineEntryPanel(out Label productLabel, out TextBox lotBox,
            out DateTimePicker expiresOnPicker, out DateTimePicker manufacturedOnPicker,
            out NumericUpDown quantityBox, out NumericUpDown priceBox)
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Top, Height = 92, BackColor = RetroTheme.FieldActiveBack,
                ColumnCount = 7, RowCount = 2, Padding = new Padding(16, 8, 16, 8)
            };
            for (var i = 0; i < 6; i++) panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

            Label Caption(string text) => new Label
            {
                Text = text, Dock = DockStyle.Fill, ForeColor = Dos16.White, Font = RetroTheme.DosFont(9F)
            };

            panel.Controls.Add(Caption("Produit"), 0, 0);
            panel.Controls.Add(Caption("Lot"), 1, 0);
            panel.Controls.Add(Caption("Peremption"), 2, 0);
            panel.Controls.Add(Caption("Fabrication"), 3, 0);
            panel.Controls.Add(Caption("Qte"), 4, 0);
            panel.Controls.Add(Caption("Prix Unit."), 5, 0);

            var productRow = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = RetroTheme.FieldActiveBack };
            productLabel = new Label
            {
                Text = "(non choisi)", AutoSize = false, Width = 140, Height = 24,
                ForeColor = Dos16.White, Font = RetroTheme.DosFont(10F)
            };
            var productButton = SmallButton("F5");
            productButton.Click += (s, e) => PickProduct();
            productRow.Controls.Add(productLabel);
            productRow.Controls.Add(productButton);
            panel.Controls.Add(productRow, 0, 1);

            lotBox = new TextBox { Dock = DockStyle.Fill, Font = RetroTheme.DosFont(10F) };
            panel.Controls.Add(lotBox, 1, 1);

            expiresOnPicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false,
                Dock = DockStyle.Fill, Font = RetroTheme.DosFont(9F)
            };
            panel.Controls.Add(expiresOnPicker, 2, 1);

            manufacturedOnPicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false,
                Dock = DockStyle.Fill, Font = RetroTheme.DosFont(9F)
            };
            panel.Controls.Add(manufacturedOnPicker, 3, 1);

            quantityBox = new NumericUpDown
            {
                Minimum = 0, Maximum = 1000000, Dock = DockStyle.Fill, Font = RetroTheme.DosFont(10F)
            };
            panel.Controls.Add(quantityBox, 4, 1);

            priceBox = new NumericUpDown
            {
                Minimum = 0, Maximum = 100000000, DecimalPlaces = 2, Increment = 0.01m,
                Dock = DockStyle.Fill, Font = RetroTheme.DosFont(10F)
            };
            panel.Controls.Add(priceBox, 5, 1);

            var addButton = SmallButton("Ajouter Ligne");
            addButton.Dock = DockStyle.Fill;
            addButton.Click += (s, e) => AddLine();
            panel.Controls.Add(addButton, 6, 1);

            return panel;
        }

        /// <summary>
        /// Columns match the real screenshot exactly — name, order, and
        /// abbreviation: NLig | C.Produit | D. Perem | Numero Lot |
        /// D. Fabri | Qte Mut | Prix Uni. C.Produit shows the legacy
        /// product *code*, like the real screen does, not a resolved name
        /// — the resolved name for the selected line is shown separately
        /// below the grid as "Produit :", matching the real screen's own
        /// footer convention (so the data is still there, just where the
        /// legacy screen actually puts it).
        /// </summary>
        private DataGridView BuildLinesGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoGenerateColumns = false,
                RowHeadersVisible = false, BorderStyle = BorderStyle.FixedSingle,
                BackgroundColor = RetroTheme.FormGridBackground, GridColor = RetroTheme.BoxBorder,
                DefaultCellStyle =
                {
                    BackColor = RetroTheme.FormGridBackground, ForeColor = RetroTheme.FormGridText,
                    // 11F, not 10F: matches every other grid in the app
                    // (StockListForm, ReceiptsListForm, DeliveriesListForm)
                    // — also fixes a real rendering glitch found by actually
                    // looking at a screenshot: the bundled bitmap font drew
                    // "NLig" as what reads like "MLig" at 10F bold, since
                    // that's not one of the pixel-grid sizes the font was
                    // designed for; 11F is already proven to render cleanly
                    // elsewhere in this app.
                    Font = RetroTheme.DosFont(11F), SelectionBackColor = RetroTheme.MenuItemSelectedBack,
                    SelectionForeColor = RetroTheme.MenuItemSelectedText
                },
                ColumnHeadersDefaultCellStyle =
                {
                    BackColor = RetroTheme.FormGridHeaderBack, ForeColor = RetroTheme.FormGridHeaderText,
                    // Bold requested for consistency with every other grid
                    // header in the app (DosFont silently ignores it for
                    // the bundled font — see DosFont's doc comment).
                    Font = RetroTheme.DosFont(11F, FontStyle.Bold)
                },
                EnableHeadersVisualStyles = false
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "nlig", HeaderText = "NLig", DataPropertyName = "LineNo", FillWeight = 8,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "cproduit", HeaderText = "C.Produit", DataPropertyName = "ProductCode", FillWeight = 16 });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "perem", HeaderText = "D. Perem", DataPropertyName = "ExpiresOn", FillWeight = 14,
                DefaultCellStyle = { Format = "dd/MM/yy" }
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "lot", HeaderText = "Numero Lot", DataPropertyName = "LotNumber", FillWeight = 18 });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "fabri", HeaderText = "D. Fabri", DataPropertyName = "ManufacturedOn", FillWeight = 14,
                DefaultCellStyle = { Format = "dd/MM/yy" }
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "qte", HeaderText = "Qte Mut", DataPropertyName = "Quantity", FillWeight = 14,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "prix", HeaderText = "Prix Uni", DataPropertyName = "UnitPriceDisplay", FillWeight = 16,
                DefaultCellStyle = { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            grid.KeyDown += (s, e) =>
            {
                // The grid is bound to an anonymous projection (for display
                // formatting — see RefreshLines), not ReceiptEntryLine
                // itself, so the real line to remove comes from the row's
                // Tag (set in RefreshLines), not DataBoundItem.
                if (e.KeyCode == Keys.Delete && grid.CurrentRow?.Tag is ReceiptEntryLine line)
                {
                    _lines.Remove(line);
                    RefreshLines();
                }
            };
            grid.SelectionChanged += (s, e) => UpdateSelectedProductLabel();
            return grid;
        }

        /// <summary>The "PRODUIT :" line under the grid on the real
        /// screenshot — the resolved name for whichever line is currently
        /// selected, since the grid itself only shows the product code.</summary>
        private void UpdateSelectedProductLabel()
        {
            _selectedProductLabel.Text = _linesGrid.CurrentRow?.Tag is ReceiptEntryLine line
                ? line.ProductLabel
                : string.Empty;
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
            PickSupplier();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            if (keyData == Keys.F2) { AttemptSave(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void PickSupplier()
        {
            using (var picker = new PickerForm<SupplierLookup>(
                "Liste des Fournisseurs", "Rechercher (sigle, raison sociale ou code) :",
                q => _supplierRepo.Search(q), s => s.Label))
            {
                if (picker.ShowDialog(this) == DialogResult.OK)
                {
                    _supplier = picker.Selected;
                    // "<code>  <sigle>", matching the real screenshot
                    // exactly: "21312  S.A.R.L FARABI M.S" — the typed
                    // code, two spaces, then FOURNIS.SIGLE (confirmed
                    // against the real data; FOURNIS.LIBELLE for that row
                    // is the longer "SARL FARABI SETIF M.S", not shown here).
                    _supplierLabel.Text = $"{_supplierRepo.LegacyFiveDigitCode(_supplier.LegacyCode)}  {_supplier.Sigle}";
                }
            }
        }

        private void PickProduct()
        {
            using (var picker = new PickerForm<ProductLookup>(
                "Liste Produits/Code ou Alpha", "Rechercher (nom du produit, DCI ou code) :",
                q => _productRepo.Search(q), p => $"{p.LegacyCode}  {p.Label}"))
            {
                if (picker.ShowDialog(this) == DialogResult.OK)
                {
                    _product = picker.Selected;
                    _productLabel.Text = _product.Label;
                }
            }
        }

        private void AddLine()
        {
            _errorLabel.Text = string.Empty;
            if (_product == null)
            {
                _errorLabel.Text = "Produit Obligatoire";
                return;
            }
            if (string.IsNullOrWhiteSpace(_lotBox.Text))
            {
                _errorLabel.Text = "Numero de Lot Obligatoire";
                return;
            }
            if (_quantityBox.Value <= 0)
            {
                _errorLabel.Text = "La Quantite doit etre Positive";
                return;
            }
            if (_lines.Any(l => l.ProductId == _product.Id && l.LotNumber == _lotBox.Text.Trim()))
            {
                _errorLabel.Text = "Produit Existe Deja Mouvemente (meme lot, cette reception)";
                return;
            }

            _lines.Add(new ReceiptEntryLine
            {
                ProductId = _product.Id,
                ProductCode = _product.LegacyCode,
                ProductLabel = _product.Label,
                LotNumber = _lotBox.Text.Trim(),
                ExpiresOn = _expiresOnPicker.Checked ? _expiresOnPicker.Value.Date : (DateTime?)null,
                ManufacturedOn = _manufacturedOnPicker.Checked ? _manufacturedOnPicker.Value.Date : (DateTime?)null,
                Quantity = (int)_quantityBox.Value,
                UnitPriceCentimes = (int)(_priceBox.Value * 100m)
            });

            _product = null;
            _productLabel.Text = "(non choisi)";
            _lotBox.Clear();
            _expiresOnPicker.Checked = false;
            _manufacturedOnPicker.Checked = false;
            _quantityBox.Value = 0;
            _priceBox.Value = 0;
            RefreshLines();
        }

        private void RefreshLines()
        {
            _linesGrid.DataSource = null;
            _linesGrid.DataSource = _lines.Select((l, i) => new
            {
                LineNo = i + 1,
                l.ProductCode,
                l.ExpiresOn,
                l.LotNumber,
                l.ManufacturedOn,
                l.Quantity,
                UnitPriceDisplay = l.UnitPriceCentimes / 100m
            }).ToList();
            // Keep the real ReceiptEntryLine objects reachable for Delete-key
            // removal and the "Produit :" line (see UpdateSelectedProductLabel)
            // — the grid above is bound to an anonymous projection for
            // display formatting, so re-bind the row tag separately.
            for (var i = 0; i < _linesGrid.Rows.Count && i < _lines.Count; i++)
            {
                _linesGrid.Rows[i].Tag = _lines[i];
            }
            var total = _lines.Sum(l => l.LineTotal);
            _totalLabel.Text = $"Total : {total:N2} DZD";
            UpdateSelectedProductLabel();
        }

        private void AttemptSave()
        {
            _errorLabel.Text = string.Empty;
            try
            {
                var receiptId = _entryRepo.Save(_supplier, _documentNoBox.Text, _receivedOnPicker.Value.Date,
                    _lines, _accountId);
                MessageBox.Show(this, $"Reception enregistree (No {receiptId}).", "EPIPHARM",
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
