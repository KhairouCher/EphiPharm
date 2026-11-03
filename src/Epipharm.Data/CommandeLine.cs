namespace Epipharm.Data
{
    /// <summary>One line of a purchase order ("Edition Commande",
    /// MENU_MAP.md section 4) — DCI-level, like Transfert Inter-
    /// Etablissement and the dotation screens, not product-level.
    /// QuantityProposed and QuantityOrdered are saved as the same typed
    /// value: nothing in the real data (COMMANDE.DBF was empty in the
    /// source — DETAIL_C.DBF's 405 rows are all orphaned, no parent order
    /// to check a real example against) shows these ever actually
    /// differing for a manually-entered line, so treating a "propose then
    /// separately adjust" workflow as two separate fields here would be
    /// inventing a distinction with nothing to confirm it.</summary>
    public class CommandeLine
    {
        public int DciId { get; set; }
        public string DciCode { get; set; }
        public string DciLabel { get; set; }
        public int Quantity { get; set; }
    }
}
