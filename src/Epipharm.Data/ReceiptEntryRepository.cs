using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;

namespace Epipharm.Data
{
    /// <summary>
    /// Saves a new supplier receipt ("Reception de Produits : Reception
    /// Fournisseur" — see UI_SPEC.md / MENU_MAP.md section 3). Writes
    /// receipts + movements + stock_lots + the settings operation counters
    /// in one transaction, so a failure partway through can't leave stock
    /// half-updated.
    ///
    /// movement_type is set to '1' for every receipt this rebuild creates —
    /// the dominant code for the confirmed supplier-TIERS shape in the real
    /// data (2344/2410 rows), but the legacy TYPE_MVT code's exact meaning
    /// per value was never fully decoded (see db/schema.sql), so this is
    /// this rebuild's own choice of "normal receipt", not a confirmed
    /// mapping. counterparty/legacy_tiers_raw on the movement/receipt rows
    /// use the supplier's real "213"+N_O code so new rows look like
    /// imported history, not a different format.
    /// </summary>
    public class ReceiptEntryRepository
    {
        private const string ReceiptMovementType = "1";

        private readonly string _connectionString;

        public ReceiptEntryRepository(string databasePath)
        {
            _connectionString = $"Data Source={databasePath};Version=3;Foreign Keys=True;";
        }

        /// <summary>The operation number this receipt would get if saved
        /// right now — display-only (see "Operation :" on the real
        /// screenshot of this screen), not reserved. The real value is
        /// only assigned transactionally inside Save, so this can go stale
        /// if another receipt is saved in between on a multi-user setup;
        /// acceptable for this single-writer-in-practice internal tool,
        /// same assumption NextCounter below already makes.</summary>
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

        public int Save(SupplierLookup supplier, string documentNo, DateTime receivedOn,
            List<ReceiptEntryLine> lines, int accountId)
        {
            if (supplier == null)
            {
                throw new InvalidOperationException("Fournisseur Obligatoire");
            }
            if (string.IsNullOrWhiteSpace(documentNo))
            {
                throw new InvalidOperationException("No de Piece Obligatoire");
            }
            if (lines == null || lines.Count == 0)
            {
                throw new InvalidOperationException("Au Moins une Ligne Obligatoire");
            }
            foreach (var line in lines)
            {
                if (line.Quantity <= 0)
                {
                    throw new InvalidOperationException("La Quantite doit etre Positive");
                }
            }
            var duplicateLot = lines.GroupBy(l => (l.ProductId, l.LotNumber))
                .FirstOrDefault(g => g.Count() > 1);
            if (duplicateLot != null)
            {
                throw new InvalidOperationException("Produit Existe Deja Mouvemente (meme lot, cette reception)");
            }

            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        var operationNo = NextCounter(conn, tx, "next_operation_no");
                        var slipNo = NextCounter(conn, tx, "next_receipt_slip_no");
                        var tiersRaw = "213" + supplier.LegacyCode;
                        var receivedOnText = receivedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                        var amountCentimes = lines.Sum(l => (long)l.Quantity * l.UnitPriceCentimes);

                        int receiptId;
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = tx;
                            cmd.CommandText =
                                "INSERT INTO receipts(operation_no, slip_no, received_on, legacy_tiers_raw, " +
                                "supplier_id, amount_centimes, document_no, movement_type) " +
                                "VALUES (@op, @slip, @date, @tiers, @supplier, @amount, @doc, @type); " +
                                "SELECT last_insert_rowid();";
                            cmd.Parameters.AddWithValue("@op", operationNo);
                            cmd.Parameters.AddWithValue("@slip", slipNo);
                            cmd.Parameters.AddWithValue("@date", receivedOnText);
                            cmd.Parameters.AddWithValue("@tiers", tiersRaw);
                            cmd.Parameters.AddWithValue("@supplier", supplier.Id);
                            cmd.Parameters.AddWithValue("@amount", amountCentimes);
                            cmd.Parameters.AddWithValue("@doc", documentNo.Trim());
                            cmd.Parameters.AddWithValue("@type", ReceiptMovementType);
                            receiptId = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        var lineNo = 1;
                        foreach (var line in lines)
                        {
                            var expiresOnText = line.ExpiresOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                            var manufacturedOnText = line.ManufacturedOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = tx;
                                cmd.CommandText =
                                    "INSERT INTO movements(operation_no, line_no, movement_type, counterparty, " +
                                    "moved_on, expires_on, document_no, product_id, quantity_requested, " +
                                    "quantity_moved, unit_price_centimes) " +
                                    "VALUES (@op, @line, @type, @tiers, @date, @expires, @doc, @product, @qty, @qty, @price);";
                                cmd.Parameters.AddWithValue("@op", operationNo);
                                cmd.Parameters.AddWithValue("@line", lineNo);
                                cmd.Parameters.AddWithValue("@type", ReceiptMovementType);
                                cmd.Parameters.AddWithValue("@tiers", tiersRaw);
                                cmd.Parameters.AddWithValue("@date", receivedOnText);
                                cmd.Parameters.AddWithValue("@expires", (object)expiresOnText ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@doc", documentNo.Trim());
                                cmd.Parameters.AddWithValue("@product", line.ProductId);
                                cmd.Parameters.AddWithValue("@qty", line.Quantity);
                                cmd.Parameters.AddWithValue("@price", line.UnitPriceCentimes);
                                cmd.ExecuteNonQuery();
                            }

                            UpsertStockLot(conn, tx, line, expiresOnText, manufacturedOnText);
                            lineNo++;
                        }

                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = tx;
                            cmd.CommandText =
                                "INSERT INTO audit_log(account_id, table_name, row_id, action, detail) " +
                                "VALUES (@account, 'receipts', @row, 'insert', @detail);";
                            cmd.Parameters.AddWithValue("@account", accountId);
                            cmd.Parameters.AddWithValue("@row", receiptId);
                            cmd.Parameters.AddWithValue("@detail",
                                $"Reception {documentNo.Trim()} — {lines.Count} ligne(s), {amountCentimes / 100m:N2} DZD, fournisseur {supplier.Label}");
                            cmd.ExecuteNonQuery();
                        }

