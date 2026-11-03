using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.Windows.Forms;
using Epipharm.Data;
using Epipharm.App.Theme;

namespace Epipharm.App
{
    /// <summary>
    /// The main menu, rebuilt as a two-panel cascading menu to match the
    /// legacy app's actual structure (UI_SPEC.md), not just its colors —
    /// "Menu Général" categories on the left drive "Menu Options" on the
    /// right, with a 3rd-level floating popup on items that have one.
    /// </summary>
    public class MainForm : Form
    {
        private readonly Account _account;
        private readonly string _dbPath;
        private readonly DosHeaderBar _header;
        private readonly DosFooterBar _footer;
        private readonly DosMenuList _categoryList;
        private readonly DosMenuList _optionsList;
        private readonly List<MenuCategory> _categories;

        public MainForm(Account account, string dbPath)
        {
            _account = account;
            _dbPath = dbPath;

            Text = "EPIPHARM";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1000, 620);
            MinimumSize = new Size(800, 500);
            KeyPreview = true;

            _header = new DosHeaderBar();
            SetEstablishmentFromSettings();

            _footer = new DosFooterBar();
            _footer.SetHints(("F1", "Help"), ("F2", "Utilisation"), ("F3", "Services"),
                ("F4", "Maintenance"), ("Esc", "Sortie"));

            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = RetroTheme.MenuBackground,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(20)
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));

            var categoryPane = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = RetroTheme.MenuBackground };
            categoryPane.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            categoryPane.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            categoryPane.Controls.Add(NewPaneLabel("Menu Général"), 0, 0);
            var categoryBorder = new Panel { Dock = DockStyle.Fill, BackColor = RetroTheme.BoxBorder, Padding = new Padding(2) };
            _categoryList = new DosMenuList { Dock = DockStyle.Fill, TabIndex = 0 };
            categoryBorder.Controls.Add(_categoryList);
            categoryPane.Controls.Add(categoryBorder, 0, 1);

            var optionsPane = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = RetroTheme.MenuBackground };
            optionsPane.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            optionsPane.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            optionsPane.Controls.Add(NewPaneLabel("Menu Options"), 0, 0);
            var optionsBorder = new Panel { Dock = DockStyle.Fill, BackColor = RetroTheme.BoxBorder, Padding = new Padding(2) };
            _optionsList = new DosMenuList { Dock = DockStyle.Fill, TabIndex = 1 };
            optionsBorder.Controls.Add(_optionsList);
            optionsPane.Controls.Add(optionsBorder, 0, 1);

            body.Controls.Add(categoryPane, 0, 0);
            body.Controls.Add(optionsPane, 1, 0);

            // Fill added first, then Top/Bottom — see Epipharm.App's earlier
            // forms for why this order matters for docking.
            Controls.Add(body);
            Controls.Add(_footer);
            Controls.Add(_header);

            _categories = MenuData.BuildCategories(OpenStock, OpenReceipts, OpenReceiptEntry, OpenDeliveries,
                OpenDeliveryEntry, OpenDotationEntry, OpenDciInfo, OpenTransferEntry, OpenReferenceList,
                OpenCommandeEntry, OpenCommandesList, OpenExpiredStockList,
                ShowNotBuilt);
            if (account.IsAdmin)
            {
                _categories.Add(new MenuCategory("ADMINISTRATION", new List<MenuOption>
                {
                    new MenuOption("Comptes Utilisateurs", OpenAccountsAdmin),
                    new MenuOption("Parametres d'Exploitation", OpenSettings),
                }));
            }

            _categoryList.Items.Clear();
            foreach (var category in _categories)
            {
                _categoryList.Items.Add(category);
            }
            // No DisplayMember here: DosMenuList is OwnerDrawFixed and draws
            // its own text from IMenuRow.Name (see DosMenuList.OnDrawItem),
            // so DisplayMember would be silently ignored — setting it here
            // previously looked like it worked but never did anything; the
            // category list actually showed each item's raw ToString()
            // ("Epipharm.App.Theme.MenuCategory") until IMenuRow was added.
            _categoryList.SelectedIndexChanged += (s, e) => RefreshOptions();
            _optionsList.KeyDown += OptionsList_KeyDown;
            _optionsList.DoubleClick += (s, e) => ChooseSelectedOption();

            if (_categoryList.Items.Count > 0)
            {
                _categoryList.SelectedIndex = 0;
            }
        }

        private Label NewPaneLabel(string text) => new Label
        {
            Text = text,
            Dock = DockStyle.Top,
            Height = 28,
            BackColor = RetroTheme.MenuBackground,
            ForeColor = RetroTheme.MenuLabel,
            Font = RetroTheme.DosFont(13F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void RefreshOptions()
        {
            _optionsList.Items.Clear();
            if (_categoryList.SelectedItem is MenuCategory category)
            {
                foreach (var option in category.Options)
                {
                    _optionsList.Items.Add(option);
                }
                if (_optionsList.Items.Count > 0)
                {
                    _optionsList.SelectedIndex = 0;
                }
            }
        }

        private void OptionsList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                ChooseSelectedOption();
            }
        }

        private void ChooseSelectedOption()
        {
            if (!(_optionsList.SelectedItem is MenuOption option))
            {
                return;
            }

            if (option.SubOptions.Count > 0)
            {
                var screenPoint = _optionsList.PointToScreen(new Point(20, _optionsList.ItemHeight * _optionsList.SelectedIndex));
                using (var popup = new FloatingMenuForm(option.SubOptions, screenPoint))
                {
                    popup.ShowDialog(this);
                }
                return;
            }

            option.OnChosen?.Invoke();
        }

        private void OpenStock()
        {
            var repo = new StockRepository(_dbPath);
            using (var form = new StockListForm(repo))
            {
                form.ShowDialog(this);
            }
        }

        private void OpenReceipts()
        {
            var repo = new ReceiptRepository(_dbPath);
            using (var form = new ReceiptsListForm(repo))
            {
                form.ShowDialog(this);
            }
        }

        private void OpenReceiptEntry()
        {
            var entryRepo = new ReceiptEntryRepository(_dbPath);
            var supplierRepo = new SupplierRepository(_dbPath);
            var productRepo = new ProductRepository(_dbPath);
            using (var form = new ReceiptEntryForm(entryRepo, supplierRepo, productRepo, _account.Id))
            {
                form.ShowDialog(this);
            }
        }

        // "(Medicaments)" is the only one of the three confirmed from a
        // real screenshot; Psychotropes/Autres Medicaments follow the same
        // pattern using the 3rd-level popup's own confirmed names — see
        // DeliveryEntryForm's constructor doc comment.
        private static readonly Dictionary<string, string> DeliverySubtitleByCategory = new Dictionary<string, string>
        {
            ["medicament"] = "Livraison sur Bon de Commande (Medicaments)",
            ["psychotrope"] = "Livraison sur Bon de Commande (Psychotropes)",
            ["autre"] = "Livraison sur Bon de Commande (Autres Medicaments)",
        };

        private void OpenDeliveryEntry(string drugCategory)
        {
            var entryRepo = new DeliveryEntryRepository(_dbPath, drugCategory);
            var serviceRepo = new ServiceRepository(_dbPath);
            using (var form = new DeliveryEntryForm(entryRepo, serviceRepo, _account.Id,
                DeliverySubtitleByCategory[drugCategory]))
            {
                form.ShowDialog(this);
            }
        }

        private void OpenTransferEntry()
        {
            var entryRepo = new TransferEntryRepository(_dbPath);
            var centerRepo = new CenterRepository(_dbPath);
            var dciRepo = new DciRepository(_dbPath);
            using (var form = new TransferEntryForm(entryRepo, centerRepo, dciRepo, _account.Id))
            {
                form.ShowDialog(this);
            }
        }

        /// <summary>Opens one of the 8 simple DONNEES NATIONALES reference
        /// lists — see ReferenceRepository's doc comment for why these use
        /// a generic screen rather than one bespoke layout per table.</summary>
        private void OpenReferenceList(string key)
        {
            var repo = new ReferenceRepository(_dbPath);
            ReferenceListForm form;
            switch (key)
            {
                case "classes":
                    form = new ReferenceListForm("Classes Therapeutiques", "Classes Therapeutiques",
                        "Code", "Libelle", null, q => repo.SearchClasses(q));
                    break;
                case "subclasses":
                    form = new ReferenceListForm("S/Classes Therapeutiques", "S/Classes Therapeutiques",
                        "Code", "Libelle", "Classe", q => repo.SearchSubclasses(q));
                    break;
                case "centers":
                    form = new ReferenceListForm("Fichier des Etablissements", "Fichier des Etablissements",
                        "Code", "Libelle", "Region", q => repo.SearchCenters(q));
                    break;
                case "services":
                    form = new ReferenceListForm("Table des Services", "Table des Services",
                        "Code", "Libelle", "Etablissement", q => repo.SearchServices(q));
                    break;
                case "countries":
                    form = new ReferenceListForm("Table des Pays", "Table des Pays",
                        "Code", "Libelle", "Sigle", q => repo.SearchCountries(q));
                    break;
                case "wilayas":
                    form = new ReferenceListForm("Table des Wilayate", "Table des Wilayate",
                        "Code", "Libelle", null, q => repo.SearchWilayas(q));
                    break;
                case "geocodes":
                    form = new ReferenceListForm("Table des Codes Geographiques", "Table des Codes Geographiques",
                        "Code", "Libelle", "Wilaya", q => repo.SearchGeoCodes(q));
                    break;
                case "sectors":
                    form = new ReferenceListForm("Table des Secteurs Sanitaires", "Table des Secteurs Sanitaires",
                        "Code", "Libelle", null, q => repo.SearchSanitarySectors(q));
                    break;
                default:
                    return;
            }
            using (form)
            {
                form.ShowDialog(this);
            }
        }

        private void OpenDciInfo()
        {
            var dciRepo = new DciRepository(_dbPath);
            using (var form = new DciInfoForm(dciRepo))
            {
                form.ShowDialog(this);
            }
        }

        // Each wording below is MENU_MAP.md's own exact (and irregular —
        // "en Medicaments"/"en Psychotropes" but "Autres Medicaments"/
        // "Dotations Stupefiants" with no "en"/"des") confirmed leaf text,
        // not a template with the category substituted in.
        private static readonly Dictionary<string, (string Title, string Subtitle)> DotationTextByCategory =
            new Dictionary<string, (string, string)>
        {
            ["medicament"] = ("Dotations Medicaments Usuels", "Mise a Jour des Dotations en Medicaments Usuels"),
            ["psychotrope"] = ("Dotations Psychotropes", "Mise a Jour des Dotations en Psychotropes"),
            ["autre"] = ("Dotations Autres Medicaments", "Mise a Jour des Dotations Autres Medicaments"),
            ["stupefiant"] = ("Dotations Stupefiants", "Mise a Jour Dotations Stupefiants"),
        };

        private void OpenDotationEntry(string drugCategory)
        {
            var entryRepo = new DotationEntryRepository(_dbPath, drugCategory);
            var serviceRepo = new ServiceRepository(_dbPath);
            var (title, subtitle) = DotationTextByCategory[drugCategory];
            using (var form = new DotationEntryForm(entryRepo, serviceRepo, _account.Id, title, subtitle))
            {
                form.ShowDialog(this);
            }
        }

        private void OpenDeliveries()
        {
            var repo = new DeliveryRepository(_dbPath);
            using (var form = new DeliveriesListForm(repo))
            {
                form.ShowDialog(this);
            }
        }

        private void OpenExpiredStockList()
        {
            var repo = new StockRepository(_dbPath);
            using (var form = new ExpiredStockListForm(repo))
            {
                form.ShowDialog(this);
            }
        }

        private void OpenCommandeEntry()
        {
            var entryRepo = new CommandeRepository(_dbPath);
            var supplierRepo = new SupplierRepository(_dbPath);
            var dciRepo = new DciRepository(_dbPath);
            using (var form = new CommandeEntryForm(entryRepo, supplierRepo, dciRepo, _account.Id))
            {
                form.ShowDialog(this);
            }
        }

        private void OpenCommandesList()
        {
            var repo = new CommandeRepository(_dbPath);
            using (var form = new CommandesListForm(repo, _account.Id))
            {
                form.ShowDialog(this);
            }
        }

        private void OpenAccountsAdmin()
        {
            var repo = new AccountRepository(_dbPath);
            using (var form = new AccountsAdminForm(repo))
            {
                form.ShowDialog(this);
            }
        }

        private void OpenSettings()
        {
            var repo = new SettingsRepository(_dbPath);
            using (var form = new SettingsForm(repo, _account.Id))
            {
                form.ShowDialog(this);
            }
        }

        private void ShowNotBuilt(string name)
        {
            MessageBox.Show(this, $"\"{name}\" n'est pas encore construit.",
                "A venir", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SetEstablishmentFromSettings()
        {
            var code = string.Empty;
            var name = string.Empty;
            try
            {
                using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT facility_code, facility_name FROM settings WHERE id = 1;";
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                code = reader["facility_code"] as string ?? string.Empty;
                                name = reader["facility_name"] as string ?? string.Empty;
                            }
                        }
                    }
                }
            }
            catch
            {
                // No settings row yet (e.g. schema-only database) — leave blank
                // rather than fail the main menu over a header line.
            }
            _header.SetEstablishment(code, name);
        }
    }
}
