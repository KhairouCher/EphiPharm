namespace Epipharm.Data
{
    /// <summary>The single PARPHARM settings row — "Parametres
    /// d'Exploitation" (MENU_MAP.md section 9). Only facility_code/
    /// facility_name/authority_name/current_year/expiry_warning_days are
    /// editable here; the next_*_no counters are shown read-only (used
    /// internally by the entry screens' own numbering — letting an admin
    /// hand-edit them risks a duplicate or a gap, with nothing on this
    /// screen to explain why that matters).</summary>
    public class SettingsInfo
    {
        public string FacilityCode { get; set; }
        public string FacilityName { get; set; }
        public string AuthorityName { get; set; }
        public int CurrentYear { get; set; }
        public int? ExpiryWarningDays { get; set; }

        public string ArchivedThroughDate { get; set; }
        public int NextOperationNo { get; set; }
        public int NextDeliverySlipNo { get; set; }
        public int NextReceiptSlipNo { get; set; }
        public int NextOrderNo { get; set; }
        public int NextCorrectionNo { get; set; }
        public int NextInventoryNo { get; set; }
    }
}
