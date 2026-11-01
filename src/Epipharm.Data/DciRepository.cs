using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Epipharm.Data
{
    public class DciRepository
    {
        private readonly string _connectionString;

        public DciRepository(string databasePath)
        {
            _connectionString = $"Data Source={databasePath};Version=3;Foreign Keys=True;";
        }

        private SQLiteConnection Open()
        {
            var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            return conn;
        }

        private const string BaseQuery =
            "SELECT d.id, d.legacy_code, sc.legacy_code AS subclass_code, tc.name AS class_name, " +
            "sc.name AS subclass_name, d.name, f.legacy_code AS form_code, f.sigle AS form_sigle, " +
            "f.dosage AS form_dosage, d.drug_category, d.tableau_b " +
            "FROM dci d " +
            "LEFT JOIN therapeutic_subclasses sc ON sc.id = d.subclass_id " +
            "LEFT JOIN therapeutic_classes tc ON tc.id = sc.class_id " +
            "LEFT JOIN dosage_forms f ON f.id = d.form_id ";

        public List<DciLookup> Search(string query, int maxResults = 50)
        {
            var results = new List<DciLookup>();
            var like = "%" + (query ?? string.Empty).Trim() + "%";

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = BaseQuery + "WHERE d.name LIKE @like OR d.legacy_code LIKE @like ORDER BY d.name LIMIT @max;";
                cmd.Parameters.AddWithValue("@like", like);
                cmd.Parameters.AddWithValue("@max", maxResults);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(Read(reader));
                    }
                }
            }

            return results;
        }

        public DciLookup FindByCode(string legacyCode)
        {
            if (string.IsNullOrWhiteSpace(legacyCode)) return null;
            var normalized = legacyCode.Replace(".", string.Empty).Trim();

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = BaseQuery + "WHERE d.legacy_code = @code LIMIT 1;";
                cmd.Parameters.AddWithValue("@code", normalized);
                using (var reader = cmd.ExecuteReader())
                {
                    return reader.Read() ? Read(reader) : null;
                }
            }
        }

        private static DciLookup Read(SQLiteDataReader reader)
        {
            var subclassCode = reader["subclass_code"] as string;
            return new DciLookup
            {
                Id = Convert.ToInt32(reader["id"]),
                LegacyCode = reader["legacy_code"] as string,
                // subclass legacy_code is C_T+S_T (e.g. "01A") — the
                // screenshot's "S/Classe" field shows just the trailing
                // S_T letter, not the full code.
                SubclassFlag = string.IsNullOrEmpty(subclassCode) ? string.Empty : subclassCode.Substring(subclassCode.Length - 1),
                ClassName = reader["class_name"] as string,
                SubclassName = reader["subclass_name"] as string,
                Name = reader["name"] as string,
                FormCode = reader["form_code"] as string,
                FormSigle = reader["form_sigle"] as string,
                FormDosage = reader["form_dosage"] as string,
                DrugCategory = reader["drug_category"] as string,
                TableauB = Convert.ToInt64(reader["tableau_b"]) != 0
            };
        }
    }
}
