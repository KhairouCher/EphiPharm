using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;

namespace Epipharm.Data
{
    /// <summary>
    /// "Livraison sur Bon de Commande (Medicaments)" (MENU_MAP.md section
    /// 2) — deliver against a service's weekly dotation for the
    /// Medicaments Usuels category. Saving decreases stock (nearest-expiry
    /// lot first — this rebuild's own choice, not confirmed from the real
    /// app, but the obvious pharmacy rule and consistent with
    /// StockListForm's expired/expiring-soon highlighting), increases the
    /// matching allocation's delivered_qty, and writes one deliveries row
    /// + one movements row per lot actually drawn from, in one transaction.
    ///
    /// Only the Medicaments Usuels category is built — the real screen has
    /// a 3rd-level choice (Medicaments Usuels / Psychotropes / Autres
    /// Medicaments, see UI_SPEC.md) and this rebuild only implements the
    /// first; see MenuData.cs.
    /// </summary>
    public class DeliveryEntryRepository
    {
        private const string DeliveryMovementType = "3"; // dominant code for this shape in the real data (15553/16158)
        private const string DrugCategory = "medicament";

        private readonly string _connectionString;

        public DeliveryEntryRepository(string databasePath)
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
        /// Approximates the legacy WEEK_LIV week number as an ISO-8601-style
        /// week (Monday-start, week 1 = the week with the year's first
        /// Thursday). The real DBF's exact week-numbering convention was
        /// never confirmed against the running app — this is this
        /// rebuild's own reasonable choice for *new* entries, not a
        /// decoding of historical WEEK_LIV values (those are kept as
        /// imported, unchanged).
        /// </summary>
        public static int WeekOfYear(DateTime date)
        {
            var day = CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(date);
            if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
            {
                date = date.AddDays(3);
            }
            return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }

        /// <summary>Same shared settings.next_operation_no counter as
        /// ReceiptEntryRepository.PeekNextOperationNo — display-only, see
        /// that method's doc comment for the staleness caveat.</summary>
        public int PeekNextOperationNo()
        {
            using (var conn = new SQLiteConnection(_connectionString))
            using (var cmd = conn.CreateCommand())
            {
                conn.Open();
                cmd.CommandText = "SELECT next_operation_no FROM settings WHERE id = 1;";
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public List<DeliveryAllocationRow> GetAllocationRows(int serviceId, int year, int week)
        {
            var results = new List<DeliveryAllocationRow>();

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT d.id AS dci_id, d.legacy_code, d.name, d.default_price_centimes, " +
                    "a.allocation_qty, a.delivered_qty, " +
                    "(SELECT COALESCE(SUM(sl.quantity), 0) FROM stock_lots sl " +
                    " JOIN products p ON p.id = sl.product_id WHERE p.dci_id = d.id) AS stock_qty " +
                    "FROM allocations a " +
                    "JOIN dci d ON d.id = a.dci_id " +
                    "WHERE a.service_id = @service AND a.drug_category = @category " +
                    "AND a.year = @year AND a.week = @week " +
                    "ORDER BY d.legacy_code;";
                cmd.Parameters.AddWithValue("@service", serviceId);
                cmd.Parameters.AddWithValue("@category", DrugCategory);
                cmd.Parameters.AddWithValue("@year", year);
                cmd.Parameters.AddWithValue("@week", week);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var allocationQty = Convert.ToInt32(reader["allocation_qty"]);
                        var deliveredQty = Convert.ToInt32(reader["delivered_qty"]);
                        var stockQty = Convert.ToInt32(reader["stock_qty"]);
                        var remaining = Math.Max(0, allocationQty - deliveredQty);
                        var suggested = Math.Max(0, Math.Min(remaining, stockQty));

                        results.Add(new DeliveryAllocationRow
                        {
                            DciId = Convert.ToInt32(reader["dci_id"]),
                            DciCode = reader["legacy_code"] as string,
                            Label = reader["name"] as string,
                            StockQty = stockQty,
                            DotationRemaining = remaining,
                            AlreadyDelivered = deliveredQty,
                            QuantityToDeliver = suggested,
                            UnitPriceCentimes = reader["default_price_centimes"] == DBNull.Value
                                ? (int?)null
                                : Convert.ToInt32(reader["default_price_centimes"])
                        });
                    }
                }
            }

