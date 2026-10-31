namespace Epipharm.Data
{
    /// <summary>One row of a product picker (F5 lookup) — legacy "Liste
    /// Produits/Code" / "Liste Produits/Alpha" (see MENU_MAP.md UI
    /// conventions: lists over typed codes).</summary>
    public class ProductLookup
    {
        public int Id { get; set; }
        public string LegacyCode { get; set; }
        public string Name { get; set; }
        public string DciName { get; set; }
        public string FormName { get; set; }

        public string Label => FormName == null ? Name : $"{Name} ({FormName})";
    }
}
