namespace Epipharm.Data
{
    /// <summary>One line of "Transfert Inter-Etablissement" — unlike
    /// receipts/deliveries, this screen's grid is keyed by DCI directly
    /// (Libelle DCI/Forme | Qte Dem | Obs.), not by individual product —
    /// confirmed from the real screenshot's columns.</summary>
    public class TransferEntryLine
    {
        public int DciId { get; set; }
        public string DciCode { get; set; }
        public string DciLabel { get; set; }
        public int Quantity { get; set; }
        public string Observation { get; set; }
    }
}
