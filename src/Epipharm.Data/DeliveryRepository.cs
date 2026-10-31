using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;

namespace Epipharm.Data
{
    public class DeliveryRepository
    {
        private readonly string _connectionString;

        public DeliveryRepository(string databasePath)
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
        /// Matches on document number or the raw legacy service code
        /// (there's no resolved service name to search by yet — see
        /// DeliveryView). Most recent first, same as the legacy "Liste
        /// Globale des Bons de Livraison/Restitution" default order.
        /// </summary>
        public List<DeliveryView> Search(string query)
        {
            var results = new List<DeliveryView>();
            var like = "%" + (query ?? string.Empty).Trim() + "%";

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT d.id, d.operation_no, d.slip_no, d.delivered_on, d.legacy_service_raw, " +
                    "sv.name AS service_name, d.amount_centimes, d.document_no, d.movement_type " +
                    "FROM deliveries d " +
                    "LEFT JOIN services sv ON sv.id = d.service_id " +
                    "WHERE d.document_no LIKE @like OR d.legacy_service_raw LIKE @like " +
                    "ORDER BY d.delivered_on DESC, d.id DESC;";
                cmd.Parameters.AddWithValue("@like", like);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new DeliveryView
                        {
                            Id = Convert.ToInt32(reader["id"]),
                            OperationNo = Convert.ToInt32(reader["operation_no"]),
                            SlipNo = Convert.ToInt32(reader["slip_no"]),
                            DeliveredOn = ParseDate(reader["delivered_on"] as string),
                            LegacyServiceRaw = reader["legacy_service_raw"] as string,
                            ServiceName = reader["service_name"] as string,
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
