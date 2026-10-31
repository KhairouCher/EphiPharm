using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Epipharm.App.Theme
{
    /// <summary>
    /// The function-key hint bar at the bottom of every legacy screen:
    /// black background, key names in light red (e.g. "&lt;F1&gt;"), their
    /// description in cyan, "&lt;Esc&gt; Sortie" always present at the end.
    /// </summary>
    public class DosFooterBar : UserControl
    {
        private readonly FlowLayoutPanel _flow;

        public DosFooterBar()
        {
            Dock = DockStyle.Bottom;
            Height = 36;
            BackColor = RetroTheme.HeaderBackground;

            _flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = RetroTheme.HeaderBackground,
                Padding = new Padding(12, 6, 0, 0)
            };
            Controls.Add(_flow);
        }

        /// <summary>
        /// Replaces every hint with the given (key, description) pairs, e.g.
        /// SetHints(("F1","Help"), ("F2","Utilisation"), ("Esc","Sortie")).
        /// </summary>
        public void SetHints(params (string Key, string Description)[] hints)
        {
            _flow.Controls.Clear();
            foreach (var hint in hints)
            {
                _flow.Controls.Add(new Label
                {
                    AutoSize = true,
                    BackColor = RetroTheme.HeaderBackground,
                    ForeColor = RetroTheme.FooterKey,
                    Font = RetroTheme.DosFont(11F, FontStyle.Bold),
                    Text = $"<{hint.Key}>",
                    Margin = new Padding(0, 0, 4, 0)
                });
                _flow.Controls.Add(new Label
                {
                    AutoSize = true,
                    BackColor = RetroTheme.HeaderBackground,
                    ForeColor = RetroTheme.FooterText,
                    Font = RetroTheme.DosFont(11F),
                    Text = hint.Description,
                    Margin = new Padding(0, 0, 24, 0)
                });
            }
        }

        public static readonly List<(string, string)> DefaultMainMenuHints = new List<(string, string)>
        {
            ("F1", "Help"), ("F2", "Utilisation"), ("F3", "Services"), ("F4", "Maintenance"), ("Esc", "Sortie")
        };
    }
}
