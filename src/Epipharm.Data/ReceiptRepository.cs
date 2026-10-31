using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;

namespace Epipharm.Data
{
    public class ReceiptRepository
    {
        private readonly string _connectionString;

        public ReceiptRepository(string databasePath)
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
        /// Matches on document number, the raw legacy TIERS code, or the
        /// resolved supplier name (see ReceiptView.Tiers — which this
        /// mirrors) where the migration could decode one. Most recent
        /// first, same as the legacy "Liste Globale des Bons de Reception"
        /// default order.
        /// </summary>
        public List<ReceiptView> Search(string query)
        {
            var results = new List<ReceiptView>();
            var like = "%" + (query ?? string.Empty).Trim() + "%";

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT r.id, r.operation_no, r.slip_no, r.received_on, r.legacy_tiers_raw, " +
                    "s.name AS supplier_name, r.amount_centimes, r.document_no, r.movement_type " +
                    "FROM receipts r " +
                    "LEFT JOIN suppliers s ON s.id = r.supplier_id " +
                    "WHERE r.document_no LIKE @like OR r.legacy_tiers_raw LIKE @like OR s.name LIKE @like " +
                    "ORDER BY r.received_on DESC, r.id DESC;";
                cmd.Parameters.AddWithValue("@like", like);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new ReceiptView
                        {
                            Id = Convert.ToInt32(reader["id"]),
                            OperationNo = Convert.ToInt32(reader["operation_no"]),
                            SlipNo = Convert.ToInt32(reader["slip_no"]),
                            ReceivedOn = ParseDate(reader["received_on"] as string),
                            LegacyTiersRaw = reader["legacy_tiers_raw"] as string,
                            SupplierName = reader["supplier_name"] as string,
                            AmountCentimes = Convert.ToInt64(reader["amount_centimes"]),
                            DocumentNo = reader["document_no"] as string,
                            MovementType = reader["movement_type"] as string
                        });
                    }
                }
            }

            return results;
        }

        private static DateTime ParseDate(string isoDate)
        {
            if (string.IsNullOrWhiteSpace(isoDate))
            {
                return default;
            }
            return DateTime.TryParseExact(isoDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed) ? parsed : default;
        }
    }
}
