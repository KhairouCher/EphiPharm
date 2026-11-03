using System;
using System.Data.SQLite;

namespace Epipharm.Data
{
    public class SettingsRepository
    {
        private readonly string _connectionString;

        public SettingsRepository(string databasePath)
        {
            _connectionString = $"Data Source={databasePath};Version=3;Foreign Keys=True;";
        }

        private SQLiteConnection Open()
        {
            var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            return conn;
        }

        public SettingsInfo Get()
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT facility_code, facility_name, authority_name, current_year, expiry_warning_days, " +
                    "archived_through_date, next_operation_no, next_delivery_slip_no, next_receipt_slip_no, " +
                    "next_order_no, next_correction_no, next_inventory_no FROM settings WHERE id = 1;";
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }
                    return new SettingsInfo
                    {
                        FacilityCode = reader["facility_code"] as string,
                        FacilityName = reader["facility_name"] as string,
                        AuthorityName = reader["authority_name"] as string,
                        CurrentYear = Convert.ToInt32(reader["current_year"]),
                        ExpiryWarningDays = reader["expiry_warning_days"] == DBNull.Value
                            ? (int?)null : Convert.ToInt32(reader["expiry_warning_days"]),
                        ArchivedThroughDate = reader["archived_through_date"] as string,
                        NextOperationNo = Convert.ToInt32(reader["next_operation_no"]),
                        NextDeliverySlipNo = Convert.ToInt32(reader["next_delivery_slip_no"]),
                        NextReceiptSlipNo = Convert.ToInt32(reader["next_receipt_slip_no"]),
                        NextOrderNo = Convert.ToInt32(reader["next_order_no"]),
                        NextCorrectionNo = Convert.ToInt32(reader["next_correction_no"]),
                        NextInventoryNo = Convert.ToInt32(reader["next_inventory_no"])
                    };
                }
            }
        }

        public void Save(SettingsInfo info, int accountId)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            if (string.IsNullOrWhiteSpace(info.FacilityCode))
            {
                throw new InvalidOperationException("Code Etablissement Obligatoire");
            }
            if (string.IsNullOrWhiteSpace(info.FacilityName))
            {
                throw new InvalidOperationException("Nom Etablissement Obligatoire");
            }

            using (var conn = Open())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        "UPDATE settings SET facility_code=@code, facility_name=@name, authority_name=@authority, " +
                        "current_year=@year, expiry_warning_days=@expiry WHERE id = 1;";
                    cmd.Parameters.AddWithValue("@code", info.FacilityCode.Trim());
                    cmd.Parameters.AddWithValue("@name", info.FacilityName.Trim());
                    cmd.Parameters.AddWithValue("@authority", (object)info.AuthorityName?.Trim() ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@year", info.CurrentYear);
                    cmd.Parameters.AddWithValue("@expiry", (object)info.ExpiryWarningDays ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        "INSERT INTO audit_log(account_id, table_name, row_id, action, detail) " +
                        "VALUES (@account, 'settings', 1, 'update', @detail);";
                    cmd.Parameters.AddWithValue("@account", accountId);
                    cmd.Parameters.AddWithValue("@detail",
                        $"Parametres d'exploitation — {info.FacilityCode.Trim()} {info.FacilityName.Trim()}, annee {info.CurrentYear}");
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
