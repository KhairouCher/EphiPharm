using System;

namespace Epipharm.Data
{
    /// <summary>
    /// One DCI's reference info — "Informations D.C.I." (MENU_MAP.md
    /// section 6). Only fields with a confirmed backing column are here:
    /// the real screenshot of this screen also shows Tableau, Stockage,
    /// Production, Affectation, N° Décision, Saison, Appartenance,
    /// "Retiré" and "Date de Retrait", but DCI has no raw column for any
    /// of those (checked — importer/import_dbf.py's dci columns are
    /// c_t/n_o/s_t/forme/libelle/tableau/type_med/priorite/qte_stock/
    /// stock_mini/stock_secu/point_comm/prix_def/qte_comman/date_comma/
    /// delai_appr/nj_rupture/d_rupture). d_rupture looks like it could be
    /// "Date de Retrait" at a glance, but it isn't: it's a projected/last
    /// stock-rupture date (see db/schema.sql's dci.projected_rupture_on),
    /// unrelated to being pulled from the catalog — a different, earlier
    /// version of this code conflated the two, which also silently hid
    /// 68% of medicament DCI from the dotation screen (see
    /// DotationEntryRepository). Showing invented values for the
    /// unconfirmed fields would be guessing at a system that tracks real
    /// medication; DciInfoForm shows their labels (matching the
    /// screenshot) with blank values instead, which is honest and also
    /// happens to match how most real DCI records show on that screen
    /// anyway.
    /// </summary>
    public class DciLookup
    {
        public int Id { get; set; }
        public string LegacyCode { get; set; }
        public string SubclassFlag { get; set; }
        public string ClassName { get; set; }
        public string SubclassName { get; set; }
        public string Name { get; set; }
        public string FormCode { get; set; }
        public string FormSigle { get; set; }
        public string FormDosage { get; set; }
        public string DrugCategory { get; set; }
        public bool TableauB { get; set; }

        /// <summary>"CC.NNN", matching the real screenshot's "Code D.C.I"
        /// display exactly (e.g. "01.007").</summary>
        public string FormattedCode => LegacyCode != null && LegacyCode.Length == 5
            ? $"{LegacyCode.Substring(0, 2)}.{LegacyCode.Substring(2)}"
            : LegacyCode;

        public string FormeDosageDisplay => string.Join(" ",
            new[] { FormCode, FormSigle, FormDosage } );

        /// <summary>Legacy TYPE_MED single-letter code resolved to the
        /// label used throughout this rebuild (db/schema.sql's
        /// category_map) — M/P/S/blank -&gt; Medicament Usuel/Psychotrope/
        /// Stupefiant/Autre.</summary>
        public string DrugCategoryDisplay
        {
            get
            {
                switch (DrugCategory)
                {
                    case "medicament": return "M  Medicament Usuel";
                    case "psychotrope": return "P  Psychotrope";
                    case "stupefiant": return "S  Stupefiant";
                    default: return "   Autre";
                }
            }
        }

    }
}
