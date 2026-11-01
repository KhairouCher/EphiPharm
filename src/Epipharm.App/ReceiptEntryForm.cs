using System;
using System.Collections.Generic;
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
    /// "Reception de Produits : Réception Fournisseur" (MENU_MAP.md section
    /// 3) — record a new supplier delivery. This is a carbon copy of the
    /// real screen's actual interaction model, not an approximation of its
    /// look: staff who already know the legacy app by muscle memory (type
    /// a code, or press F5 to pick from a list; type the next field; Esc to
    /// finish) get exactly that here — no added buttons, no side panel to
    /// fill in before a line appears in the grid. The one and only surface
    /// is the grid itself: C.Produit / D. Perem / Numero Lot / D. Fabri /
    /// Qte Mut / Prix Uni are typed directly into row 1, Enter/Tab moves to
    /// the next cell, and a fresh blank row is always ready underneath
    /// (DataGridView's native new-row support, not a hand-built "Ajouter
    /// Ligne" flow — an earlier version of this screen had exactly that
    /// and it was a real, visible departure from the screenshot, not a
    /// detail).
    ///
    /// There is no "Enregistrer" button either: the real screens never
    /// showed one. Esc asks "Enregistrer ? (O/N)" when there's something
    /// to save — the legacy app's own confirmation convention (MENU_MAP.md
    /// "UI conventions": "Confirmation (O/N/Esc)?"), via DosConfirmForm —
    /// not an invented control.
    ///
    /// Field layout (Operation / Date Mouvement / No Commande, then Code
    /// Fournisseur / No Piece) and grid columns match the real screenshot
    /// of this screen exactly.
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
        private readonly TextBox _supplierCodeBox;
        private readonly Label _supplierNameLabel;
        private readonly DateTimePicker _receivedOnPicker;
        private readonly TextBox _orderNoBox;
        private readonly TextBox _documentNoBox;

        private readonly DataGridView _grid;
        private readonly Label _selectedProductLabel;
        private readonly Label _totalLabel;
        private readonly Label _errorLabel;

        private SupplierLookup _supplier;
        private readonly BindingList<GridRow> _rows = new BindingList<GridRow> { AllowNew = true };

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

            _header = new DosHeaderBar { Subtitle = "Reception de Produits : Réception Fournisseur" };
            _footer = new DosFooterBar();
            _footer.SetHints(("F5", "Fournisseur/Produit"), ("Suppr", "Retirer la Ligne"), ("Esc", "Sortie"));

            var headerFields = new TableLayoutPanel
            {
                Dock = DockStyle.Top, Height = 90, BackColor = RetroTheme.MenuBackground,
                ColumnCount = 6, RowCount = 2, Padding = new Padding(16, 10, 16, 10)
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
                Format = DateTimePickerFormat.Custom, CustomFormat = RetroTheme.DosDateFormat,
                Dock = DockStyle.Fill, Font = RetroTheme.DosFont(10F)
            };
            headerFields.Controls.Add(_receivedOnPicker, 3, 0);

            headerFields.Controls.Add(FieldLabel("No Commande :"), 4, 0);
            // Disabled, not omitted: the real screen has this field, but
            // purchase_orders isn't implemented yet (COMMANDE.DBF was empty
            // in the source).
            _orderNoBox = new TextBox { Dock = DockStyle.Fill, Font = RetroTheme.DosFont(10F), Enabled = false };
            headerFields.Controls.Add(_orderNoBox, 5, 0);

            headerFields.Controls.Add(FieldLabel("Code Fournisseur :"), 0, 1);
            var supplierRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, BackColor = RetroTheme.MenuBackground, ColumnCount = 2, RowCount = 1
            };
            supplierRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            supplierRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _supplierCodeBox = new TextBox { Dock = DockStyle.Fill, Font = RetroTheme.DosFont(11F) };
            _supplierCodeBox.KeyDown += SupplierCodeBox_KeyDown;
            _supplierCodeBox.Leave += (s, e) => ResolveTypedSupplier();
            _supplierNameLabel = new Label
            {
                Dock = DockStyle.Fill, ForeColor = RetroTheme.FieldLookupValue, Font = RetroTheme.DosFont(11F),
                TextAlign = ContentAlignment.MiddleLeft
            };
            supplierRow.Controls.Add(_supplierCodeBox, 0, 0);
            supplierRow.Controls.Add(_supplierNameLabel, 1, 0);
            headerFields.SetColumnSpan(supplierRow, 3);
            headerFields.Controls.Add(supplierRow, 1, 1);

            headerFields.Controls.Add(FieldLabel("No Piece :"), 4, 1);
            _documentNoBox = new TextBox { Dock = DockStyle.Fill, Font = RetroTheme.DosFont(11F) };
            headerFields.Controls.Add(_documentNoBox, 5, 1);

            _grid = BuildGrid();

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

            var totalBar = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom, Height = 40, BackColor = RetroTheme.MenuBackground,
                ColumnCount = 2, RowCount = 1, Padding = new Padding(16, 6, 16, 6)
            };
            totalBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            totalBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            _errorLabel = new Label
            {
                Dock = DockStyle.Fill, ForeColor = Dos16.LightRed, Font = RetroTheme.DosFont(10F, FontStyle.Bold)
            };
            _totalLabel = new Label
            {
                Dock = DockStyle.Fill, ForeColor = RetroTheme.FieldValue, Font = RetroTheme.DosFont(12F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight, Text = "Total : 0,00 DZD"
            };
            totalBar.Controls.Add(_errorLabel, 0, 0);
            totalBar.Controls.Add(_totalLabel, 1, 0);

            // Fill (_grid) first, then the other Dock-anchored bands,
            // nearest-to-farthest from the fill area.
            Controls.Add(_grid);
            Controls.Add(productBar);
            Controls.Add(totalBar);
            Controls.Add(headerFields);
            Controls.Add(_footer);
            Controls.Add(_header);
        }

        /// <summary>One grid row's raw typed text — parsed/resolved lazily
        /// (on cell commit for the product code, at save time for
        /// everything else), matching how a DOS form only validates a
        /// field once you leave it, not keystroke-by-keystroke.</summary>
        private class GridRow
        {
            public string ProductCode { get; set; } = string.Empty;
            public string ProductLabel { get; set; } = string.Empty;
            public int? ProductId { get; set; }
            public string ExpiresOnText { get; set; } = string.Empty;
            public string LotNumber { get; set; } = string.Empty;
            public string ManufacturedOnText { get; set; } = string.Empty;
            public string QuantityText { get; set; } = string.Empty;
            public string PriceText { get; set; } = string.Empty;

            public bool IsBlank => string.IsNullOrWhiteSpace(ProductCode);
        }

        private DataGridView BuildGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill, AllowUserToAddRows = true, AllowUserToResizeRows = false,
                AllowUserToDeleteRows = true, SelectionMode = DataGridViewSelectionMode.CellSelect,
                AutoGenerateColumns = false, RowHeadersVisible = true, RowHeadersWidth = 40,
                BorderStyle = BorderStyle.FixedSingle,
                BackgroundColor = RetroTheme.FormGridBackground, GridColor = RetroTheme.BoxBorder,
                DataSource = _rows,
                DefaultCellStyle =
                {
                    BackColor = RetroTheme.FormGridBackground, ForeColor = RetroTheme.FormGridText,
                    Font = RetroTheme.DosFont(11F), SelectionBackColor = RetroTheme.FieldActiveBack,
                    SelectionForeColor = RetroTheme.FieldActiveText
                },
                ColumnHeadersDefaultCellStyle =
                {
                    BackColor = RetroTheme.FormGridHeaderBack, ForeColor = RetroTheme.FormGridHeaderText,
                    Font = RetroTheme.DosFont(11F, FontStyle.Bold)
                },
                RowHeadersDefaultCellStyle =
                {
                    BackColor = RetroTheme.FormGridHeaderBack, ForeColor = RetroTheme.FormGridHeaderText,
                    Font = RetroTheme.DosFont(10F)
                },
                EnableHeadersVisualStyles = false
            };
            // NLig: the real screen's leftmost column is just the 1-based
            // line number — that's exactly what a DataGridView row header
            // is for, not a bound data column (nothing to type there).
            grid.RowPostPaint += (s, e) =>
            {
                var text = (e.RowIndex + 1).ToString(CultureInfo.InvariantCulture);
                var bounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, grid.RowHeadersWidth, e.RowBounds.Height);
                // DataGridView already painted its own current-row glyph
                // (a small triangle/pencil) into this rectangle before
                // RowPostPaint fires — paint over it first, or the glyph
                // shows through behind the line number (caught by actually
                // looking at a screenshot, not obvious from the code).
                using (var backBrush = new SolidBrush(grid.RowHeadersDefaultCellStyle.BackColor))
                {
                    e.Graphics.FillRectangle(backBrush, bounds);
                }
                TextRenderer.DrawText(e.Graphics, text, grid.RowHeadersDefaultCellStyle.Font, bounds,
                    grid.RowHeadersDefaultCellStyle.ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };

            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "cproduit", HeaderText = "C.Produit", DataPropertyName = "ProductCode", FillWeight = 16 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "perem", HeaderText = "D. Perem", DataPropertyName = "ExpiresOnText", FillWeight = 14 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "lot", HeaderText = "Numero Lot", DataPropertyName = "LotNumber", FillWeight = 18 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "fabri", HeaderText = "D. Fabri", DataPropertyName = "ManufacturedOnText", FillWeight = 14 });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "qte", HeaderText = "Qte Mut", DataPropertyName = "QuantityText", FillWeight = 14,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "prix", HeaderText = "Prix Uni", DataPropertyName = "PriceText", FillWeight = 16,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            grid.CellEndEdit += Grid_CellEndEdit;
            grid.CellEnter += (s, e) => UpdateSelectedProductLabel(e.RowIndex);
            grid.KeyDown += Grid_KeyDown;
            grid.DataError += (s, e) => { e.ThrowException = false; };
            return grid;
        }

        private void Grid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (_grid.Columns[e.ColumnIndex].Name != "cproduit")
            {
                RefreshTotal();
                return;
            }
            ResolveTypedProduct(e.RowIndex);
            RefreshTotal();
        }

        private void Grid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && _grid.CurrentCell != null && !_grid.IsCurrentCellInEditMode)
            {
                var deleteRowIndex = _grid.CurrentCell.RowIndex;
                if (deleteRowIndex >= 0 && deleteRowIndex < _rows.Count)
                {
                    _rows.RemoveAt(deleteRowIndex);
                    RefreshTotal();
                }
                e.Handled = true;
                return;
            }

            if (e.KeyCode != Keys.F5 || _grid.CurrentCell == null) return;
            if (_grid.Columns[_grid.CurrentCell.ColumnIndex].Name != "cproduit") return;

            var rowIndex = _grid.CurrentCell.RowIndex;
            using (var picker = new PickerForm<ProductLookup>(
                "Liste Produits/Code ou Alpha", "Rechercher (nom du produit, DCI ou code) :",
                q => _productRepo.Search(q), p => $"{p.LegacyCode}  {p.Label}"))
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;

                var chosen = picker.Selected;
                if (rowIndex >= _rows.Count)
                {
                    // Picking via F5 on the always-present blank "new row"
                    // adds it to the real list first, same as typing would.
                    _rows.Add(new GridRow());
                    rowIndex = _rows.Count - 1;
                }
                var row = _rows[rowIndex];
                row.ProductCode = chosen.Code;
                row.ProductId = chosen.Id;
                row.ProductLabel = chosen.Label;
                _rows.ResetItem(rowIndex);
                UpdateSelectedProductLabel(rowIndex);
            }
            e.Handled = true;
        }

        private void ResolveTypedProduct(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _rows.Count) return;
            var row = _rows[rowIndex];
            if (string.IsNullOrWhiteSpace(row.ProductCode))
            {
                row.ProductId = null;
                row.ProductLabel = string.Empty;
                return;
            }

            var found = _productRepo.FindByCode(row.ProductCode.Trim());
            if (found == null)
            {
                row.ProductId = null;
                row.ProductLabel = string.Empty;
                _errorLabel.Text = "Produit Inexistant";
            }
            else
            {
                row.ProductId = found.Id;
                row.ProductCode = found.Code;
                row.ProductLabel = found.Label;
                _errorLabel.Text = string.Empty;
            }
            _rows.ResetItem(rowIndex);
            UpdateSelectedProductLabel(rowIndex);
        }

        private void UpdateSelectedProductLabel(int rowIndex)
        {
            _selectedProductLabel.Text = rowIndex >= 0 && rowIndex < _rows.Count
                ? _rows[rowIndex].ProductLabel
                : string.Empty;
        }

        private void RefreshTotal()
        {
            decimal total = 0;
            foreach (var row in _rows)
            {
                if (int.TryParse(row.QuantityText, out var qty) &&
                    decimal.TryParse(row.PriceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
                {
                    total += qty * price;
                }
            }
            _totalLabel.Text = $"Total : {total:N2} DZD";
        }

        private static Label FieldLabel(string text) => new Label
        {
            Text = text, Dock = DockStyle.Fill, ForeColor = RetroTheme.FieldLabel, Font = RetroTheme.DosFont(10F),
            TextAlign = ContentAlignment.MiddleLeft
        };

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _supplierCodeBox.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { AttemptCloseWithConfirm(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void SupplierCodeBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5)
            {
                PickSupplier();
                e.Handled = true;
            }
        }

        private void PickSupplier()
        {
            using (var picker = new PickerForm<SupplierLookup>(
                "Liste des Fournisseurs", "Rechercher (sigle, raison sociale ou code) :",
                q => _supplierRepo.Search(q), s => s.Label))
            {
                if (picker.ShowDialog(this) == DialogResult.OK)
                {
                    SetSupplier(picker.Selected);
                }
            }
        }

        private void ResolveTypedSupplier()
        {
            var typed = _supplierCodeBox.Text.Trim();
            if (string.IsNullOrEmpty(typed))
            {
                _supplier = null;
                _supplierNameLabel.Text = string.Empty;
                return;
            }
            var found = _supplierRepo.FindByFiveDigitCode(typed);
            if (found == null)
            {
                _supplier = null;
                _supplierNameLabel.Text = string.Empty;
                _errorLabel.Text = "Fournisseur Inexistant";
            }
            else
            {
                SetSupplier(found);
            }
        }

        private void SetSupplier(SupplierLookup supplier)
        {
            _supplier = supplier;
            _supplierCodeBox.Text = _supplierRepo.LegacyFiveDigitCode(supplier.LegacyCode);
            // Sigle alone, not Label ("sigle - name"): the real screenshot
            // shows only the sigle after the code (FOURNIS.SIGLE,
            // confirmed against the real data).
            _supplierNameLabel.Text = supplier.Sigle;
            _errorLabel.Text = string.Empty;
        }

        private void AttemptCloseWithConfirm()
        {
            var hasContent = _rows.Any(r => !r.IsBlank) || _supplier != null ||
                              !string.IsNullOrWhiteSpace(_documentNoBox.Text);
            if (!hasContent)
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
            var lines = new List<ReceiptEntryLine>();

            foreach (var row in _rows)
            {
                if (row.IsBlank) continue;

                if (row.ProductId == null)
                {
                    _errorLabel.Text = "Produit Inexistant";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(row.LotNumber))
                {
                    _errorLabel.Text = "Numero de Lot Obligatoire";
                    return false;
                }
                if (!int.TryParse(row.QuantityText, out var qty) || qty <= 0)
                {
                    _errorLabel.Text = "La Quantite doit etre Positive";
                    return false;
                }
                decimal price = 0;
                if (!string.IsNullOrWhiteSpace(row.PriceText) &&
                    !decimal.TryParse(row.PriceText, NumberStyles.Number, CultureInfo.InvariantCulture, out price))
                {
                    _errorLabel.Text = "Prix Invalide";
                    return false;
                }
                if (!TryParseDosDate(row.ExpiresOnText, out var expiresOn))
                {
                    _errorLabel.Text = "Date de Peremption Invalide";
                    return false;
                }
                if (!TryParseDosDate(row.ManufacturedOnText, out var manufacturedOn))
                {
                    _errorLabel.Text = "Date de Fabrication Invalide";
                    return false;
                }

                lines.Add(new ReceiptEntryLine
                {
                    ProductId = row.ProductId.Value,
                    ProductCode = row.ProductCode,
                    ProductLabel = row.ProductLabel,
                    LotNumber = row.LotNumber.Trim(),
                    ExpiresOn = expiresOn,
                    ManufacturedOn = manufacturedOn,
                    Quantity = qty,
                    UnitPriceCentimes = (int)(price * 100m)
                });
            }

            try
            {
                var receiptId = _entryRepo.Save(_supplier, _documentNoBox.Text, _receivedOnPicker.Value.Date,
                    lines, _accountId);
                _errorLabel.ForeColor = RetroTheme.FieldValue;
                _errorLabel.Text = $"Reception enregistree (No {receiptId}).";
                return true;
            }
            catch (InvalidOperationException ex)
            {
                _errorLabel.Text = ex.Message;
                return false;
            }
        }

        /// <summary>Parses the legacy DD/MM/YY date format (see
        /// RetroTheme.DosDateFormat); blank is valid (no date entered).</summary>
        private static bool TryParseDosDate(string text, out DateTime? result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(text)) return true;
            if (!DateTime.TryParseExact(text.Trim(), new[] { "dd/MM/yy", "dd/MM/yyyy", "d/M/yy", "d/M/yyyy" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return false;
            }
            result = parsed;
            return true;
        }
    }
}
