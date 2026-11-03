namespace Epipharm.Data
{
    /// <summary>One row of "Liste des Commandes".</summary>
    public class CommandeRow
    {
        public int Id { get; set; }
        public int OrderNo { get; set; }
        public string OrderedOn { get; set; }
        public string SupplierSigle { get; set; }
        public string DeliveredFlag { get; set; }
        public string CancelledOn { get; set; }

        public string StatusDisplay => !string.IsNullOrEmpty(CancelledOn) ? "Annulee"
            : !string.IsNullOrEmpty(DeliveredFlag) ? "Livree"
            : "Lancee";
    }
}
