using System;

namespace Epipharm.Data
{
    /// <summary>
    /// One row of the supplier-receipts browse screen (legacy RECEPT.DBF).
    /// SupplierName is resolved for the TIERS shapes the migration can
    /// decode (confirmed "213"+supplier-code format — see db/schema.sql's
    /// note on legacy_tiers_raw); it's null for the other ~31% of rows
    /// whose TIERS value is a different thing entirely (establishment
    /// transfer codes, etc.), where Tiers falls back to the raw code
    /// instead of showing a name nobody confirmed.
    /// </summary>
    public class ReceiptView
    {
        public int Id { get; set; }
        public int OperationNo { get; set; }
        public int SlipNo { get; set; }
        public DateTime ReceivedOn { get; set; }
        public string LegacyTiersRaw { get; set; }
        public string SupplierName { get; set; }

        /// <summary>
        /// long, not int: a whole receipt slip's total can exceed Int32's
        /// ~21.5 million DZD range — the real data has one at ~48.5 million
        /// DZD (4,856,939,160 centimes). Reading this as Convert.ToInt32
        /// used to crash the whole screen with "Value was either too large
        /// or too small for an Int32" the moment that row was fetched —
        /// caught by actually running this screen against the real data,
        /// not by inspection.
        /// </summary>
        public long AmountCentimes { get; set; }
        public string DocumentNo { get; set; }
        public string MovementType { get; set; }

        public decimal Amount => AmountCentimes / 100m;

        /// <summary>What the Tiers column actually shows: the resolved
        /// supplier name when there is one, the raw legacy code otherwise.</summary>
        public string Tiers => string.IsNullOrEmpty(SupplierName) ? LegacyTiersRaw : SupplierName;
    }
}
