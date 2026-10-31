using System;
using System.Drawing;
using System.Windows.Forms;
using Epipharm.Data;

namespace Epipharm.App
{
    /// <summary>
    /// Main menu stub. Modules below come straight from MENU_MAP.md (recovered
    /// from the legacy PHARMA.EXE strings) and are placeholders — each one
    /// should become its own Form as that module gets built. Not wiring up
    /// real screens yet on purpose: ships the login/account path end-to-end
    /// first, builds confidence that is solid, then layers screens on top.
    /// </summary>
    public class MainForm : Form
    {
        private static readonly string[] Modules =
        {
            "Stock",
            "Livraisons (vers les services)",
            "Receptions (des fournisseurs)",
            "Commandes",
            "Dotations",
            "Nomenclature (DCI, produits, formes...)",
            "Etats et listings",
            "Archivage annuel",
        };

        public MainForm(Account account)
        {
            Text = "EPIPHARM";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(560, 520);
            Font = new Font("Segoe UI", 12F);
            MinimumSize = new Size(560, 520);

            var header = new Label
            {
                Text = $"Connecte : {account.Username} ({(account.IsAdmin ? "administrateur" : "utilisateur")})",
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(16, 0, 0, 0)
            };

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(16)
            };

            foreach (var module in Modules)
            {
                var button = new Button
                {
                    Text = module,
                    Size = new Size(500, 56),
                    Font = new Font("Segoe UI", 13F),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(16, 0, 0, 0),
                    Margin = new Padding(0, 0, 0, 8)
                };
                button.Click += (s, e) => MessageBox.Show(
                    this, $"\"{module}\" n'est pas encore construit.",
                    "A venir", MessageBoxButtons.OK, MessageBoxIcon.Information);
                flow.Controls.Add(button);
            }

            if (account.IsAdmin)
            {
                var adminButton = new Button
                {
                    Text = "Administration (comptes, parametres)",
                    Size = new Size(500, 56),
                    Font = new Font("Segoe UI", 13F),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(16, 0, 0, 0),
                    Margin = new Padding(0, 0, 0, 8)
                };
                adminButton.Click += (s, e) => MessageBox.Show(
                    this, "L'administration n'est pas encore construite.",
                    "A venir", MessageBoxButtons.OK, MessageBoxIcon.Information);
                flow.Controls.Add(adminButton);
            }

            Controls.Add(flow);
            Controls.Add(header);
        }
    }
}
