using System;

namespace Epipharm.Data
{
    /// <summary>One line of a receipt being entered — legacy "Reception de
    /// Produits" grid (NLig / C.Produit / D.Perem / Numero Lot / D.Fabri /
    /// Qte Mut / Prix Uni, see UI_SPEC.md).</summary>
    public class ReceiptEntryLine
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; }
        public string ProductLabel { get; set; }
        public string LotNumber { get; set; }
        public DateTime? ExpiresOn { get; set; }
        public DateTime? ManufacturedOn { get; set; }
        public int Quantity { get; set; }
        public int UnitPriceCentimes { get; set; }

        public decimal LineTotal => Quantity * (UnitPriceCentimes / 100m);
    }
}
