namespace Epipharm.Data
{
    /// <summary>One row of an establishment picker (F5 lookup on "Code
    /// Service" — Transfert Inter-Etablissement really means establishment
    /// here, not a hospital ward/service; see UI_SPEC.md/MENU_MAP.md and
    /// CenterRepository).</summary>
    public class CenterLookup
    {
        public int Id { get; set; }
        public string LegacyCode { get; set; }
        public string Name { get; set; }

        public string Label => $"{LegacyCode}  {Name}";
    }
}
