using System;

namespace Epipharm.Data
{
    /// <summary>One row of the stock screen: a product+lot joined with its drug name.</summary>
    public class StockLotView
    {
        public int Id { get; set; }
        public string ProductName { get; set; }
        public string DciName { get; set; }
        public string FormName { get; set; }
        public string LotNumber { get; set; }
        public DateTime? ExpiresOn { get; set; }
        public int Quantity { get; set; }
        public int? UnitPriceCentimes { get; set; }

        public decimal? UnitPrice => UnitPriceCentimes.HasValue ? UnitPriceCentimes.Value / 100m : null;

        public int DaysUntilExpiry => ExpiresOn.HasValue
            ? (int)(ExpiresOn.Value.Date - DateTime.Today).TotalDays
            : int.MaxValue;

        public bool IsExpired => ExpiresOn.HasValue && DaysUntilExpiry < 0;

        public bool IsExpiringSoon(int warningDays) =>
            ExpiresOn.HasValue && !IsExpired && DaysUntilExpiry <= warningDays;
    }
}
