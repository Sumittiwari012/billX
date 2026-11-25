using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace MyWPFCRUDApp.Services
{
    /// <summary>
    /// Cloud-first stock writes.
    ///
    /// Write(...)  runs ONE transaction on the CLOUD database (the same code that used to run locally),
    ///             and when it commits, replaces the local copy of the stock/transfer tables with the cloud's copy.
    /// If the cloud cannot be reached nothing is changed anywhere.
    /// If the cloud commit works but the local refresh fails, an error says so ("saved, do not repeat").
    ///
    /// The cloud is the owner of these tables, so they must NOT be in CloudSyncService's push list.
    /// </summary>
    public static class CloudStock
    {
        // Parent before child. Deleted in reverse, inserted in this order.
        private static readonly string[] Tables =
        {
            "StockTransfer",
            "StockTransferItem",
            "GodownStock",
            "StoreStock",
            "StockLedger"
        };

        private static readonly object RefreshLock = new object();

        // ───────────── cloud connection ─────────────

        internal static MySqlConnection OpenCloud()
        {
            // Dedicated stock login: asked once (first time it is needed), then stored encrypted on this PC.
            var cs = CloudStockCredentials.GetOrPrompt();
            try
            {
                var c = new MySqlConnection(cs);
                c.Open();
                return c;
            }
            catch (MySqlException ex)
            {
                // 1045 = access denied: the saved password is wrong or was changed. Forget it so the next action asks again.
                if (ex.Number == 1045)
                {
                    CloudStockCredentials.Clear();
                    throw new InvalidOperationException(
                        "The cloud rejected the saved login. It has been cleared; you will be asked again on the next action. Nothing was changed.", ex);
                }
                throw new InvalidOperationException(
                    "Cannot reach the cloud database, so nothing was changed.\n" + ex.Message, ex);
            }
        }

        // ───────────── write on the cloud, then refresh local ─────────────

        public static T Write<T>(Func<MySqlConnection, MySqlTransaction, T> work)
        {
            T result;
            using (var cloud = OpenCloud())
            using (var tx = cloud.BeginTransaction())
            {
                try
                {
                    result = work(cloud, tx);
                    tx.Commit();
                }
                catch
                {
                    try { tx.Rollback(); } catch { /* connection already gone */ }
                    throw;
                }
            }

            RefreshLocal();
            return result;
        }

        public static void Write(Action<MySqlConnection, MySqlTransaction> work) =>
            Write<object>((c, t) => { work(c, t); return null; });

        // ───────────── refresh local from cloud ─────────────

        /// <summary>Use this for a manual "Refresh" / retry. Throws a clear message on failure.</summary>
        public static void RefreshLocal()
        {
            lock (RefreshLock)
            {
                try { PullAll(); }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "The change WAS saved to the cloud, but this computer could not be refreshed from it:\n"
                        + ex.Message
                        + "\n\nDo not repeat the action. Refresh again, or run Sync, to bring this computer up to date.", ex);
                }
            }
        }

        private static void PullAll()
        {
            // 1. Read every table from the cloud first, in one snapshot, so a cloud failure never wipes local data.
            var data = new Dictionary<string, DataTable>();
            using (var cloud = OpenCloud())
            using (var cloudTx = cloud.BeginTransaction(IsolationLevel.RepeatableRead))
            {
                foreach (var t in Tables)
                {
                    var dt = new DataTable();
                    using var cmd = new MySqlCommand($"SELECT * FROM `{t}`", cloud, cloudTx);
                    using var adapter = new MySqlDataAdapter(cmd);
                    adapter.Fill(dt);
                    data[t] = dt;
                }
                cloudTx.Commit();
            }

            // 2. Replace the local tables in one local transaction.
            using var local = new MySqlConnection(DatabaseHelper.ConnectionString);
            local.Open();
            using var tx = local.BeginTransaction();
            try
            {
                Exec(local, tx, "SET FOREIGN_KEY_CHECKS=0");
                foreach (var t in Tables.AsEnumerable().Reverse())
                    Exec(local, tx, $"DELETE FROM `{t}`");
                foreach (var t in Tables)
                    InsertAll(local, tx, t, data[t]);
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
            finally
            {
                Exec(local, null, "SET FOREIGN_KEY_CHECKS=1");
            }
        }

        private static void InsertAll(MySqlConnection conn, MySqlTransaction tx, string table, DataTable dt)
        {
            if (dt.Rows.Count == 0) return;

            // only columns that exist on both sides
            var localCols = GetColumns(conn, tx, table);
            var cols = dt.Columns.Cast<DataColumn>()
                .Select(c => c.ColumnName)
                .Where(c => localCols.Contains(c))
                .ToList();
            if (cols.Count == 0) return;

            var rows = dt.Rows.Cast<DataRow>().ToList();
            const int batchSize = 200;

            for (int start = 0; start < rows.Count; start += batchSize)
            {
                var batch = rows.Skip(start).Take(batchSize).ToList();
                using var cmd = new MySqlCommand { Connection = conn, Transaction = tx };

                var sb = new StringBuilder();
                sb.Append("INSERT INTO `").Append(table).Append("` (");
                sb.Append(string.Join(",", cols.Select(c => $"`{c}`")));
                sb.Append(") VALUES ");

                var groups = new List<string>();
                for (int r = 0; r < batch.Count; r++)
                {
                    var names = new List<string>();
                    for (int c = 0; c < cols.Count; c++)
                    {
                        var p = $"@p{r}_{c}";
                        names.Add(p);
                        cmd.Parameters.AddWithValue(p, batch[r][cols[c]] ?? DBNull.Value);
                    }
                    groups.Add("(" + string.Join(",", names) + ")");
                }
                sb.Append(string.Join(",", groups));
                cmd.CommandText = sb.ToString();
                cmd.ExecuteNonQuery();
            }
        }

        private static HashSet<string> GetColumns(MySqlConnection conn, MySqlTransaction tx, string table)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var cmd = new MySqlCommand(
                @"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @t", conn, tx);
            cmd.Parameters.AddWithValue("@t", table);
            using var r = cmd.ExecuteReader();
            while (r.Read()) set.Add(r.GetString(0));
            return set;
        }

        private static void Exec(MySqlConnection conn, MySqlTransaction tx, string sql)
        {
            using var cmd = new MySqlCommand(sql, conn, tx);
            cmd.ExecuteNonQuery();
        }
    }
}


