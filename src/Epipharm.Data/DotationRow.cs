namespace Epipharm.Data
{
    /// <summary>One row of "Mise a Jour des Dotations en Medicaments
    /// Usuels" (MENU_MAP.md section 5) — one row per active Medicaments
    /// Usuels DCI, with that service/week's current dotation (0 if none
    /// set yet).</summary>
    public class DotationRow
    {
        public int DciId { get; set; }
        public string DciCode { get; set; }
        public string Label { get; set; }
        public int Dotation { get; set; }
    }
}
