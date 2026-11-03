namespace Epipharm.Data
{
    /// <summary>One row of a simple code/name reference list (Classes
    /// Therapeutiques, Pays, Wilayas, etc. — MENU_MAP.md section 6/
    /// "DONNEES NATIONALES"). Extra is an optional third column (e.g. a
    /// parent record's name), blank when a table has nothing to show
    /// there.</summary>
    public class ReferenceRow
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Extra { get; set; }
    }
}
