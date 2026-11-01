using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Epipharm.Data
{
    public class SupplierRepository
    {
        private readonly string _connectionString;

        public SupplierRepository(string databasePath)
        {
            _connectionString = $"Data Source={databasePath};Version=3;Foreign Keys=True;";
        }

        private SQLiteConnection Open()
        {
            var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            return conn;
        }

        public List<SupplierLookup> Search(string query, int maxResults = 50)
        {
            var results = new List<SupplierLookup>();
            var like = "%" + (query ?? string.Empty).Trim() + "%";

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT id, legacy_code, sigle, name FROM suppliers " +
                    "WHERE sigle LIKE @like OR name LIKE @like OR legacy_code LIKE @like " +
                    "ORDER BY sigle LIMIT @max;";
                cmd.Parameters.AddWithValue("@like", like);
                cmd.Parameters.AddWithValue("@max", maxResults);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new SupplierLookup
                        {
                            Id = Convert.ToInt32(reader["id"]),
                            LegacyCode = reader["legacy_code"] as string,
                            Sigle = reader["sigle"] as string,
                            Name = reader["name"] as string
                        });
                    }
                }
            }

            return results;
        }

        /// <summary>The "213"+N_O code the legacy app displays/types as one
        /// field (e.g. "21312" — see db/schema.sql's note on RECEPT.TIERS).
        /// New receipts store this so they stay consistent with imported
        /// history rather than inventing a different format for new rows.</summary>
        public string LegacyFiveDigitCode(string legacyCode) => "213" + legacyCode;

        /// <summary>The other direction of LegacyFiveDigitCode — resolves a
        /// typed "213"+N_O code back to a supplier, for typing the code
        /// directly into "Code Fournisseur" instead of using the F5
        /// picker.</summary>
        public SupplierLookup FindByFiveDigitCode(string code5)
        {
            if (string.IsNullOrEmpty(code5) || code5.Length != 5 || !code5.StartsWith("213"))
            {
                return null;
            }

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id, legacy_code, sigle, name FROM suppliers WHERE legacy_code = @no LIMIT 1;";
                cmd.Parameters.AddWithValue("@no", code5.Substring(3));

                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }
                    return new SupplierLookup
                    {
                        Id = Convert.ToInt32(reader["id"]),
                        LegacyCode = reader["legacy_code"] as string,
                        Sigle = reader["sigle"] as string,
                        Name = reader["name"] as string
                    };
                }
            }
        }
    }
}