                        tx.Commit();
                        return receiptId;
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>Reads and increments a settings counter inside the same
        /// transaction, so two receipts can't be assigned the same
        /// operation number (this app is single-writer at a time in
        /// practice, but this keeps the invariant true regardless).</summary>
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

        /// <summary>Adds the received quantity to the matching (product,
        /// lot) stock row, creating it if this is a new lot — matching the
        /// legacy rule that stock only ever enters through a receipt (see
        /// MENU_MAP.md section 1).</summary>
        private static void UpsertStockLot(SQLiteConnection conn, SQLiteTransaction tx, ReceiptEntryLine line,
            string expiresOnText, string manufacturedOnText)
        {
            int? existingId = null;
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT id FROM stock_lots WHERE product_id = @product AND lot_number = @lot;";
                cmd.Parameters.AddWithValue("@product", line.ProductId);
                cmd.Parameters.AddWithValue("@lot", line.LotNumber);
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    existingId = Convert.ToInt32(result);
                }
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                if (existingId.HasValue)
                {
                    cmd.CommandText =
                        "UPDATE stock_lots SET quantity = quantity + @qty, unit_price_centimes = @price, " +
                        "expires_on = COALESCE(@expires, expires_on), " +
                        "manufactured_on = COALESCE(@fabri, manufactured_on) WHERE id = @id;";
                    cmd.Parameters.AddWithValue("@id", existingId.Value);
                }
                else
                {
                    cmd.CommandText =
                        "INSERT INTO stock_lots(product_id, lot_number, expires_on, manufactured_on, quantity, " +
                        "unit_price_centimes) VALUES (@product, @lot, @expires, @fabri, @qty, @price);";
                    cmd.Parameters.AddWithValue("@product", line.ProductId);
                    cmd.Parameters.AddWithValue("@lot", line.LotNumber);
                }
                cmd.Parameters.AddWithValue("@qty", line.Quantity);
                cmd.Parameters.AddWithValue("@price", line.UnitPriceCentimes);
                cmd.Parameters.AddWithValue("@expires", (object)expiresOnText ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fabri", (object)manufacturedOnText ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
