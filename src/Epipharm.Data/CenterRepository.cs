using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Epipharm.Data
{
    public class CenterRepository
    {
        private readonly string _connectionString;

        public CenterRepository(string databasePath)
        {
            _connectionString = $"Data Source={databasePath};Version=3;Foreign Keys=True;";
        }

        private SQLiteConnection Open()
        {
            var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            return conn;
        }

        public List<CenterLookup> Search(string query, int maxResults = 100)
        {
            var results = new List<CenterLookup>();
            var like = "%" + (query ?? string.Empty).Trim() + "%";

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT id, legacy_code, name FROM centers " +
                    "WHERE name LIKE @like OR legacy_code LIKE @like ORDER BY name LIMIT @max;";
                cmd.Parameters.AddWithValue("@like", like);
                cmd.Parameters.AddWithValue("@max", maxResults);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new CenterLookup
                        {
                            Id = Convert.ToInt32(reader["id"]),
                            LegacyCode = reader["legacy_code"] as string,
                            Name = reader["name"] as string
                        });
                    }
                }
            }

            return results;
        }

        public CenterLookup FindByCode(string legacyCode)
        {
            if (string.IsNullOrWhiteSpace(legacyCode)) return null;

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id, legacy_code, name FROM centers WHERE legacy_code = @code LIMIT 1;";
                cmd.Parameters.AddWithValue("@code", legacyCode.Trim());
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new CenterLookup
                    {
                        Id = Convert.ToInt32(reader["id"]),
                        LegacyCode = reader["legacy_code"] as string,
                        Name = reader["name"] as string
                    };
                }
            }
        }
    }
}
