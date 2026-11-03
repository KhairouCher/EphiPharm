using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;

namespace Epipharm.Data
{
    /// <summary>
    /// "Edition Commande" / "Liste des Commandes" / "Annulation Commande"
    /// (MENU_MAP.md section 4). purchase_orders/purchase_order_lines exist
    /// in db/schema.sql but were never populated: COMMANDE.DBF was empty
    /// in the source data, and DETAIL_C.DBF's 405 rows are all orphaned
    /// (no parent order to join against) — so unlike every other entry
    /// screen in this rebuild, there is no real historical example to
    /// check a save/list/cancel round-trip against. Built anyway, since
    /// the schema and the legacy rules (MENU_MAP.md: an order is "Lancee"
    /// or "Annulee"; a cancelled or not-yet-issued order can't have a
    /// movement reference it) are both confirmed, just never previously
    /// exercised.
    /// </summary>
    public class CommandeRepository
    {
        private readonly string _connectionString;

        public CommandeRepository(string databasePath)
        {
            _connectionString = $"Data Source={databasePath};Version=3;Foreign Keys=True;";
        }

        private SQLiteConnection Open()
        {
            var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            return conn;
        }

        public List<CommandeRow> List(int maxResults = 200)
        {
            var results = new List<CommandeRow>();
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT po.id, po.order_no, po.ordered_on, s.sigle AS supplier_sigle, " +
                    "po.delivered_flag, po.cancelled_on " +
                    "FROM purchase_orders po LEFT JOIN suppliers s ON s.id = po.supplier_id " +
                    "ORDER BY po.order_no DESC LIMIT @max;";
                cmd.Parameters.AddWithValue("@max", maxResults);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new CommandeRow
                        {
                            Id = Convert.ToInt32(reader["id"]),
                            OrderNo = Convert.ToInt32(reader["order_no"]),
                            OrderedOn = reader["ordered_on"] as string,
                            SupplierSigle = reader["supplier_sigle"] as string,
                            DeliveredFlag = reader["delivered_flag"] as string,
                            CancelledOn = reader["cancelled_on"] as string
                        });
                    }
                }
            }
            return results;
        }

        public int Save(SupplierLookup supplier, DateTime orderedOn, List<CommandeLine> lines, int accountId)
        {
            if (supplier == null)
            {
                throw new InvalidOperationException("Fournisseur Obligatoire");
            }
            var nonBlank = (lines ?? new List<CommandeLine>()).Where(l => l.DciId > 0).ToList();
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
                        var orderNo = NextCounter(conn, tx, "next_order_no");
                        var orderedOnText = orderedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                        int orderId;
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = tx;
                            cmd.CommandText =
                                "INSERT INTO purchase_orders(order_no, ordered_on, supplier_id) " +
                                "VALUES (@no, @date, @supplier); SELECT last_insert_rowid();";
                            cmd.Parameters.AddWithValue("@no", orderNo);
                            cmd.Parameters.AddWithValue("@date", orderedOnText);
                            cmd.Parameters.AddWithValue("@supplier", supplier.Id);
                            orderId = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        var lineNo = 1;
                        foreach (var line in nonBlank)
                        {
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = tx;
                                cmd.CommandText =
                                    "INSERT INTO purchase_order_lines(order_id, line_no, dci_id, quantity_proposed, " +
                                    "quantity_ordered) VALUES (@order, @line, @dci, @qty, @qty);";
                                cmd.Parameters.AddWithValue("@order", orderId);
                                cmd.Parameters.AddWithValue("@line", lineNo);
                                cmd.Parameters.AddWithValue("@dci", line.DciId);
                                cmd.Parameters.AddWithValue("@qty", line.Quantity);
                                cmd.ExecuteNonQuery();
                            }
                            lineNo++;
                        }

                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = tx;
                            cmd.CommandText =
                                "INSERT INTO audit_log(account_id, table_name, row_id, action, detail) " +
                                "VALUES (@account, 'purchase_orders', @row, 'insert', @detail);";
                            cmd.Parameters.AddWithValue("@account", accountId);
                            cmd.Parameters.AddWithValue("@row", orderId);
                            cmd.Parameters.AddWithValue("@detail",
                                $"Commande {orderNo} — {nonBlank.Count} DCI, fournisseur {supplier.Label}");
                            cmd.ExecuteNonQuery();
                        }

                        tx.Commit();
                        return orderId;
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>"Annulation Commande": sets cancelled_on. Refuses if
        /// the order already has a delivered_flag set (matching MENU_MAP's
        /// confirmed rule — rejected if a movement references it — the
        /// closest proxy for "has a movement" that this schema actually
        /// tracks on the order itself) or is already cancelled.</summary>
        public void Cancel(int orderId, DateTime cancelledOn, int accountId)
        {
            using (var conn = Open())
            {
                string deliveredFlag, cancelledAlready;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT delivered_flag, cancelled_on FROM purchase_orders WHERE id = @id;";
                    cmd.Parameters.AddWithValue("@id", orderId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            throw new InvalidOperationException("Commande Inexistante");
                        }
                        deliveredFlag = reader["delivered_flag"] as string;
                        cancelledAlready = reader["cancelled_on"] as string;
                    }
                }
                if (!string.IsNullOrEmpty(cancelledAlready))
                {
                    throw new InvalidOperationException("Commande Deja Annulee");
                }
                if (!string.IsNullOrEmpty(deliveredFlag))
                {
                    throw new InvalidOperationException("Commande Deja Mouvementee — Annulation Impossible");
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE purchase_orders SET cancelled_on = @date WHERE id = @id;";
                    cmd.Parameters.AddWithValue("@date", cancelledOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    cmd.Parameters.AddWithValue("@id", orderId);
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        "INSERT INTO audit_log(account_id, table_name, row_id, action, detail) " +
                        "VALUES (@account, 'purchase_orders', @row, 'update', 'Annulation Commande');";
                    cmd.Parameters.AddWithValue("@account", accountId);
                    cmd.Parameters.AddWithValue("@row", orderId);
                    cmd.ExecuteNonQuery();
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
    }
}