// =====================================================================================================
// EDITS TO EXISTING FILES
// =====================================================================================================
//
// 1) StockTransferService.cs  -  seven one-word edits. Replace
//        Db.InTransaction(
//    with
//        CloudStock.Write(
//    in: SaveDraft, DeleteDraft, Dispatch, Receive, Reject, CancelTransfer, ResolveShortage.
//    Nothing else in that file changes (LoadHeader / LoadItems / Apply already use the conn + tx they are given).
//
// 2) CloudSyncService.cs  -  delete these five lines from TablesInDependencyOrder:
//        "GodownStock", "StoreStock", "StockTransfer", "StockTransferItem", "StockLedger",
//
// 3) CloudPullService.cs  -  delete the same five from AdditiveTables.
//    Optional, at the end of PullCustomerDataFromCloudAsync (after CommitAsync):
//        await Task.Run(() => CloudStock.RefreshLocal(), cancellationToken);
//
// 4) StockService.cs  -  PostGodownStock: replace everything after the "Quantity cannot be zero / Opening stock /
//    reason" checks (from `var prod = FindProduct(barcode);` to the end of the method) with:
//
//        CloudStock.Write((c, t) =>
//        {
//            long productId;
//            using (var cmd = Db.Cmd(c, t, "SELECT Id FROM MProducts WHERE Barcode = @bc", ("@bc", barcode)))
//            {
//                var o = cmd.ExecuteScalar();
//                if (o == null || o == DBNull.Value)
//                    throw new InvalidOperationException(
//                        $"Barcode '{barcode}' is not in the cloud product list yet. Run Sync first.");
//                productId = Convert.ToInt64(o);
//            }
//            Apply(c, t, LocationType.Godown, godownId, productId, barcode, qty, movement, "Manual", null, user, reason);
//        });
//
// 5) NOT covered yet: PurchaseService's godown mirror (AdjustGodown / SyncGodownOnUpdate). It still writes
//    GodownStock + StockLedger locally, and the next CloudStock refresh will erase those local rows.
// =====================================================================================================
