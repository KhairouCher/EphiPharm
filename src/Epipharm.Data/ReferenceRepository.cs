using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Epipharm.Data
{
    /// <summary>
    /// Read-only browse for the simple reference/nomenclature tables under
    /// DONNEES NATIONALES (MENU_MAP.md section 6) — Classes Therapeutiques,
    /// S/Classes Therapeutiques, Table des Services, Pays, Wilayate, Codes
    /// Geographiques, Secteurs Sanitaires, Fichier des Etablissements. No
    /// screenshot exists for any of these specific screens (the only
    /// DONNEES NATIONALES evidence is the category's own item list and
    /// order, already confirmed and wired in MenuData.cs) — these use the
    /// same pale Browse archetype as StockListForm (UI_SPEC.md archetype
    /// C), which is what every other plain list screen in this rebuild
    /// uses, rather than guessing at a different layout with nothing to
    /// base it on.
    ///
    /// One method per table, not a fully generic query builder: each
    /// table's real schema decides whether there's a meaningful "Extra"
    /// column (e.g. a subclass's parent class), and that's clearer spelled
    /// out per table than hidden behind a table-name parameter.
    /// </summary>
    public class ReferenceRepository
    {
        private readonly string _connectionString;

        public ReferenceRepository(string databasePath)
        {
            _connectionString = $"Data Source={databasePath};Version=3;Foreign Keys=True;";
        }

        private SQLiteConnection Open()
        {
            var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            return conn;
        }

        private List<ReferenceRow> Run(string sql, string query, int maxResults)
        {
            var results = new List<ReferenceRow>();
            var like = "%" + (query ?? string.Empty).Trim() + "%";

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@like", like);
                cmd.Parameters.AddWithValue("@max", maxResults);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new ReferenceRow
                        {
                            Code = reader["code"] as string,
                            Name = reader["name"] as string,
                            Extra = reader["extra"] as string
                        });
                    }
                }
            }

            return results;
        }

        public List<ReferenceRow> SearchClasses(string query, int maxResults = 2000) => Run(
            "SELECT legacy_code AS code, name AS name, NULL AS extra FROM therapeutic_classes " +
            "WHERE name LIKE @like OR legacy_code LIKE @like ORDER BY legacy_code LIMIT @max;", query, maxResults);

        public List<ReferenceRow> SearchSubclasses(string query, int maxResults = 2000) => Run(
            "SELECT sc.legacy_code AS code, sc.name AS name, tc.name AS extra " +
            "FROM therapeutic_subclasses sc LEFT JOIN therapeutic_classes tc ON tc.id = sc.class_id " +
            "WHERE sc.name LIKE @like OR sc.legacy_code LIKE @like ORDER BY sc.legacy_code LIMIT @max;", query, maxResults);

        public List<ReferenceRow> SearchServices(string query, int maxResults = 2000) => Run(
            "SELECT sigle AS code, name AS name, legacy_center_code AS extra FROM services " +
            "WHERE name LIKE @like OR sigle LIKE @like ORDER BY name LIMIT @max;", query, maxResults);

        public List<ReferenceRow> SearchCountries(string query, int maxResults = 2000) => Run(
            "SELECT legacy_code AS code, name AS name, sigle AS extra FROM countries " +
            "WHERE name LIKE @like OR legacy_code LIKE @like ORDER BY name LIMIT @max;", query, maxResults);

        public List<ReferenceRow> SearchWilayas(string query, int maxResults = 2000) => Run(
            "SELECT legacy_code AS code, name AS name, NULL AS extra FROM wilayas " +
            "WHERE name LIKE @like OR legacy_code LIKE @like ORDER BY legacy_code LIMIT @max;", query, maxResults);

        public List<ReferenceRow> SearchGeoCodes(string query, int maxResults = 2000) => Run(
            "SELECT g.legacy_code AS code, g.name AS name, w.name AS extra " +
            "FROM geo_codes g LEFT JOIN wilayas w ON w.id = g.wilaya_id " +
            "WHERE g.name LIKE @like OR g.legacy_code LIKE @like ORDER BY g.legacy_code LIMIT @max;", query, maxResults);

        public List<ReferenceRow> SearchSanitarySectors(string query, int maxResults = 2000) => Run(
            "SELECT legacy_code AS code, name AS name, NULL AS extra FROM sanitary_sectors " +
            "WHERE name LIKE @like OR legacy_code LIKE @like ORDER BY legacy_code LIMIT @max;", query, maxResults);

        public List<ReferenceRow> SearchCenters(string query, int maxResults = 2000) => Run(
            "SELECT legacy_code AS code, name AS name, region AS extra FROM centers " +
            "WHERE name LIKE @like OR legacy_code LIKE @like ORDER BY name LIMIT @max;", query, maxResults);
    }
}
