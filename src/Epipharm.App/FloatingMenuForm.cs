using System;
using System.Drawing;
using System.Windows.Forms;
using Epipharm.App.Theme;

namespace Epipharm.App
{
    /// <summary>
    /// The small floating 3rd-level box seen in UI_SPEC.md overlapping the
    /// "Menu Options" panel (e.g. Livraison sur Bon de Commande's
    /// Medicaments Usuels / Psychotropes / Autres Medicaments). A separate
    /// borderless top-most window positioned over the parent, rather than a
    /// custom-painted overlay inside the same form — simpler to get right
    /// without a WinForms designer or compiler in this session to check it
    /// against.
    /// </summary>
    public class FloatingMenuForm : Form
    {
        public FloatingMenuForm(System.Collections.Generic.List<MenuOption> options, Point screenLocation)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Location = screenLocation;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = RetroTheme.BoxBorder;
            Padding = new Padding(2);
            Width = 260;
            Height = Math.Min(28 * options.Count + 4, 300);

            var list = new DosMenuList { Dock = DockStyle.Fill };
            list.Items.AddRange(options.ToArray());
            list.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && list.SelectedItem is MenuOption chosen)
                {
                    Close();
                    chosen.OnChosen?.Invoke();
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    Close();
                }
            };
            list.DoubleClick += (s, e) =>
            {
                if (list.SelectedItem is MenuOption chosen)
                {
                    Close();
                    chosen.OnChosen?.Invoke();
                }
            };
            Controls.Add(list);
            Deactivate += (s, e) => Close();

            Shown += (s, e) => list.Focus();
        }
    }
}
