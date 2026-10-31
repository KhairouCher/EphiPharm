using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;

namespace Epipharm.Data
{
    public class StockRepository
    {
        private const int DefaultExpiryWarningDays = 60; // legacy PARPHARM.PEREMPTION was 60 in the real data

        private readonly string _connectionString;

        public StockRepository(string databasePath)
        {
            _connectionString = $"Data Source={databasePath};Version=3;Foreign Keys=True;";
        }

        private SQLiteConnection Open()
        {
            var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            return conn;
        }

        /// <summary>
        /// Matches on product name, generic (DCI) name, or lot number — staff
        /// search by typing part of a drug's name, not a code (see MENU_MAP.md
        /// "UI conventions": lists over typed codes).
        /// </summary>
        public List<StockLotView> Search(string query)
        {
            var results = new List<StockLotView>();
            var like = "%" + (query ?? string.Empty).Trim() + "%";

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT sl.id, p.name AS product_name, d.name AS dci_name, f.name AS form_name, " +
                    "sl.lot_number, sl.expires_on, sl.quantity, sl.unit_price_centimes " +
                    "FROM stock_lots sl " +
                    "JOIN products p ON p.id = sl.product_id " +
                    "LEFT JOIN dci d ON d.id = p.dci_id " +
                    "LEFT JOIN dosage_forms f ON f.id = p.form_id " +
                    "WHERE p.name LIKE @like OR d.name LIKE @like OR sl.lot_number LIKE @like " +
                    "ORDER BY p.name, sl.expires_on;";
                cmd.Parameters.AddWithValue("@like", like);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new StockLotView
                        {
                            Id = Convert.ToInt32(reader["id"]),
                            ProductName = reader["product_name"] as string,
                            DciName = reader["dci_name"] as string,
                            FormName = reader["form_name"] as string,
                            LotNumber = reader["lot_number"] as string,
                            ExpiresOn = ParseDate(reader["expires_on"] as string),
                            Quantity = Convert.ToInt32(reader["quantity"]),
                            UnitPriceCentimes = reader["unit_price_centimes"] == DBNull.Value
                                ? (int?)null
                                : Convert.ToInt32(reader["unit_price_centimes"])
                        });
                    }
                }
            }

            return results;
        }

        public int GetExpiryWarningDays()
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT expiry_warning_days FROM settings WHERE id = 1;";
                var value = cmd.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                {
                    return DefaultExpiryWarningDays;
                }
                return Convert.ToInt32(value);
            }
        }

        private static DateTime? ParseDate(string isoDate)
        {
            if (string.IsNullOrWhiteSpace(isoDate))
            {
                return null;
            }
            if (DateTime.TryParseExact(isoDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsed))
            {
                return parsed;
            }
            return null;
        }
    }
}
