using System.Drawing;
using System.Windows.Forms;

namespace Epipharm.App.Theme
{
    /// <summary>
    /// A ListBox styled like the legacy "Menu Général"/"Menu Options" boxes:
    /// blue background, white item text, the selected line a solid red bar
    /// with white text. Items whose MenuOption.Confirmed is false (a
    /// category this session never actually saw real items for) are drawn
    /// dim instead of white, so a placeholder never looks like confirmed
    /// data.
    /// </summary>
    public class DosMenuList : ListBox
    {
        public DosMenuList()
        {
            BackColor = RetroTheme.MenuBackground;
            ForeColor = RetroTheme.MenuItemText;
            Font = RetroTheme.DosFont(12F);
            BorderStyle = BorderStyle.None;
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = 24;
            IntegralHeight = false;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0)
            {
                return;
            }

            var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var back = selected ? RetroTheme.MenuItemSelectedBack : RetroTheme.MenuBackground;
            var fore = selected
                ? RetroTheme.MenuItemSelectedText
                : (Items[e.Index] is MenuOption opt && !opt.Confirmed ? Dos16.DarkGray : RetroTheme.MenuItemText);

            using (var backBrush = new SolidBrush(back))
            using (var foreBrush = new SolidBrush(fore))
            {
                e.Graphics.FillRectangle(backBrush, e.Bounds);
                var text = Items[e.Index] is MenuOption option ? option.Name : Items[e.Index]?.ToString();
                e.Graphics.DrawString(text, e.Font ?? Font, foreBrush, e.Bounds.X + 8, e.Bounds.Y + 3);
            }
        }
    }
}
