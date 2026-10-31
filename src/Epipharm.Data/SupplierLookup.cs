namespace Epipharm.Data
{
    /// <summary>One row of a supplier picker (F5 lookup on "Code
    /// Fournisseur" — see Reception de Produits in UI_SPEC.md).</summary>
    public class SupplierLookup
    {
        public int Id { get; set; }
        public string LegacyCode { get; set; }
        public string Sigle { get; set; }
        public string Name { get; set; }

        // Plain hyphen, not an em-dash: this renders through the bundled
        // Px437 DOS font (RetroTheme.DosFont), whose CP437 glyph table has
        // no em-dash — it drew as a "ø"-looking character, caught by
        // actually looking at a screenshot of the supplier picker, not by
        // reading the code.
        public string Label => string.IsNullOrEmpty(Sigle) ? Name : $"{Sigle} - {Name}";
    }
}
