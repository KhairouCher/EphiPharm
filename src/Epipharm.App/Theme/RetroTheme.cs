using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;

namespace Epipharm.App.Theme
{
    /// <summary>
    /// The standard 16-color DOS/VGA text-mode palette, by role. Picked from
    /// the fixed EGA/VGA palette rather than sampled off the phone photos in
    /// UI_SPEC.md — those have glare and perspective distortion, but DOS text
    /// mode could only ever use these 16 colors, so this is the more reliable
    /// source. See UI_SPEC.md for which role maps to which screen element.
    /// </summary>
    public static class Dos16
    {
        public static readonly Color Black = Color.FromArgb(0, 0, 0);
        public static readonly Color Blue = Color.FromArgb(0, 0, 170);
        public static readonly Color Green = Color.FromArgb(0, 170, 0);
        public static readonly Color Cyan = Color.FromArgb(0, 170, 170);
        public static readonly Color Red = Color.FromArgb(170, 0, 0);
        public static readonly Color Magenta = Color.FromArgb(170, 0, 170);
        public static readonly Color Brown = Color.FromArgb(170, 85, 0);
        public static readonly Color LightGray = Color.FromArgb(170, 170, 170);
        public static readonly Color DarkGray = Color.FromArgb(85, 85, 85);
        public static readonly Color LightBlue = Color.FromArgb(85, 85, 255);
        public static readonly Color LightGreen = Color.FromArgb(85, 255, 85);
        public static readonly Color LightCyan = Color.FromArgb(85, 255, 255);
        public static readonly Color LightRed = Color.FromArgb(255, 85, 85);
        public static readonly Color LightMagenta = Color.FromArgb(255, 85, 255);
        public static readonly Color Yellow = Color.FromArgb(255, 255, 85);
        public static readonly Color White = Color.FromArgb(255, 255, 255);
    }

    /// <summary>
    /// Shared colors and font for the retro screens, named by what they're
    /// used for (see UI_SPEC.md) rather than by raw color, so a screen's
    /// code reads as "what this means" not "which of the 16 colors".
    /// </summary>
    public static class RetroTheme
    {
        public static readonly Color HeaderBackground = Dos16.Black;
        public static readonly Color HeaderTitle = Dos16.Yellow;
        public static readonly Color HeaderDivider = Dos16.LightCyan;
        public static readonly Color MenuBackground = Dos16.Blue;
        public static readonly Color MenuLabel = Dos16.LightCyan;
        public static readonly Color MenuItemText = Dos16.White;
        public static readonly Color MenuItemSelectedBack = Dos16.Red;
        public static readonly Color MenuItemSelectedText = Dos16.White;
        public static readonly Color FieldLabel = Dos16.White;
        public static readonly Color FieldValue = Dos16.Yellow;
        public static readonly Color FieldLookupValue = Dos16.LightCyan;
        public static readonly Color FieldActiveBack = Dos16.Red;
        public static readonly Color FieldActiveText = Dos16.White;
        public static readonly Color BoxBorder = Dos16.White;
        public static readonly Color FooterKey = Dos16.LightRed;
        public static readonly Color FooterText = Dos16.LightCyan;
        public static readonly Color BrowseBackground = Dos16.LightGray;
        public static readonly Color BrowseText = Dos16.Blue;
        public static readonly Color BrowseSelectedBack = Dos16.Red;
        public static readonly Color BrowseSelectedText = Dos16.White;
        public static readonly Color SecondaryLabel = Dos16.LightMagenta;

        private static Font _dosFont;
        private static readonly PrivateFontCollection BundledFonts = LoadBundledFonts();

        /// <summary>
        /// Loads Px437 IBM VGA 8x16 — the actual DOS VGA text-mode bitmap
        /// font the legacy app rendered with, not an approximation — from
        /// Resources/Fonts next to the exe (see NOTICE.md there for its
        /// source/license). A PrivateFontCollection is used instead of
        /// installing it, so the app renders correctly on any machine it's
        /// copied to. The collection itself must be kept alive for as long
        /// as fonts from it are used (GDI+ requirement), hence the static
        /// field rather than a local/disposed one.
        /// </summary>
        private static PrivateFontCollection LoadBundledFonts()
        {
            var collection = new PrivateFontCollection();
            try
            {
                var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
                var fontPath = Path.Combine(baseDir, "Resources", "Fonts", "Px437_IBM_VGA_8x16.ttf");
                if (File.Exists(fontPath))
                {
                    collection.AddFontFile(fontPath);
                }
            }
            catch
            {
                // Missing/unreadable font file — DosFont below falls back to
                // an installed or generic monospace font instead of crashing
                // the whole app over a cosmetic detail.
            }
            return collection;
        }

        /// <summary>
        /// A monospace font approximating the original bitmap VGA font.
        /// Prefers the real bundled DOS VGA font (loaded above); falls back
        /// to an installed look-alike if present, then to Consolas/Lucida
        /// Console, which every Windows install has.
        /// </summary>
        public static Font DosFont(float size, FontStyle style = FontStyle.Regular)
        {
            if (BundledFonts.Families.Length > 0)
            {
                try
                {
                    return new Font(BundledFonts.Families[0], size, style);
                }
                catch
                {
                    // Fall through to the installed/generic options below.
                }
            }

            foreach (var name in new[] { "Perfect DOS VGA 437", "Px437 IBM VGA8", "Consolas", "Lucida Console" })
            {
                using (var installed = new InstalledFontCollection())
                {
                    foreach (var family in installed.Families)
                    {
                        if (family.Name == name)
                        {
                            return new Font(name, size, style);
                        }
                    }
                }
            }
            return new Font(FontFamily.GenericMonospace, size, style);
        }

        /// <summary>The one cached font most screens use for body text.</summary>
        public static Font Body => _dosFont ?? (_dosFont = DosFont(12F));
    }
}
