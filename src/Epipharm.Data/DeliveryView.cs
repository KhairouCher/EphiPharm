using System;

namespace Epipharm.Data
{
    /// <summary>
    /// One row of the deliveries browse screen (legacy LIVRAISO.DBF).
    /// ServiceName is left null for now — deliveries.service_id is
    /// unresolved by the migration (see db/schema.sql's note on
    /// legacy_service_raw); LegacyServiceRaw is shown instead so nothing
    /// here claims a link that hasn't actually been confirmed.
    /// </summary>
    public class DeliveryView
    {
        public int Id { get; set; }
        public int OperationNo { get; set; }
        public int SlipNo { get; set; }
        public DateTime DeliveredOn { get; set; }
        public string LegacyServiceRaw { get; set; }
        public string ServiceName { get; set; }

        /// <summary>
        /// long, not int — see ReceiptView.AmountCentimes for why. The real
        /// deliveries data tops out at ~11.97 million DZD today (within
        /// Int32's ~21.5 million DZD range), but receipts' equivalent field
        /// already proved this pattern overflows, so this is fixed the
        /// same way rather than waiting for deliveries data to grow into it.
        /// </summary>
        public long AmountCentimes { get; set; }
        public string DocumentNo { get; set; }
        public string MovementType { get; set; }

        public decimal Amount => AmountCentimes / 100m;
    }
}
