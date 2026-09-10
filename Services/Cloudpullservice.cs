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
    /// ADDITIVE ONLY: for MCustomer / MCustomerPurchaseMaster /
    /// MCustomerPurchaseDetail / MCustomerPayment / MCustomerReturnMaster /
    /// MCustomerReturnDetail / MPettyCash / MLoginLogout, nothing is deleted.
    /// For each table we read the cloud's rows, work out which Ids don't exist
    /// locally yet, and INSERT only those. Existing local rows - including their
    /// current field values - are left completely untouched.
    ///
    /// CAVEAT: because this is additive-only, a row that gets mutated on another
    /// terminal AFTER it was already pulled once will NOT have that mutation
    /// reflected locally (e.g. MCustomerPurchaseMaster.IsReturned/ReturnDate set
    /// later, MPettyCash.Accepted flipped later, MLoginLogout.logoutTime/
    /// Settlement filled in later). If any of those need to stay in sync, add a
    /// small targeted "upsert just these columns" pass for that specific table -
    /// there's a clearly marked spot below for it.
    ///
    /// PRODUCT QUANTITY IS NEVER OVERWRITTEN FROM THE CLOUD. Instead: whatever
    /// rows were just newly inserted into MCustomerPurchaseDetail /
    /// MCustomerReturnDetail (found via the additive-insert step above - no
    /// separate snapshot/diff needed anymore, since "missing locally" already
    /// *is* "new") are used to compute a per-barcode quantity delta:
    ///   - new MCustomerPurchaseDetail rows  -> Quantity -= sold qty
    ///   - new MCustomerReturnDetail rows    -> Quantity += returned qty
    /// ProductQuantity itself is only ever touched to INSERT a row for a barcode
    /// that doesn't exist locally at all yet (e.g. a brand new product created on
    /// another terminal). Existing local ProductQuantity rows are never
    /// overwritten by that step.
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
        /// Tables that are pulled additively (only rows missing locally, by Id,
        /// are inserted). Listed parent-before-child for FK-friendly insert order.
        /// </summary>
        private static readonly string[] AdditiveTables =
        {
            "MCustomer",
            "MCustomerPurchaseMaster",
            "MCustomerPurchaseDetail",
            "MCustomerPayment",
            "MCustomerReturnMaster",
            "MCustomerReturnDetail",
            "MPettyCash",
            "MLoginLogout",
        };

        private const string PurchaseDetailTable = "MCustomerPurchaseDetail";
        private const string ReturnDetailTable = "MCustomerReturnDetail";

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
                // Off during the additive inserts purely so table order below
                // doesn't have to be perfectly dependency-safe; it already is,
                // but this keeps us from getting bitten later if the list order
                // changes.
                await SetForeignKeyChecksAsync(localConn, transaction, enabled: false, cancellationToken);

                // Rows newly inserted into the two detail tables, captured as we
                // go, so we can compute quantity deltas from exactly those rows -
                // no separate "what's new" comparison needed, since additive
                // insert already tells us that directly.
                List<(long ProductId, double Quantity)> newSaleRows = new();
                List<(long ProductId, double Quantity)> newReturnRows = new();

                foreach (var table in AdditiveTables)
                {
                    progress?.Report($"Pulling {table}...");
                    var inserted = await InsertMissingRowsAsync(
                        cloudConn, localConn, transaction, table, progress, cancellationToken);

                    if (table == PurchaseDetailTable)
                        newSaleRows = ExtractProductQuantityPairs(inserted);
                    else if (table == ReturnDetailTable)
                        newReturnRows = ExtractProductQuantityPairs(inserted);
                }

                await SetForeignKeyChecksAsync(localConn, transaction, enabled: true, cancellationToken);

                // ---- OPTIONAL SPOT: targeted upsert of specific mutable columns ----
                // e.g. sync MCustomerPurchaseMaster.IsReturned/ReturnDate,
                // MPettyCash.Accepted, MLoginLogout.logoutTime/Settlement for rows
                // that already exist locally. Not implemented - additive-only per
                // request. Add a small UpdateMutableColumnsAsync(...) call here per
                // table if/when needed.

                // ---- Quantity adjustment based on newly-inserted sale/return rows ----
                progress?.Report("Working out quantity adjustments from new transactions...");

                var productBarcodeMap = await GetProductBarcodeMapAsync(localConn, transaction, cancellationToken);

                await ApplyQuantityAdjustmentsAsync(
                    localConn, transaction, newSaleRows, productBarcodeMap, sign: -1,
                    label: "sale", progress, cancellationToken);
                await ApplyQuantityAdjustmentsAsync(
                    localConn, transaction, newReturnRows, productBarcodeMap, sign: +1,
                    label: "return", progress, cancellationToken);

                progress?.Report("Pulling missing product quantity rows (new barcodes only)...");
                await InsertMissingProductQuantitiesAsync(cloudConn, localConn, transaction, progress, cancellationToken);

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
        /// Reads every row from the cloud's copy of <paramref name="table"/>,
        /// works out which Ids are not already present locally, and inserts only
        /// those rows (Id included, unchanged) into the local table. Existing
        /// local rows are never touched. Returns the rows that were inserted, as
        /// column-name -> value dictionaries, so callers can pull out whatever
        /// fields they need (e.g. ProductId/Quantity for quantity deltas) without
        /// a second round trip.
        /// </summary>
        private static async Task<List<Dictionary<string, object?>>> InsertMissingRowsAsync(
            MySqlConnection cloudConn,
            MySqlConnection localConn,
            MySqlTransaction localTx,
            string table,
            IProgress<string>? progress,
            CancellationToken cancellationToken)
        {
            var localIds = await GetLocalIdsAsync(localConn, localTx, table, cancellationToken);

            var cloudRows = new DataTable();
            using (var adapter = new MySqlDataAdapter($"SELECT * FROM `{table}`;", cloudConn))
            {
                adapter.Fill(cloudRows);
            }

            var insertedRows = new List<Dictionary<string, object?>>();

            if (cloudRows.Rows.Count == 0)
            {
                progress?.Report($"{table}: nothing in the cloud.");
                return insertedRows;
            }

            var localColumns = await GetLocalColumnsAsync(localConn, localTx, table, cancellationToken);

            var columnsToInsert = cloudRows.Columns
                .Cast<DataColumn>()
                .Select(c => c.ColumnName)
                .Where(c => localColumns.Contains(c))
                .ToList();

            foreach (DataRow row in cloudRows.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var id = Convert.ToInt64(row["Id"]);
                if (localIds.Contains(id))
                    continue; // already have it locally - leave it alone.

                var values = new Dictionary<string, object?>();
                foreach (var col in columnsToInsert)
                {
                    values[col] = row[col] == DBNull.Value ? null : row[col];
                }

                await InsertRowAsync(localConn, localTx, table, values, cancellationToken);
                insertedRows.Add(values);
            }

            progress?.Report($"{table}: {insertedRows.Count} new row(s) inserted, existing rows untouched.");
            return insertedRows;
        }

        private static List<(long ProductId, double Quantity)> ExtractProductQuantityPairs(
            List<Dictionary<string, object?>> rows)
        {
            var result = new List<(long, double)>();

            foreach (var row in rows)
            {
                if (row.TryGetValue("ProductId", out var pidObj) && pidObj != null &&
                    row.TryGetValue("Quantity", out var qtyObj) && qtyObj != null)
                {
                    var productId = Convert.ToInt64(pidObj);
                    var quantity = Convert.ToDouble(qtyObj);
                    result.Add((productId, quantity));
                }
            }

            return result;
        }

        private static async Task<HashSet<long>> GetLocalIdsAsync(
            MySqlConnection conn, MySqlTransaction tx, string table, CancellationToken cancellationToken)
        {
            var ids = new HashSet<long>();

            using var cmd = new MySqlCommand($"SELECT Id FROM `{table}`;", conn, tx);
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                ids.Add(reader.GetInt64(0));

            return ids;
        }

        private static async Task<Dictionary<long, string>> GetProductBarcodeMapAsync(
            MySqlConnection conn, MySqlTransaction tx, CancellationToken cancellationToken)
        {
            var map = new Dictionary<long, string>();

            using var cmd = new MySqlCommand("SELECT Id, Barcode FROM MProducts;", conn, tx);
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                map[reader.GetInt64(0)] = reader.GetString(1);

            return map;
        }

        /// <summary>
        /// Aggregates the given rows by barcode and applies
        /// Quantity = GREATEST(0, Quantity + sign * summedQty) for each barcode,
        /// one UPDATE per barcode. Rows referencing an unknown ProductId, or a
        /// barcode with no local ProductQuantity row, are skipped and reported.
        /// </summary>
        private static async Task ApplyQuantityAdjustmentsAsync(
            MySqlConnection conn, MySqlTransaction tx,
            IEnumerable<(long ProductId, double Quantity)> rows,
            Dictionary<long, string> productBarcodeMap,
            int sign,
            string label,
            IProgress<string>? progress,
            CancellationToken cancellationToken)
        {
            var deltas = new Dictionary<string, double>();
            var skippedUnknownProduct = 0;

            foreach (var (productId, quantity) in rows)
            {
                if (!productBarcodeMap.TryGetValue(productId, out var barcode))
                {
                    skippedUnknownProduct++;
                    continue;
                }

                deltas.TryGetValue(barcode, out var existing);
                deltas[barcode] = existing + sign * quantity;
            }

            if (deltas.Count == 0)
            {
                progress?.Report($"Quantity adjustment ({label}): no new rows.");
                return;
            }

            var adjusted = 0;
            var missingLocally = 0;

            foreach (var (barcode, delta) in deltas)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var cmd = new MySqlCommand(@"
                    UPDATE ProductQuantity
                    SET Quantity = GREATEST(0, Quantity + @delta),
                        ModifiedBy = 'CloudSync',
                        ModifiedDate = CURRENT_TIMESTAMP
                    WHERE Barcode = @barcode;", conn, tx);
                cmd.Parameters.AddWithValue("@delta", delta);
                cmd.Parameters.AddWithValue("@barcode", barcode);

                var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
                if (affected == 0)
                    missingLocally++;
                else
                    adjusted++;
            }

            progress?.Report(
                $"Quantity adjustment ({label}): {adjusted} barcode(s) adjusted from {deltas.Count} affected barcode(s)"
                + (missingLocally > 0 ? $", {missingLocally} skipped (no local ProductQuantity row)" : "")
                + (skippedUnknownProduct > 0 ? $", {skippedUnknownProduct} row(s) skipped (unknown ProductId)" : "")
                + ".");
        }

        /// <summary>
        /// Inserts a ProductQuantity row for any cloud barcode that doesn't exist
        /// locally yet. Existing local rows are left completely untouched.
        /// </summary>
        private static async Task InsertMissingProductQuantitiesAsync(
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
                progress?.Report($"{table}: nothing in the cloud to check.");
                return;
            }

            var localBarcodes = await GetLocalBarcodesAsync(localConn, localTx, cancellationToken);

            var inserted = 0;

            foreach (DataRow row in cloudRows.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (row["Barcode"] == DBNull.Value)
                    continue;

                var barcode = row["Barcode"].ToString()!;
                if (localBarcodes.Contains(barcode))
                    continue; // already exists locally - leave its quantity alone.

                var quantity = row["Quantity"] == DBNull.Value ? 0L : Convert.ToInt64(row["Quantity"]);
                var minSelling = row["MinimumSellingQuantity"] == DBNull.Value
                    ? 1L
                    : Convert.ToInt64(row["MinimumSellingQuantity"]);
                var productCode = row.Table.Columns.Contains("ProductCode") && row["ProductCode"] != DBNull.Value
                    ? row["ProductCode"].ToString()
                    : null;

                await InsertProductQuantityAsync(
                    localConn, localTx, barcode, productCode, minSelling, quantity, cancellationToken);
                inserted++;
            }

            progress?.Report($"{table}: {inserted} new barcode(s) added (no existing rows were modified).");
        }

        private static async Task<HashSet<string>> GetLocalBarcodesAsync(
            MySqlConnection conn, MySqlTransaction tx, CancellationToken cancellationToken)
        {
            var barcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var cmd = new MySqlCommand("SELECT Barcode FROM ProductQuantity;", conn, tx);
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                barcodes.Add(reader.GetString(0));

            return barcodes;
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