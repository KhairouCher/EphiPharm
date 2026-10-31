using System;

namespace Epipharm.Data
{
    /// <summary>
    /// One row of the supplier-receipts browse screen (legacy RECEPT.DBF).
    /// SupplierName is left null for now — receipts.supplier_id is
    /// unresolved by the migration (see db/schema.sql's note on
    /// legacy_tiers_raw); LegacyTiersRaw is shown instead so nothing here
    /// claims a link that hasn't actually been confirmed.
    /// </summary>
    public class ReceiptView
    {
        public int Id { get; set; }
        public int OperationNo { get; set; }
        public int SlipNo { get; set; }
        public DateTime ReceivedOn { get; set; }
        public string LegacyTiersRaw { get; set; }
        public string SupplierName { get; set; }
        public int AmountCentimes { get; set; }
        public string DocumentNo { get; set; }
        public string MovementType { get; set; }

        public decimal Amount => AmountCentimes / 100m;
    }
}
