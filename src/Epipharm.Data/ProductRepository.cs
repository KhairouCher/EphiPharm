using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Epipharm.Data
{
    public class ProductRepository
    {
        private readonly string _connectionString;

        public ProductRepository(string databasePath)
        {
            _connectionString = $"Data Source={databasePath};Version=3;Foreign Keys=True;";
        }

        private SQLiteConnection Open()
        {
            var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            return conn;
        }

        /// <summary>Matches on product name, DCI name, or legacy code — the
        /// same "type part of a name, pick from the list" pattern as
        /// StockRepository.Search (see MENU_MAP.md UI conventions).
        /// Withdrawn products (withdrawn_on set) are excluded: a receipt
        /// can't bring stock in for a product the legacy app has retired.</summary>
        public List<ProductLookup> Search(string query, int maxResults = 50)
        {
            var results = new List<ProductLookup>();
            var like = "%" + (query ?? string.Empty).Trim() + "%";

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT p.id, p.legacy_code, p.name AS product_name, d.name AS dci_name, f.name AS form_name " +
                    "FROM products p " +
                    "LEFT JOIN dci d ON d.id = p.dci_id " +
                    "LEFT JOIN dosage_forms f ON f.id = p.form_id " +
                    "WHERE p.withdrawn_on IS NULL " +
                    "AND (p.name LIKE @like OR d.name LIKE @like OR p.legacy_code LIKE @like) " +
                    "ORDER BY p.name LIMIT @max;";
                cmd.Parameters.AddWithValue("@like", like);
                cmd.Parameters.AddWithValue("@max", maxResults);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new ProductLookup
                        {
                            Id = Convert.ToInt32(reader["id"]),
                            LegacyCode = reader["legacy_code"] as string,
                            Name = reader["product_name"] as string,
                            DciName = reader["dci_name"] as string,
                            FormName = reader["form_name"] as string
                        });
                    }
                }
            }

            return results;
        }
    }
}
