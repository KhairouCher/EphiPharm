using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;

namespace Epipharm.Data
{
    /// <summary>
    /// "Transfert Inter-Etablissement" (MENU_MAP.md section 2) — send
    /// stock to another establishment. The real screenshot's "Code
    /// Service" field is actually an establishment code here (confirmed:
    /// "23122010" / "09091" resolve to real rows in the centers table,
    /// e.g. "C.H.U BLIDA" — not the services table every other screen's
    /// "Code Service" uses), so this writes to deliveries/movements the
    /// same way DeliveryEntryRepository does, but with service_id always
    /// NULL: that column references services(id), and a transfer's
    /// destination is a center, a genuinely different table — forcing it
    /// into the wrong FK would be worse than leaving it unresolved.
    /// legacy_service_raw keeps the real center code either way.
    ///
    /// Grid rows are DCI-level, not product-level (matching the real
    /// screenshot's columns), so saving depletes stock the same
    /// nearest-expiry-first way as DeliveryEntryRepository.
    ///
    /// No price/amount column is shown on this screen at all (unlike
    /// Reception de Produits), and a DCI can span several differently-
    /// priced products, so amount_centimes is saved as 0 rather than
    /// guessing a value from a different product's price.
    /// </summary>
    public class TransferEntryRepository
    {
        // Unconfirmed: distinct from deliveries' dominant '3' (normal
        // service delivery against a dotation) since a transfer isn't
        // that — '2' is a plausible candidate (52/16158 real delivery
        // rows, a small, "exceptional operation"-sized minority), but
        // this is this rebuild's own choice, not a decoded TYPE_MVT value.
        private const string TransferMovementType = "2";

        private readonly string _connectionString;

        public TransferEntryRepository(string databasePath)
        {
            _connectionString = $"Data Source={databasePath};Version=3;Foreign Keys=True;";
        }

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

        public int Save(CenterLookup center, string documentNo, DateTime operationDate,
            List<TransferEntryLine> lines, int accountId)
        {
            if (center == null)
            {
                throw new InvalidOperationException("Etablissement Obligatoire");
            }
            if (string.IsNullOrWhiteSpace(documentNo))
            {
                throw new InvalidOperationException("No de Piece Obligatoire");
            }
            var nonBlank = (lines ?? new List<TransferEntryLine>()).Where(l => l.DciId > 0).ToList();
            if (nonBlank.Count == 0)
            {
                throw new InvalidOperationException("Au Moins une Ligne Obligatoire");
            }
            foreach (var line in nonBlank)
            {
                if (line.Quantity <= 0)
                {
                    throw new InvalidOperationException("La Quantite doit etre Positive");
                }
            }

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

                        int deliveryId;
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = tx;
                            cmd.CommandText =
                                "INSERT INTO deliveries(operation_no, slip_no, delivered_on, legacy_service_raw, " +
                                "service_id, amount_centimes, document_no, movement_type) " +
                                "VALUES (@op, @slip, @date, @center, NULL, 0, @doc, @type); " +
                                "SELECT last_insert_rowid();";
                            cmd.Parameters.AddWithValue("@op", operationNo);
                            cmd.Parameters.AddWithValue("@slip", slipNo);
                            cmd.Parameters.AddWithValue("@date", operationDateText);
                            cmd.Parameters.AddWithValue("@center", center.LegacyCode);
                            cmd.Parameters.AddWithValue("@doc", documentNo.Trim());
                            cmd.Parameters.AddWithValue("@type", TransferMovementType);
                            deliveryId = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        var lineNo = 1;
                        foreach (var line in nonBlank)
                        {
                            DepleteStockFifo(conn, tx, line.DciId, line.Quantity, operationNo, ref lineNo,
                                operationDateText, documentNo.Trim(), center.LegacyCode);
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
                                $"Transfert {documentNo.Trim()} — {nonBlank.Count} DCI, etablissement {center.Label}");
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

        /// <summary>Same nearest-expiry-first depletion as
        /// DeliveryEntryRepository.DepleteStockFifo (duplicated rather than
        /// shared — each entry repository in this rebuild is self-
        /// contained, matching the existing pattern in this codebase).</summary>
        private static void DepleteStockFifo(SQLiteConnection conn, SQLiteTransaction tx, int dciId, int quantity,
            int operationNo, ref int lineNo, string operationDateText, string documentNo, string counterparty)
        {
            var remaining = quantity;
            var lots = new List<(int Id, int ProductId, int Quantity, int? Price)>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText =
                    "SELECT sl.id, sl.product_id, sl.quantity, sl.unit_price_centimes FROM stock_lots sl " +
                    "JOIN products p ON p.id = sl.product_id WHERE p.dci_id = @dci AND sl.quantity > 0 " +
                    "ORDER BY (sl.expires_on IS NULL), sl.expires_on ASC;";
                cmd.Parameters.AddWithValue("@dci", dciId);
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
                        "VALUES (@op, @line, @type, @center, @date, @doc, @product, @qty, @qty, @price);";
                    insert.Parameters.AddWithValue("@op", operationNo);
                    insert.Parameters.AddWithValue("@line", lineNo);
                    insert.Parameters.AddWithValue("@type", TransferMovementType);
                    insert.Parameters.AddWithValue("@center", counterparty);
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

            if (remaining > 0)
            {
                throw new InvalidOperationException(
                    $"Stock Insuffisant pour la DCI {dciId} (il manque {remaining} unite(s))");
            }
        }
    }
}
