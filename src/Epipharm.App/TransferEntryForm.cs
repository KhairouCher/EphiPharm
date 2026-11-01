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
    /// "Transfert Inter-Etablissement" (MENU_MAP.md section 2) — send
    /// stock to another establishment. Same carbon-copy approach as
    /// ReceiptEntryForm: no buttons, the grid itself is the only entry
    /// surface (DCI typed directly, Tab/Enter to the next cell, a fresh
    /// blank row always ready), Esc asks "Enregistrer ? (O/N)".
    ///
    /// The real screenshot's "Code Service" field is actually an
    /// establishment code here (confirmed: "23122010"/"09091" resolve to
    /// real centers table rows, e.g. "C.H.U BLIDA") — see
    /// TransferEntryRepository for why this writes service_id as NULL
    /// rather than force it into the wrong foreign key.
    ///
    /// Field layout (Code Service / Date Operation / No de Piece, then
    /// No Operation) and grid columns (DCI / Libelle DCI/Forme / Qte Dem /
    /// Obs.) match the real screenshot exactly.
    /// </summary>
    public class TransferEntryForm : Form
    {
        private readonly TransferEntryRepository _entryRepo;
        private readonly CenterRepository _centerRepo;
        private readonly DciRepository _dciRepo;
        private readonly int _accountId;

        private readonly DosHeaderBar _header;
        private readonly DosFooterBar _footer;
        private readonly Label _operationLabel;
        private readonly TextBox _centerCodeBox;
        private readonly Label _centerNameLabel;
        private readonly DateTimePicker _operationDatePicker;
        private readonly TextBox _documentNoBox;

        private readonly DataGridView _grid;
        private readonly Label _errorLabel;

        private CenterLookup _center;
        private readonly BindingList<GridRow> _rows = new BindingList<GridRow> { AllowNew = true };

        public TransferEntryForm(TransferEntryRepository entryRepo, CenterRepository centerRepo,
            DciRepository dciRepo, int accountId)
        {
            _entryRepo = entryRepo ?? throw new ArgumentNullException(nameof(entryRepo));
            _centerRepo = centerRepo ?? throw new ArgumentNullException(nameof(centerRepo));
            _dciRepo = dciRepo ?? throw new ArgumentNullException(nameof(dciRepo));
            _accountId = accountId;

            Text = "EPIPHARM — Transfert Inter-Etablissement";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(960, 640);
            MinimumSize = new Size(820, 540);
            KeyPreview = true;
            BackColor = RetroTheme.MenuBackground;

            _header = new DosHeaderBar { Subtitle = "Transfert Inter-Etablissement" };
            _footer = new DosFooterBar();
            _footer.SetHints(("F5", "Etablissement/DCI"), ("Suppr", "Retirer la Ligne"), ("Esc", "Sortie"));

            var headerFields = new TableLayoutPanel
            {
                Dock = DockStyle.Top, Height = 64, BackColor = RetroTheme.MenuBackground,
                ColumnCount = 6, RowCount = 2, Padding = new Padding(16, 8, 16, 8)
            };
            headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18F));
            headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18F));
            headerFields.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            headerFields.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            headerFields.Controls.Add(FieldLabel("Code Service :"), 0, 0);
            var centerRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, BackColor = RetroTheme.MenuBackground, ColumnCount = 2, RowCount = 1
            };
            centerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            centerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _centerCodeBox = new TextBox { Dock = DockStyle.Fill, Font = RetroTheme.DosFont(11F) };
            _centerCodeBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.F5) { PickCenter(); e.Handled = true; } };
            _centerCodeBox.Leave += (s, e) => ResolveTypedCenter();
            _centerNameLabel = new Label
            {
                Dock = DockStyle.Fill, ForeColor = RetroTheme.FieldLookupValue, Font = RetroTheme.DosFont(11F),
                TextAlign = ContentAlignment.MiddleLeft
            };
            centerRow.Controls.Add(_centerCodeBox, 0, 0);
            centerRow.Controls.Add(_centerNameLabel, 1, 0);
            headerFields.Controls.Add(centerRow, 1, 0);

            headerFields.Controls.Add(FieldLabel("Date Operation :"), 2, 0);
            _operationDatePicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom, CustomFormat = RetroTheme.DosDateFormat,
                Dock = DockStyle.Fill, Font = RetroTheme.DosFont(10F)
            };
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

            _grid = BuildGrid();

            var errorBar = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom, Height = 30, BackColor = RetroTheme.MenuBackground,
                ColumnCount = 1, RowCount = 1, Padding = new Padding(16, 4, 16, 4)
            };
            _errorLabel = new Label
            {
                Dock = DockStyle.Fill, ForeColor = Dos16.LightRed, Font = RetroTheme.DosFont(10F, FontStyle.Bold)
            };
            errorBar.Controls.Add(_errorLabel, 0, 0);

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
            public string Observation { get; set; } = string.Empty;

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
            { Name = "libelle", HeaderText = "Libelle DCI/Forme", DataPropertyName = "DciLabel", FillWeight = 46, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "qdem", HeaderText = "Qte Dem", DataPropertyName = "QuantityText", FillWeight = 16,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "obs", HeaderText = "Obs.", DataPropertyName = "Observation", FillWeight = 24 });

            grid.CellEndEdit += Grid_CellEndEdit;
            grid.KeyDown += Grid_KeyDown;
            grid.DataError += (s, e) => { e.ThrowException = false; };
            return grid;
        }

        private void Grid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (_grid.Columns[e.ColumnIndex].Name == "dci")
            {
                ResolveTypedDci(e.RowIndex);
            }
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
            _centerCodeBox.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { AttemptCloseWithConfirm(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void PickCenter()
        {
            using (var picker = new PickerForm<CenterLookup>(
                "Liste des Etablissements", "Rechercher (nom ou code) :",
                q => _centerRepo.Search(q), c => c.Label))
            {
                if (picker.ShowDialog(this) == DialogResult.OK)
                {
                    SetCenter(picker.Selected);
                }
            }
        }

        private void ResolveTypedCenter()
        {
            var typed = _centerCodeBox.Text.Trim();
            if (string.IsNullOrEmpty(typed))
            {
                _center = null;
                _centerNameLabel.Text = string.Empty;
                return;
            }
            var found = _centerRepo.FindByCode(typed);
            if (found == null)
            {
                _center = null;
                _centerNameLabel.Text = string.Empty;
                _errorLabel.Text = "Etablissement Inexistant";
            }
            else
            {
                SetCenter(found);
            }
        }

        private void SetCenter(CenterLookup center)
        {
            _center = center;
            _centerCodeBox.Text = center.LegacyCode;
            _centerNameLabel.Text = center.Name;
            _errorLabel.Text = string.Empty;
        }

        private void AttemptCloseWithConfirm()
        {
            var hasContent = _rows.Any(r => !r.IsBlank) || _center != null ||
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
            var lines = new List<TransferEntryLine>();

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

                lines.Add(new TransferEntryLine
                {
                    DciId = row.DciId.Value,
                    DciCode = row.DciCode,
                    DciLabel = row.DciLabel,
                    Quantity = qty,
                    Observation = row.Observation
                });
            }

            try
            {
                var deliveryId = _entryRepo.Save(_center, _documentNoBox.Text, _operationDatePicker.Value.Date,
                    lines, _accountId);
                _errorLabel.ForeColor = RetroTheme.FieldValue;
                _errorLabel.Text = $"Transfert enregistre (No {deliveryId}).";
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