            return results;
        }

        public int Save(ServiceLookup service, string documentNo, DateTime operationDate,
            List<DeliveryAllocationRow> rows, int accountId)
        {
            if (service == null)
            {
                throw new InvalidOperationException("Service Obligatoire");
            }
            if (string.IsNullOrWhiteSpace(documentNo))
            {
                throw new InvalidOperationException("No de Piece Obligatoire");
            }
            var toDeliver = (rows ?? new List<DeliveryAllocationRow>())
                .Where(r => r.QuantityToDeliver > 0).ToList();
            if (toDeliver.Count == 0)
            {
                throw new InvalidOperationException("Au Moins une Quantite a Livrer Obligatoire");
            }
            foreach (var row in toDeliver)
            {
                if (row.QuantityToDeliver > row.StockQty)
                {
                    throw new InvalidOperationException($"{row.Label} : Quantite Superieure au Stock");
                }
                if (row.QuantityToDeliver > row.DotationRemaining)
                {
                    throw new InvalidOperationException($"{row.Label} : Quantite Superieure a la Dotation Restante");
                }
            }

            var year = operationDate.Year;
            var week = WeekOfYear(operationDate);

            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        var operationNo = NextCounter(conn, tx, "next_operation_no");
                        var slipNo = NextCounter(conn, tx, "next_delivery_slip_no");
                        var operationDateText = operationDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                        var amountCentimes = toDeliver.Sum(r => (long)r.QuantityToDeliver * (r.UnitPriceCentimes ?? 0));

                        int deliveryId;
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = tx;
                            cmd.CommandText =
                                "INSERT INTO deliveries(operation_no, slip_no, delivered_on, legacy_service_raw, " +
                                "service_id, amount_centimes, document_no, movement_type) " +
                                "VALUES (@op, @slip, @date, @service, @serviceId, @amount, @doc, @type); " +
                                "SELECT last_insert_rowid();";
                            cmd.Parameters.AddWithValue("@op", operationNo);
                            cmd.Parameters.AddWithValue("@slip", slipNo);
                            cmd.Parameters.AddWithValue("@date", operationDateText);
                            cmd.Parameters.AddWithValue("@service", service.Code);
                            cmd.Parameters.AddWithValue("@serviceId", service.Id);
                            cmd.Parameters.AddWithValue("@amount", amountCentimes);
                            cmd.Parameters.AddWithValue("@doc", documentNo.Trim());
                            cmd.Parameters.AddWithValue("@type", DeliveryMovementType);
                            deliveryId = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        var lineNo = 1;
                        foreach (var row in toDeliver)
                        {
                            DepleteStockFifo(conn, tx, row.DciId, row.QuantityToDeliver, operationNo, ref lineNo,
                                operationDateText, documentNo.Trim(), service.Code);
                            UpdateAllocationDelivered(conn, tx, service.Id, row.DciId, year, week, row.QuantityToDeliver);
                        }

                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = tx;
                            cmd.CommandText =
                                "INSERT INTO audit_log(account_id, table_name, row_id, action, detail) " +
                                "VALUES (@account, 'deliveries', @row, 'insert', @detail);";
                            cmd.Parameters.AddWithValue("@account", accountId);
                            cmd.Parameters.AddWithValue("@row", deliveryId);
                            cmd.Parameters.AddWithValue("@detail",
                                $"Livraison {documentNo.Trim()} — {toDeliver.Count} DCI, {amountCentimes / 100m:N2} DZD, service {service.Label}");
                            cmd.ExecuteNonQuery();
                        }

