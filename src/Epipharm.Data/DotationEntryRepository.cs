using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Epipharm.Data
{
    /// <summary>
    /// "Mise a Jour des Dotations en Medicaments Usuels/Psychotropes/
    /// Autres Medicaments/Stupefiants" — set every DCI of one drug
    /// category's weekly allocation for a service (DCI has no real
    /// "discontinued" flag to filter on — see GetRows). This is what
    /// DeliveryEntryRepository's "Dota." column actually reads, so
    /// without this screen the dotation figures could only ever come from
    /// the original DBF import, never be set or changed for real.
    ///
    /// One instance per drug_category — the four "Mise a Jour des
    /// Dotations..." menu leaves (MENU_MAP.md section 5) are this same
    /// screen/repository parameterized by category, not four separate
    /// implementations.
    /// </summary>
    public class DotationEntryRepository
    {
        private readonly string _drugCategory;
        private readonly string _connectionString;

        public DotationEntryRepository(string databasePath, string drugCategory)
        {
            _connectionString = $"Data Source={databasePath};Version=3;Foreign Keys=True;";
            _drugCategory = drugCategory ?? throw new ArgumentNullException(nameof(drugCategory));
        }

        private SQLiteConnection Open()
        {
            var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            return conn;
        }

        public List<DotationRow> GetRows(int serviceId, int year, int week)
        {
            var results = new List<DotationRow>();

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT d.id, d.legacy_code, d.name, COALESCE(a.allocation_qty, 0) AS dotation " +
                    "FROM dci d " +
                    "LEFT JOIN allocations a ON a.dci_id = d.id AND a.service_id = @service " +
                    "  AND a.drug_category = @category AND a.year = @year AND a.week = @week " +
                    // No "is active" filter here: dci has no real
                    // discontinued flag (see db/schema.sql's long note on
                    // projected_rupture_on) — filtering on that field used
                    // to hide 68% of medicament DCI from this exact screen.
                    "WHERE d.drug_category = @category " +
                    "ORDER BY d.legacy_code;";
                cmd.Parameters.AddWithValue("@service", serviceId);
                cmd.Parameters.AddWithValue("@category", _drugCategory);
                cmd.Parameters.AddWithValue("@year", year);
                cmd.Parameters.AddWithValue("@week", week);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new DotationRow
                        {
                            DciId = Convert.ToInt32(reader["id"]),
                            DciCode = reader["legacy_code"] as string,
                            Label = reader["name"] as string,
                            Dotation = Convert.ToInt32(reader["dotation"])
                        });
                    }
                }
            }

            return results;
        }

        /// <summary>Upserts each row's dotation — update if a row already
        /// exists for (service, dci, category, year, week), insert a new
        /// one (delivered_qty starts at 0) otherwise. A dotation of 0 is
        /// saved like any other value, not treated as "nothing to do":
        /// zeroing one out is a legitimate update (MENU_MAP.md notes a
        /// dotation can't be *deleted* once justified, but setting it to 0
        /// isn't a delete).</summary>
        public void Save(int serviceId, int year, int week, List<DotationRow> rows, int accountId)
        {
            if (rows == null || rows.Count == 0)
            {
                throw new InvalidOperationException("Aucune Donnee a Enregistrer");
            }

            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        var changed = 0;
                        foreach (var row in rows)
                        {
                            int existingId;
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = tx;
                                cmd.CommandText =
                                    "SELECT id FROM allocations WHERE service_id=@service AND dci_id=@dci " +
                                    "AND drug_category=@category AND year=@year AND week=@week;";
                                cmd.Parameters.AddWithValue("@service", serviceId);
                                cmd.Parameters.AddWithValue("@dci", row.DciId);
                                cmd.Parameters.AddWithValue("@category", _drugCategory);
                                cmd.Parameters.AddWithValue("@year", year);
                                cmd.Parameters.AddWithValue("@week", week);
                                var result = cmd.ExecuteScalar();
                                existingId = result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
                            }

                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = tx;
                                if (existingId > 0)
                                {
                                    cmd.CommandText = "UPDATE allocations SET allocation_qty=@qty WHERE id=@id;";
                                    cmd.Parameters.AddWithValue("@qty", row.Dotation);
                                    cmd.Parameters.AddWithValue("@id", existingId);
                                }
                                else
                                {
                                    cmd.CommandText =
                                        "INSERT INTO allocations(center_legacy_code, service_id, dci_id, drug_category, " +
                                        "allocation_qty, year, week, delivered_qty, justified) " +
                                        "VALUES ('23124010', @service, @dci, @category, @qty, @year, @week, 0, 0);";
                                    cmd.Parameters.AddWithValue("@service", serviceId);
                                    cmd.Parameters.AddWithValue("@dci", row.DciId);
                                    cmd.Parameters.AddWithValue("@category", _drugCategory);
                                    cmd.Parameters.AddWithValue("@qty", row.Dotation);
                                    cmd.Parameters.AddWithValue("@year", year);
                                    cmd.Parameters.AddWithValue("@week", week);
                                }
                                cmd.ExecuteNonQuery();
                            }
                            changed++;
                        }

                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = tx;
                            cmd.CommandText =
                                "INSERT INTO audit_log(account_id, table_name, action, detail) " +
                                "VALUES (@account, 'allocations', 'update', @detail);";
                            cmd.Parameters.AddWithValue("@account", accountId);
                            cmd.Parameters.AddWithValue("@detail",
                                $"Mise a jour dotations ({_drugCategory}) — service {serviceId}, annee {year}, semaine {week}, {changed} DCI");
                            cmd.ExecuteNonQuery();
                        }

                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}
