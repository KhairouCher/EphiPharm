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
    /// "Edition Commande" (MENU_MAP.md section 4) — create a new purchase
    /// order. No legacy screenshot of this exact screen (and no real
    /// historical order to check field names against either — see
    /// CommandeRepository's doc comment), so this follows the same
    /// confirmed pattern every other entry screen in this rebuild actually
    /// uses: no buttons, type a supplier code (or F5) and DCI lines
    /// directly into the grid, Esc asks "Enregistrer ? (O/N)".
    /// </summary>
    public class CommandeEntryForm : Form
    {
        private readonly CommandeRepository _entryRepo;
        private readonly SupplierRepository _supplierRepo;
        private readonly DciRepository _dciRepo;
        private readonly int _accountId;

        private readonly DosHeaderBar _header;
        private readonly DosFooterBar _footer;
        private readonly TextBox _supplierCodeBox;
        private readonly Label _supplierNameLabel;
        private readonly DateTimePicker _orderedOnPicker;

        private readonly DataGridView _grid;
        private readonly Label _errorLabel;

        private SupplierLookup _supplier;
        private readonly BindingList<GridRow> _rows = new BindingList<GridRow> { AllowNew = true };

        public CommandeEntryForm(CommandeRepository entryRepo, SupplierRepository supplierRepo,
            DciRepository dciRepo, int accountId)
        {
            _entryRepo = entryRepo ?? throw new ArgumentNullException(nameof(entryRepo));
            _supplierRepo = supplierRepo ?? throw new ArgumentNullException(nameof(supplierRepo));
            _dciRepo = dciRepo ?? throw new ArgumentNullException(nameof(dciRepo));
            _accountId = accountId;

            Text = "EPIPHARM — Edition Commande";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(900, 620);
            MinimumSize = new Size(760, 500);
            KeyPreview = true;
            BackColor = RetroTheme.MenuBackground;

            _header = new DosHeaderBar { Subtitle = "Edition Commande" };
            _footer = new DosFooterBar();
            _footer.SetHints(("F5", "Fournisseur/DCI"), ("Suppr", "Retirer la Ligne"), ("Esc", "Sortie"));

            var headerFields = new TableLayoutPanel
            {
                Dock = DockStyle.Top, Height = 40, BackColor = RetroTheme.MenuBackground,
                ColumnCount = 4, RowCount = 1, Padding = new Padding(16, 6, 16, 6)
            };
            headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
            headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            // Without an explicit RowStyle, this single row's height was
            // collapsing to near-zero (caught by actually looking at a
            // screenshot — the field labels were entirely invisible,
            // blank blue band), unlike every other header in this app,
            // which always sets RowStyles even for a 1-row panel.
            headerFields.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            headerFields.Controls.Add(FieldLabel("Code Fournisseur :"), 0, 0);
            var supplierRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, BackColor = RetroTheme.MenuBackground, ColumnCount = 2, RowCount = 1
            };
            supplierRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            supplierRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _supplierCodeBox = new TextBox { Dock = DockStyle.Fill, Font = RetroTheme.DosFont(11F) };
            _supplierCodeBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.F5) { PickSupplier(); e.Handled = true; } };
            _supplierCodeBox.Leave += (s, e) => ResolveTypedSupplier();
            _supplierNameLabel = new Label
            {
                Dock = DockStyle.Fill, ForeColor = RetroTheme.FieldLookupValue, Font = RetroTheme.DosFont(11F),
                TextAlign = ContentAlignment.MiddleLeft
            };
            supplierRow.Controls.Add(_supplierCodeBox, 0, 0);
            supplierRow.Controls.Add(_supplierNameLabel, 1, 0);
            headerFields.Controls.Add(supplierRow, 1, 0);

            headerFields.Controls.Add(FieldLabel("Date Commande :"), 2, 0);
            _orderedOnPicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom, CustomFormat = RetroTheme.DosDateFormat,
                Dock = DockStyle.Fill, Font = RetroTheme.DosFont(10F)
            };
            headerFields.Controls.Add(_orderedOnPicker, 3, 0);

            _grid = BuildGrid();

            var errorBar = new Panel { Dock = DockStyle.Bottom, Height = 30, BackColor = RetroTheme.MenuBackground };
            _errorLabel = new Label
            {
                Dock = DockStyle.Fill, ForeColor = Dos16.LightRed, Font = RetroTheme.DosFont(10F, FontStyle.Bold),
                Padding = new Padding(16, 4, 0, 0)
            };
            errorBar.Controls.Add(_errorLabel);

            Controls.Add(_grid);
            Controls.Add(errorBar);
            Controls.Add(headerFields);
            Controls.Add(_footer);
            Controls.Add(_header);
        }

        private class GridRow
        {
            public string DciCode { get; set; } = string.Empty;
            public string DciLabel { get; set; } = string.Empty;
            public int? DciId { get; set; }
            public string QuantityText { get; set; } = string.Empty;

            public bool IsBlank => string.IsNullOrWhiteSpace(DciCode);
        }

        private DataGridView BuildGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill, AllowUserToAddRows = true, AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect, AutoGenerateColumns = false,
                RowHeadersVisible = false, BorderStyle = BorderStyle.FixedSingle,
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
                EnableHeadersVisualStyles = false
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "dci", HeaderText = "DCI", DataPropertyName = "DciCode", FillWeight = 14 });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "libelle", HeaderText = "Libelle DCI/Forme", DataPropertyName = "DciLabel", FillWeight = 56, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "qte", HeaderText = "Qte Commandee", DataPropertyName = "QuantityText", FillWeight = 30,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            grid.CellEndEdit += (s, e) => { if (grid.Columns[e.ColumnIndex].Name == "dci") ResolveTypedDci(e.RowIndex); };
            grid.KeyDown += Grid_KeyDown;
            grid.DataError += (s, e) => { e.ThrowException = false; };
            return grid;
        }

        private void Grid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && _grid.CurrentCell != null && !_grid.IsCurrentCellInEditMode)
            {
                var deleteRowIndex = _grid.CurrentCell.RowIndex;
                if (deleteRowIndex >= 0 && deleteRowIndex < _rows.Count)
                {
                    _rows.RemoveAt(deleteRowIndex);
                }
                e.Handled = true;
                return;
            }

            if (e.KeyCode != Keys.F5 || _grid.CurrentCell == null) return;
            if (_grid.Columns[_grid.CurrentCell.ColumnIndex].Name != "dci") return;

            var rowIndex = _grid.CurrentCell.RowIndex;
            using (var picker = new PickerForm<DciLookup>(
                "Liste des Code DCI (Libelle + Forme et Dosage)", "Rechercher (nom ou code) :",
                q => _dciRepo.Search(q), d => $"{d.FormattedCode}  {d.Name}"))
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;

                var chosen = picker.Selected;
                if (rowIndex >= _rows.Count)
                {
                    _rows.Add(new GridRow());
                    rowIndex = _rows.Count - 1;
                }
                var row = _rows[rowIndex];
                row.DciCode = chosen.FormattedCode;
                row.DciId = chosen.Id;
                row.DciLabel = chosen.Name;
                _rows.ResetItem(rowIndex);
            }
            e.Handled = true;
        }

        private void ResolveTypedDci(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _rows.Count) return;
            var row = _rows[rowIndex];
            if (string.IsNullOrWhiteSpace(row.DciCode))
            {
                row.DciId = null;
                row.DciLabel = string.Empty;
                return;
            }

            var found = _dciRepo.FindByCode(row.DciCode.Trim());
            if (found == null)
            {
                row.DciId = null;
                row.DciLabel = string.Empty;
                _errorLabel.Text = "D.C.I Inexistant(e)";
            }
            else
            {
                row.DciId = found.Id;
                row.DciCode = found.FormattedCode;
                row.DciLabel = found.Name;
                _errorLabel.Text = string.Empty;
            }
            _rows.ResetItem(rowIndex);
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
            _supplierNameLabel.Text = supplier.Sigle;
            _errorLabel.Text = string.Empty;
        }

        private void AttemptCloseWithConfirm()
        {
            var hasContent = _rows.Any(r => !r.IsBlank) || _supplier != null;
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
            var lines = new List<CommandeLine>();

            foreach (var row in _rows)
            {
                if (row.IsBlank) continue;

                if (row.DciId == null)
                {
                    _errorLabel.Text = "D.C.I Inexistant(e)";
                    return false;
                }
                if (!int.TryParse(row.QuantityText, out var qty) || qty <= 0)
                {
                    _errorLabel.Text = "La Quantite doit etre Positive";
                    return false;
                }

                lines.Add(new CommandeLine
                {
                    DciId = row.DciId.Value,
                    DciCode = row.DciCode,
                    DciLabel = row.DciLabel,
                    Quantity = qty
                });
            }

            try
            {
                var orderId = _entryRepo.Save(_supplier, _orderedOnPicker.Value.Date, lines, _accountId);
                _errorLabel.ForeColor = RetroTheme.FieldValue;
                _errorLabel.Text = $"Commande enregistree (No {orderId}).";
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