                        tx.Commit();
                        return deliveryId;
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        private static int NextCounter(SQLiteConnection conn, SQLiteTransaction tx, string column)
        {
            int value;
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = $"SELECT {column} FROM settings WHERE id = 1;";
                value = Convert.ToInt32(cmd.ExecuteScalar());
            }
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = $"UPDATE settings SET {column} = @next WHERE id = 1;";
                cmd.Parameters.AddWithValue("@next", value + 1);
                cmd.ExecuteNonQuery();
            }
            return value;
        }

        /// <summary>Consumes `quantity` units across this DCI's stock_lots,
        /// nearest-expiry-first (NULL expiry sorts last — treated as "not
        /// expiring soon" rather than depleted first), one movements row
        /// per lot actually drawn from. Throws if the DCI's total stock
        /// somehow doesn't cover the request (shouldn't happen — Save
        /// validates against the same StockQty total before calling this —
        /// but checked rather than silently going negative).</summary>
        private static void DepleteStockFifo(SQLiteConnection conn, SQLiteTransaction tx, int dciId, int quantity,
            int operationNo, ref int lineNo, string operationDateText, string documentNo, string serviceCode)
        {
            var remaining = quantity;
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText =
                    "SELECT sl.id, sl.product_id, sl.quantity, sl.unit_price_centimes FROM stock_lots sl " +
                    "JOIN products p ON p.id = sl.product_id WHERE p.dci_id = @dci AND sl.quantity > 0 " +
                    "ORDER BY (sl.expires_on IS NULL), sl.expires_on ASC;";
                cmd.Parameters.AddWithValue("@dci", dciId);

                var lots = new List<(int Id, int ProductId, int Quantity, int? Price)>();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lots.Add((
                            Convert.ToInt32(reader["id"]),
                            Convert.ToInt32(reader["product_id"]),
                            Convert.ToInt32(reader["quantity"]),
                            reader["unit_price_centimes"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["unit_price_centimes"])));
                    }
                }

                foreach (var lot in lots)
                {
                    if (remaining <= 0) break;
                    var take = Math.Min(remaining, lot.Quantity);

                    using (var update = conn.CreateCommand())
                    {
                        update.Transaction = tx;
                        update.CommandText = "UPDATE stock_lots SET quantity = quantity - @take WHERE id = @id;";
                        update.Parameters.AddWithValue("@take", take);
                        update.Parameters.AddWithValue("@id", lot.Id);
                        update.ExecuteNonQuery();
                    }

                    using (var insert = conn.CreateCommand())
                    {
                        insert.Transaction = tx;
                        insert.CommandText =
                            "INSERT INTO movements(operation_no, line_no, movement_type, counterparty, moved_on, " +
                            "document_no, product_id, quantity_requested, quantity_moved, unit_price_centimes) " +
                            "VALUES (@op, @line, @type, @service, @date, @doc, @product, @qty, @qty, @price);";
                        insert.Parameters.AddWithValue("@op", operationNo);
                        insert.Parameters.AddWithValue("@line", lineNo);
                        insert.Parameters.AddWithValue("@type", DeliveryMovementType);
                        insert.Parameters.AddWithValue("@service", serviceCode);
                        insert.Parameters.AddWithValue("@date", operationDateText);
                        insert.Parameters.AddWithValue("@doc", documentNo);
                        insert.Parameters.AddWithValue("@product", lot.ProductId);
                        insert.Parameters.AddWithValue("@qty", take);
                        insert.Parameters.AddWithValue("@price", (object)lot.Price ?? DBNull.Value);
                        insert.ExecuteNonQuery();
                    }

                    lineNo++;
                    remaining -= take;
                }
            }

            if (remaining > 0)
            {
                throw new InvalidOperationException(
                    $"Stock Insuffisant pour la DCI {dciId} (il manque {remaining} unite(s))");
            }
        }

        private static void UpdateAllocationDelivered(SQLiteConnection conn, SQLiteTransaction tx, int serviceId,
            int dciId, int year, int week, int quantity)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText =
                    "UPDATE allocations SET delivered_qty = delivered_qty + @qty " +
                    "WHERE service_id = @service AND dci_id = @dci AND drug_category = @category " +
                    "AND year = @year AND week = @week;";
                cmd.Parameters.AddWithValue("@qty", quantity);
                cmd.Parameters.AddWithValue("@service", serviceId);
                cmd.Parameters.AddWithValue("@dci", dciId);
                cmd.Parameters.AddWithValue("@category", DrugCategory);
                cmd.Parameters.AddWithValue("@year", year);
                cmd.Parameters.AddWithValue("@week", week);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
