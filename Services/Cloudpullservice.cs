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
    /// Pulls customer, customer-purchase, payment, and product-quantity rows that
    /// exist in the cloud database into the local database.
    ///
    /// For MCustomer / MCustomerPurchaseMaster / MCustomerPurchaseDetail /
    /// MCustomerPayment: rows are pulled by Id, tracked via __CloudImportTracking,
    /// and only ever ADDED locally - existing local rows are never changed.
    ///
    /// For ProductQuantity: rows are matched by the natural key Barcode instead.
    /// If a barcode doesn't exist locally yet, the row is inserted. If it already
    /// exists locally, its Quantity (and MinimumSellingQuantity) is UPDATED to the
    /// cloud's value - this is the one table where the cloud is treated as the
    /// source of truth for the value, not just for new rows.
    ///
    /// IMPORTANT ASSUMPTION: MCustomerPurchaseDetail.ProductId is trusted as-is
    /// (not remapped) because product master data (MProducts, MCategory, MUnit,
    /// MSubCategory) is pushed local -> cloud with explicit, matching Id values via
    /// CloudSyncService. If that ever changes, ProductId matching here would need
    /// to be redone via a natural key (e.g. Barcode) instead.
    ///
    /// Usage:
    ///   await CloudPullService.PullCustomerDataFromCloudAsync(progress);
    /// </summary>
    public static class CloudPullService
    {
        private const string TrackingTable = "__CloudImportTracking";

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

            await EnsureTrackingTableAsync(localConn, cancellationToken);

            using var transaction = await localConn.BeginTransactionAsync(cancellationToken);

            try
            {
                progress?.Report("Pulling customers...");
                var customerIdMap = await PullTableAsync(
                    cloudConn, localConn, transaction,
                    table: "MCustomer",
                    idColumn: "Id",
                    remapColumns: null,
                    progress, cancellationToken);

                progress?.Report("Pulling customer purchase invoices...");
                var purchaseMasterIdMap = await PullTableAsync(
                    cloudConn, localConn, transaction,
                    table: "MCustomerPurchaseMaster",
                    idColumn: "Id",
                    remapColumns: new Dictionary<string, IReadOnlyDictionary<long, long>>
                    {
                        ["CustomerId"] = customerIdMap
                    },
                    progress, cancellationToken);

                progress?.Report("Pulling customer purchase line items...");
                await PullTableAsync(
                    cloudConn, localConn, transaction,
                    table: "MCustomerPurchaseDetail",
                    idColumn: "Id",
                    remapColumns: new Dictionary<string, IReadOnlyDictionary<long, long>>
                    {
                        ["PurchaseMasterId"] = purchaseMasterIdMap
                    },
                    progress, cancellationToken);

                progress?.Report("Pulling customer payments...");
                await PullTableAsync(
                    cloudConn, localConn, transaction,
                    table: "MCustomerPayment",
                    idColumn: "Id",
                    remapColumns: new Dictionary<string, IReadOnlyDictionary<long, long>>
                    {
                        ["CustomerId"] = customerIdMap
                    },
                    progress, cancellationToken);

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
        /// Pulls every row of <paramref name="table"/> from the cloud database.
        /// Rows already present locally (per the tracking table) are skipped.
        /// New rows are inserted locally with a fresh auto-increment Id, with any
        /// foreign-key columns listed in <paramref name="remapColumns"/> translated
        /// from their cloud Id to the corresponding local Id. Returns a complete
        /// cloudId -> localId map for the whole table (covering both rows that were
        /// already tracked and rows inserted just now), for use by dependent tables.
        /// </summary>
        private static async Task<Dictionary<long, long>> PullTableAsync(
            MySqlConnection cloudConn,
            MySqlConnection localConn,
            MySqlTransaction localTx,
            string table,
            string idColumn,
            IReadOnlyDictionary<string, IReadOnlyDictionary<long, long>>? remapColumns,
            IProgress<string>? progress,
            CancellationToken cancellationToken)
        {
            var idMap = new Dictionary<long, long>();

            var cloudRows = new DataTable();
            using (var adapter = new MySqlDataAdapter($"SELECT * FROM `{table}`;", cloudConn))
            {
                adapter.Fill(cloudRows);
            }

            if (cloudRows.Rows.Count == 0)
            {
                progress?.Report($"{table}: nothing in the cloud to pull.");
                return idMap;
            }

            var localColumns = await GetLocalColumnsAsync(localConn, localTx, table, cancellationToken);

            int inserted = 0, alreadyPresent = 0, skippedOrphan = 0;

            foreach (DataRow row in cloudRows.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var cloudId = Convert.ToInt64(row[idColumn]);

                var existingLocalId = await TryGetTrackedLocalIdAsync(localConn, localTx, table, cloudId, cancellationToken);
                if (existingLocalId.HasValue)
                {
                    idMap[cloudId] = existingLocalId.Value;
                    alreadyPresent++;
                    continue;
                }

                var columnsToInsert = cloudRows.Columns
                    .Cast<DataColumn>()
                    .Select(c => c.ColumnName)
                    .Where(c => localColumns.Contains(c) && !c.Equals(idColumn, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var values = new Dictionary<string, object?>();
                bool orphan = false;

                foreach (var col in columnsToInsert)
                {
                    object? value = row[col] == DBNull.Value ? null : row[col];

                    if (remapColumns != null && remapColumns.TryGetValue(col, out var map))
                    {
                        if (value == null)
                        {
                            // Nullable FK left null - carry over as null.
                            values[col] = null;
                            continue;
                        }

                        var rawCloudFk = Convert.ToInt64(value);
                        if (!map.TryGetValue(rawCloudFk, out var mappedLocalFk))
                        {
                            orphan = true;
                            break;
                        }
                        value = mappedLocalFk;
                    }

                    values[col] = value;
                }

                if (orphan)
                {
                    skippedOrphan++;
                    progress?.Report($"{table}: skipped cloud row Id {cloudId} - referenced parent row not found locally.");
                    continue;
                }

                var newLocalId = await InsertRowAndGetIdAsync(localConn, localTx, table, values, cancellationToken);
                await RecordTrackingAsync(localConn, localTx, table, cloudId, newLocalId, cancellationToken);

                idMap[cloudId] = newLocalId;
                inserted++;
            }

            progress?.Report(
                $"{table}: {inserted} new row(s) added, {alreadyPresent} already present, {skippedOrphan} skipped (missing parent).");

            return idMap;
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

        private static async Task<long?> TryGetTrackedLocalIdAsync(
            MySqlConnection conn, MySqlTransaction tx, string table, long cloudId, CancellationToken cancellationToken)
        {
            var sql = $"SELECT LocalId FROM `{TrackingTable}` WHERE TableName=@t AND CloudId=@c;";
            using var cmd = new MySqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@t", table);
            cmd.Parameters.AddWithValue("@c", cloudId);

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return result == null ? (long?)null : Convert.ToInt64(result);
        }

        private static async Task<long> InsertRowAndGetIdAsync(
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

            using (var cmd = new MySqlCommand(sb.ToString(), conn, tx))
            {
                foreach (var col in columns)
                    cmd.Parameters.AddWithValue($"@{col}", values[col] ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            using var idCmd = new MySqlCommand("SELECT LAST_INSERT_ID();", conn, tx);
            var idResult = await idCmd.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(idResult);
        }

        private static async Task RecordTrackingAsync(
            MySqlConnection conn, MySqlTransaction tx, string table, long cloudId, long localId,
            CancellationToken cancellationToken)
        {
            var sql = $"INSERT INTO `{TrackingTable}` (TableName, CloudId, LocalId) VALUES (@t, @c, @l);";
            using var cmd = new MySqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@t", table);
            cmd.Parameters.AddWithValue("@c", cloudId);
            cmd.Parameters.AddWithValue("@l", localId);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task EnsureTrackingTableAsync(MySqlConnection conn, CancellationToken cancellationToken)
        {
            const string sql = @"
                CREATE TABLE IF NOT EXISTS __CloudImportTracking (
                    TableName    VARCHAR(100) NOT NULL,
                    CloudId      BIGINT NOT NULL,
                    LocalId      BIGINT NOT NULL,
                    ImportedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (TableName, CloudId)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            using var cmd = new MySqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}