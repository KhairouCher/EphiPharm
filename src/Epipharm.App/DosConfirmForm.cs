using System;
using System.Drawing;
using System.Windows.Forms;
using Epipharm.App.Theme;

namespace Epipharm.App
{
    /// <summary>
    /// The legacy app's own confirmation convention before a destructive or
    /// committing action — "Confirmation (O/N/Esc)?" — confirmed from the
    /// real app's strings (see MENU_MAP.md "UI conventions"). Used instead
    /// of a WinForms MessageBox (can't be retro-themed) or an invented
    /// "Enregistrer" button (the real screens don't show one): entry
    /// screens ask this on Esc when there's something to save, not via a
    /// button that was never there. Keyboard-only — O confirms, N or Esc
    /// cancels — matching how the legacy app itself worked with no mouse.
    /// </summary>
    public class DosConfirmForm : Form
    {
        public bool Confirmed { get; private set; }

        public DosConfirmForm(string message)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = RetroTheme.BoxBorder;
            Padding = new Padding(2);
            ClientSize = new Size(Math.Max(360, message.Length * 10 + 40), 70);
            KeyPreview = true;

            var inner = new Panel { Dock = DockStyle.Fill, BackColor = RetroTheme.FieldActiveBack };
            var label = new Label
            {
                Text = $"{message} (O/N)",
                Dock = DockStyle.Fill,
                ForeColor = RetroTheme.FieldActiveText,
                Font = RetroTheme.DosFont(12F),
                TextAlign = ContentAlignment.MiddleCenter
            };
            inner.Controls.Add(label);
            Controls.Add(inner);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.O)
            {
                Confirmed = true;
                Close();
                return true;
            }
            if (keyData == Keys.N || keyData == Keys.Escape)
            {
                Confirmed = false;
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>Shows the prompt and returns the answer — false (no
        /// confirmation needed/given) if there's nothing to confirm.</summary>
        public static bool Ask(IWin32Window owner, string message)
        {
            using (var form = new DosConfirmForm(message))
            {
                form.ShowDialog(owner);
                return form.Confirmed;
            }
        }
    }
}
