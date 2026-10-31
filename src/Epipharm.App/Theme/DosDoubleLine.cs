using System.Drawing;
using System.Windows.Forms;

namespace Epipharm.App.Theme
{
    /// <summary>
    /// Approximates a row of the IBM-437 double horizontal line character
    /// (═) that separates every header/footer band in the legacy screens —
    /// drawn as two thin lines rather than repeating the actual character,
    /// so it stays a clean line at any window width instead of needing an
    /// exact character count.
    /// </summary>
    public class DosDoubleLine : Control
    {
        public DosDoubleLine()
        {
            Dock = DockStyle.Top;
            Height = 5;
            BackColor = Color.Black;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (var pen = new Pen(RetroTheme.HeaderDivider))
            {
                e.Graphics.DrawLine(pen, 0, 1, Width, 1);
                e.Graphics.DrawLine(pen, 0, 3, Width, 3);
            }
        }
    }
}
