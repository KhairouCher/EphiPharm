namespace Epipharm.Data
{
    /// <summary>
    /// One row of the "Livraison sur Bon de Commande (Medicaments)" grid —
    /// one row per DCI in the selected service's weekly dotation for the
    /// Medicaments Usuels category, matching the real screenshot's columns
    /// (DCI / Libelle DCI/Forme / Stock / Dota. / Q.Liv / Observ.).
    /// </summary>
    public class DeliveryAllocationRow
    {
        public int DciId { get; set; }
        public string DciCode { get; set; }
        public string Label { get; set; }
        public int StockQty { get; set; }
        public int DotationRemaining { get; set; }
        public int AlreadyDelivered { get; set; }

        /// <summary>DCI.default_price_centimes — not shown on screen (the
        /// real screenshot has no price column here), only used internally
        /// to compute the delivery slip's amount_centimes on save, same as
        /// a real pharmacy invoice would value a delivery at list price.</summary>
        public int? UnitPriceCentimes { get; set; }

        /// <summary>The quantity the user will actually deliver — bound to
        /// the grid's editable column. Pre-filled by
        /// DeliveryEntryRepository.GetAllocationRows to min(DotationRemaining,
        /// StockQty), 0 when out of stock (see IsRupture): the real
        /// screenshot's footer ("Introduire directement la Qte Demandee ou
        /// &lt;Rc&gt; pour la Modifiee") implies the field starts pre-filled
        /// and the operator either accepts or overwrites it, not that it
        /// starts blank.</summary>
        public int QuantityToDeliver { get; set; }

        public bool IsRupture => StockQty <= 0;

        /// <summary>The real screenshot's "Observ." column: just the word
        /// "Rupture" when there's no stock for this DCI at all, blank
        /// otherwise — a separate column from "Q.De[mandee]" (cut off at
        /// the photo's right edge, but named in the footer hint
        /// "Introduire directement la Qte Demandee"), which is the
        /// editable quantity-to-deliver column.</summary>
        public string ObservationDisplay => IsRupture ? "Rupture" : string.Empty;
    }
}
