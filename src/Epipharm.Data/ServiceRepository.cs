using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Epipharm.Data
{
    public class ServiceRepository
    {
        /// <summary>
        /// The services table has two establishment codes in the real data
        /// (legacy_center_code '23124010' with 82 wards, '25120305' with
        /// just 2 generic rows). Deliveries in this rebuild are scoped to
        /// '23124010': confirmed against a real screenshot of the F5
        /// "Liste des Services Utilisateurs" popup on the Livraison sur Bon
        /// de Commande screen, whose codes/names (e.g. '03'=PEDIATRIE,
        /// '05'=P.URGENCE) only match services under that establishment,
        /// not '25120305'.
        /// </summary>
        public const string DeliveryEstablishmentCode = "23124010";

        private readonly string _connectionString;

        public ServiceRepository(string databasePath)
        {
            _connectionString = $"Data Source={databasePath};Version=3;Foreign Keys=True;";
        }

        private SQLiteConnection Open()
        {
            var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            return conn;
        }

        public List<ServiceLookup> Search(string query, int maxResults = 100)
        {
            var results = new List<ServiceLookup>();
            var like = "%" + (query ?? string.Empty).Trim() + "%";

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT id, sigle, name FROM services " +
                    "WHERE legacy_center_code = @center AND (sigle LIKE @like OR name LIKE @like) " +
                    "ORDER BY sigle LIMIT @max;";
                cmd.Parameters.AddWithValue("@center", DeliveryEstablishmentCode);
                cmd.Parameters.AddWithValue("@like", like);
                cmd.Parameters.AddWithValue("@max", maxResults);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new ServiceLookup
                        {
                            Id = Convert.ToInt32(reader["id"]),
                            Code = reader["sigle"] as string,
                            Name = reader["name"] as string
                        });
                    }
                }
            }

            return results;
        }

        /// <summary>Resolves a typed service code directly, for typing it
        /// into "Code Service" instead of using the F5 picker — same
        /// "type a code, or press F5" convention as
        /// ProductRepository.FindByCode / SupplierRepository.FindByFiveDigitCode.</summary>
        public ServiceLookup FindByCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT id, sigle, name FROM services WHERE legacy_center_code = @center AND sigle = @code LIMIT 1;";
                cmd.Parameters.AddWithValue("@center", DeliveryEstablishmentCode);
                cmd.Parameters.AddWithValue("@code", code.Trim());

                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }
                    return new ServiceLookup
                    {
                        Id = Convert.ToInt32(reader["id"]),
                        Code = reader["sigle"] as string,
                        Name = reader["name"] as string
                    };
                }
            }
        }
    }
}
