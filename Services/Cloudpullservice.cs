using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace MyWPFCRUDApp.Services
{
    /// <summary>
    /// Pulls customer, customer-purchase, customer-return, payment, petty-cash,
    /// login/logout, and product-quantity rows from the cloud database into the
    /// local database.
    ///
    /// For MCustomer / MCustomerPurchaseMaster / MCustomerPurchaseDetail /
    /// MCustomerPayment / MCustomerReturnMaster / MCustomerReturnDetail /
    /// MPettyCash / MLoginLogout: this is a FULL REPLACE. Every row currently in
    /// the local table is deleted, and every row from the cloud table is copied
    /// in as-is (including the cloud's own Id values - they are NOT remapped).
    /// The cloud is treated as the sole source of truth for these tables.
    ///
    /// Because Id values are carried over unchanged, foreign keys between these
    /// tables (e.g. MCustomerPurchaseDetail.PurchaseMasterId) line up naturally
    /// and need no translation. Foreign key checks are disabled for the duration
    /// of the pull so tables can be cleared/reloaded without worrying about
    /// delete/insert ordering across them.
    ///
    /// For ProductQuantity: rows are matched by the natural key Barcode instead,
    /// and this table is NOT wiped. If a barcode doesn't exist locally yet, the
    /// row is inserted. If it already exists locally, its Quantity (and
    /// MinimumSellingQuantity) is UPDATED to the cloud's value.
    ///
    /// NOT pulled here (sync direction wasn't established for these, so they're
    /// left untouched to avoid guessing wrong): MCounterNew, MCounterUser,
    /// MPaymentMethod, MUser, MUserType.
    ///
    /// Usage:
    ///   await CloudPullService.PullCustomerDataFromCloudAsync(progress);
    /// </summary>
    public static class CloudPullService
    {
        /// <summary>
        /// Tables that are fully replaced from the cloud (deleted locally, then
        /// re-copied verbatim, Id included). Order matters only in that it's a
        /// sensible "parent before child" read/insert order for reporting
        /// purposes - actual FK enforcement is disabled during the pull, so the
        /// order does not need to satisfy dependency constraints.
        /// </summary>
        private static readonly (string Table, string IdColumn)[] FullReplaceTables = new[]
        {
            ("MCustomer", "Id"),
            ("MCustomerPurchaseMaster", "Id"),
            ("MCustomerPurchaseDetail", "Id"),
            ("MCustomerPayment", "Id"),
            ("MCustomerReturnMaster", "Id"),
            ("MCustomerReturnDetail", "Id"),
            ("MPettyCash", "Id"),
            ("MLoginLogout", "Id"),
        };

        public static async Task PullCustomerDataFromCloudAsync(
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(CloudSyncService.CloudConnectionString))
                throw new InvalidOperationException(
                    "CloudSyncService.CloudConnectionString has not been set.");

            using var localConn = new MySqlConnection(DatabaseHelper.ConnectionString);
            using var cloudConn = new MySqlConnection(CloudSyncService.CloudConnectionString);

            await localConn.OpenAsync(cancellationToken);
            await cloudConn.OpenAsync(cancellationToken);

            using var transaction = await localConn.BeginTransactionAsync(cancellationToken);

            try
            {
                // Disabled so tables can be cleared/reloaded in any order without
                // tripping FK constraints between them (Ids are carried over
                // unchanged from the cloud, so relationships stay intact once all
                // tables are reloaded).
                await SetForeignKeyChecksAsync(localConn, transaction, enabled: false, cancellationToken);

                foreach (var (table, idColumn) in FullReplaceTables)
                {
                    progress?.Report($"Pulling {table}...");
                    await PullTableFullReplaceAsync(
                        cloudConn, localConn, transaction, table, idColumn, progress, cancellationToken);
                }

                await SetForeignKeyChecksAsync(localConn, transaction, enabled: true, cancellationToken);

                progress?.Report("Pulling product quantities...");
                await PullProductQuantitiesAsync(cloudConn, localConn, transaction, progress, cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                progress?.Report("Pull complete.");
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        private static async Task SetForeignKeyChecksAsync(
            MySqlConnection conn, MySqlTransaction tx, bool enabled, CancellationToken cancellationToken)
        {
            using var cmd = new MySqlCommand($"SET FOREIGN_KEY_CHECKS={(enabled ? 1 : 0)};", conn, tx);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>
        /// Upserts ProductQuantity by Barcode: inserts any barcode from the cloud
        /// that doesn't exist locally yet, and updates Quantity /
        /// MinimumSellingQuantity for any barcode that already exists locally so it
        /// matches the cloud's current value. Does not touch ProductCode or Id.
        /// </summary>
        private static async Task PullProductQuantitiesAsync(
            MySqlConnection cloudConn,
            MySqlConnection localConn,
            MySqlTransaction localTx,
            IProgress<string>? progress,
            CancellationToken cancellationToken)
        {
            const string table = "ProductQuantity";

            var cloudRows = new DataTable();
            using (var adapter = new MySqlDataAdapter($"SELECT * FROM `{table}`;", cloudConn))
            {
                adapter.Fill(cloudRows);
            }

            if (cloudRows.Rows.Count == 0)
            {
                progress?.Report($"{table}: nothing in the cloud to pull.");
                return;
            }

            int inserted = 0, updated = 0, unchanged = 0;

            foreach (DataRow row in cloudRows.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (row["Barcode"] == DBNull.Value)
                    continue;

                var barcode = row["Barcode"].ToString()!;
                var quantity = row["Quantity"] == DBNull.Value ? 0L : Convert.ToInt64(row["Quantity"]);
                var minSelling = row["MinimumSellingQuantity"] == DBNull.Value
                    ? 1L
                    : Convert.ToInt64(row["MinimumSellingQuantity"]);
                var productCode = row.Table.Columns.Contains("ProductCode") && row["ProductCode"] != DBNull.Value
                    ? row["ProductCode"].ToString()
                    : null;

                var existing = await TryGetLocalProductQuantityAsync(localConn, localTx, barcode, cancellationToken);

                if (existing == null)
                {
                    await InsertProductQuantityAsync(
                        localConn, localTx, barcode, productCode, minSelling, quantity, cancellationToken);
                    inserted++;
                }
                else if (existing.Value.Quantity != quantity || existing.Value.MinimumSellingQuantity != minSelling)
                {
                    await UpdateProductQuantityAsync(
                        localConn, localTx, barcode, minSelling, quantity, cancellationToken);
                    updated++;
                }
                else
                {
                    unchanged++;
                }
            }

            progress?.Report(
                $"{table}: {inserted} new barcode(s) added, {updated} updated, {unchanged} already up to date.");
        }

        private static async Task<(long Quantity, long MinimumSellingQuantity)?> TryGetLocalProductQuantityAsync(
            MySqlConnection conn, MySqlTransaction tx, string barcode, CancellationToken cancellationToken)
        {
            const string sql = "SELECT Quantity, MinimumSellingQuantity FROM ProductQuantity WHERE Barcode = @barcode LIMIT 1;";
            using var cmd = new MySqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@barcode", barcode);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return (reader.GetInt64(0), reader.GetInt64(1));
        }

        private static async Task InsertProductQuantityAsync(
            MySqlConnection conn, MySqlTransaction tx, string barcode, string? productCode,
            long minSelling, long quantity, CancellationToken cancellationToken)
        {
            const string sql = @"
                INSERT INTO ProductQuantity (ProductCode, Barcode, MinimumSellingQuantity, Quantity)
                VALUES (@productCode, @barcode, @minSelling, @quantity);";

            using var cmd = new MySqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@productCode", (object?)productCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@barcode", barcode);
            cmd.Parameters.AddWithValue("@minSelling", minSelling);
            cmd.Parameters.AddWithValue("@quantity", quantity);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task UpdateProductQuantityAsync(
            MySqlConnection conn, MySqlTransaction tx, string barcode,
            long minSelling, long quantity, CancellationToken cancellationToken)
        {
            const string sql = @"
                UPDATE ProductQuantity
                SET Quantity = @quantity,
                    MinimumSellingQuantity = @minSelling,
                    ModifiedBy = 'CloudSync',
                    ModifiedDate = CURRENT_TIMESTAMP
                WHERE Barcode = @barcode;";

            using var cmd = new MySqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@quantity", quantity);
            cmd.Parameters.AddWithValue("@minSelling", minSelling);
            cmd.Parameters.AddWithValue("@barcode", barcode);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>
        /// Deletes every row currently in the local <paramref name="table"/> and
        /// replaces it with an exact copy of the cloud table's rows, including the
        /// cloud's own <paramref name="idColumn"/> values (no remapping, no
        /// tracking - the cloud is the source of truth for this table).
        /// </summary>
        private static async Task PullTableFullReplaceAsync(
            MySqlConnection cloudConn,
            MySqlConnection localConn,
            MySqlTransaction localTx,
            string table,
            string idColumn,
            IProgress<string>? progress,
            CancellationToken cancellationToken)
        {
            await DeleteAllRowsAsync(localConn, localTx, table, cancellationToken);

            var cloudRows = new DataTable();
            using (var adapter = new MySqlDataAdapter($"SELECT * FROM `{table}`;", cloudConn))
            {
                adapter.Fill(cloudRows);
            }

            if (cloudRows.Rows.Count == 0)
            {
                progress?.Report($"{table}: local table cleared, nothing in the cloud to copy.");
                return;
            }

            var localColumns = await GetLocalColumnsAsync(localConn, localTx, table, cancellationToken);

            var columnsToInsert = cloudRows.Columns
                .Cast<DataColumn>()
                .Select(c => c.ColumnName)
                .Where(c => localColumns.Contains(c)) // idColumn included - Ids are carried over as-is.
                .ToList();

            int inserted = 0;

            foreach (DataRow row in cloudRows.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var values = new Dictionary<string, object?>();
                foreach (var col in columnsToInsert)
                {
                    values[col] = row[col] == DBNull.Value ? null : row[col];
                }

                await InsertRowAsync(localConn, localTx, table, values, cancellationToken);
                inserted++;
            }

            progress?.Report($"{table}: local table cleared, {inserted} row(s) copied from cloud.");
        }

        private static async Task DeleteAllRowsAsync(
            MySqlConnection conn, MySqlTransaction tx, string table, CancellationToken cancellationToken)
        {
            using var cmd = new MySqlCommand($"DELETE FROM `{table}`;", conn, tx);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task<HashSet<string>> GetLocalColumnsAsync(
            MySqlConnection conn, MySqlTransaction tx, string table, CancellationToken cancellationToken)
        {
            const string sql = @"
                SELECT COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = @table;";

            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var cmd = new MySqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@table", table);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(reader.GetString(0));
            }

            return result;
        }

        private static async Task InsertRowAsync(
            MySqlConnection conn, MySqlTransaction tx, string table,
            Dictionary<string, object?> values, CancellationToken cancellationToken)
        {
            var columns = values.Keys.ToList();

            var sb = new StringBuilder();
            sb.Append("INSERT INTO `").Append(table).Append("` (");
            sb.Append(string.Join(",", columns.Select(c => $"`{c}`")));
            sb.Append(") VALUES (");
            sb.Append(string.Join(",", columns.Select(c => $"@{c}")));
            sb.Append(");");

            using var cmd = new MySqlCommand(sb.ToString(), conn, tx);
            foreach (var col in columns)
                cmd.Parameters.AddWithValue($"@{col}", values[col] ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}