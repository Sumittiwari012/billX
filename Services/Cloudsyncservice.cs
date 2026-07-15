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
    /// Pushes the entire contents of the local MySQL database up to the cloud MySQL
    /// database, replacing whatever is currently in the cloud tables.
    ///
    /// Usage:
    ///   CloudSyncService.CloudConnectionString = "server=...;database=...;user=...;password=...;";
    ///   await CloudSyncService.SyncLocalToCloudAsync(progress);
    /// </summary>
    public static class CloudSyncService
    {
        /// <summary>
        /// Connection string for the cloud database. Set this at startup (e.g. from
        /// app settings / a config file) - do NOT hardcode credentials in source.
        /// </summary>
        public static string CloudConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Tables in parent-before-child order. Used for both the delete pass (reversed)
        /// and the insert pass (as-is). FK checks are disabled during the sync, so this
        /// order is mainly for readability/debuggability rather than strict correctness.
        ///
        /// IMPORTANT: trim this list down to only the tables that actually exist in your
        /// cloud database. Trying to sync a table that isn't there yet will throw.
        /// </summary>
        private static readonly string[] TablesInDependencyOrder =
        {
            // Level 0 - no FK dependencies
            "MCategory",
            "MUnit",

            // Level 1 - depend on level 0
            "MSubCategory",   // -> MCategory

            // Level 2 - depend on level 1
            "MProducts",      // -> MCategory, MSubCategory, MUnit

            // Level 3 - depend on level 2
            "ProductQuantity",// -> MProducts (Barcode)
        };

        /// <summary>
        /// Wipes every table in <see cref="TablesInDependencyOrder"/> on the cloud
        /// database and re-populates it from the local database, inside a single
        /// transaction. If anything fails, everything is rolled back.
        /// </summary>
        public static async Task SyncLocalToCloudAsync(
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(CloudConnectionString))
                throw new InvalidOperationException(
                    "CloudSyncService.CloudConnectionString has not been set.");

            using var localConn = new MySqlConnection(DatabaseHelper.ConnectionString);
            using var cloudConn = new MySqlConnection(CloudConnectionString);

            await localConn.OpenAsync(cancellationToken);
            await cloudConn.OpenAsync(cancellationToken);

            // Disable FK checks on the cloud side for the duration of the sync so we
            // don't have to worry about delete/insert ordering edge cases.
            await ExecuteNonQueryAsync(cloudConn, null, "SET FOREIGN_KEY_CHECKS=0;", cancellationToken);

            using var transaction = await cloudConn.BeginTransactionAsync(cancellationToken);

            try
            {
                // 1) Clear cloud tables (children first, so it reads nicely even
                //    though FK checks are off).
                foreach (var table in TablesInDependencyOrder.Reverse())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report($"Clearing cloud table {table}...");
                    await ExecuteNonQueryAsync(cloudConn, transaction, $"DELETE FROM `{table}`;", cancellationToken);
                }

                // 2) Copy local data across (parents first).
                foreach (var table in TablesInDependencyOrder)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report($"Copying {table}...");
                    var rowCount = await CopyTableAsync(localConn, cloudConn, transaction, table, progress, cancellationToken);
                    progress?.Report($"Copied {rowCount} row(s) into {table}.");
                }

                await transaction.CommitAsync(cancellationToken);
                progress?.Report("Sync complete.");
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
            finally
            {
                await ExecuteNonQueryAsync(cloudConn, null, "SET FOREIGN_KEY_CHECKS=1;", cancellationToken);
            }
        }

        /// <summary>
        /// Returns the set of column names that actually exist on <paramref name="table"/>
        /// in whichever database <paramref name="conn"/> is connected to. Column-name
        /// comparisons are case-insensitive (matching MySQL's own behavior for identifiers).
        /// </summary>
        private static async Task<HashSet<string>> GetTableColumnsAsync(
            MySqlConnection conn,
            string table,
            CancellationToken cancellationToken)
        {
            const string sql = @"
                SELECT COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = @table;";

            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@table", table);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(reader.GetString(0));
            }

            return result;
        }

        /// <summary>
        /// Reads every row of <paramref name="table"/> from the local database and
        /// inserts it into the cloud database, but ONLY for columns that exist on
        /// both sides. Columns present only locally are dropped (reported via
        /// <paramref name="progress"/>); columns present only in the cloud table
        /// are left at their defaults. Explicit Id values are preserved so that
        /// foreign-key relationships stay intact. Batches rows to keep each SQL
        /// statement a manageable size.
        /// </summary>
        private static async Task<int> CopyTableAsync(
            MySqlConnection localConn,
            MySqlConnection cloudConn,
            MySqlTransaction transaction,
            string table,
            IProgress<string>? progress,
            CancellationToken cancellationToken)
        {
            var dt = new DataTable();
            using (var adapter = new MySqlDataAdapter($"SELECT * FROM `{table}`;", localConn))
            {
                adapter.Fill(dt);
            }

            if (dt.Rows.Count == 0)
                return 0;

            var localColumns = dt.Columns
                .Cast<DataColumn>()
                .Select(c => c.ColumnName)
                .ToList();

            var cloudColumns = await GetTableColumnsAsync(cloudConn, table, cancellationToken);

            // Only keep columns that exist on both sides. Preserve local column
            // name casing for the INSERT - MySQL matches column names case-insensitively.
            var commonColumns = localColumns
                .Where(c => cloudColumns.Contains(c))
                .ToList();

            var skippedColumns = localColumns.Except(commonColumns).ToList();
            if (skippedColumns.Count > 0)
            {
                progress?.Report(
                    $"{table}: skipping local-only column(s) not present in cloud: {string.Join(", ", skippedColumns)}");
            }

            if (commonColumns.Count == 0)
            {
                progress?.Report($"{table}: no matching columns found between local and cloud - skipped.");
                return 0;
            }

            const int batchSize = 200; // rows per INSERT statement
            var allRows = dt.Rows.Cast<DataRow>().ToList();

            for (int start = 0; start < allRows.Count; start += batchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batch = allRows.Skip(start).Take(batchSize).ToList();

                using var cmd = new MySqlCommand
                {
                    Connection = cloudConn,
                    Transaction = transaction
                };

                var sb = new StringBuilder();
                sb.Append("INSERT INTO `").Append(table).Append("` (");
                sb.Append(string.Join(",", commonColumns.Select(c => $"`{c}`")));
                sb.Append(") VALUES ");

                var rowValueGroups = new List<string>();
                for (int r = 0; r < batch.Count; r++)
                {
                    var paramNames = new List<string>();
                    for (int c = 0; c < commonColumns.Count; c++)
                    {
                        var paramName = $"@p{r}_{c}";
                        paramNames.Add(paramName);
                        var value = batch[r][commonColumns[c]];
                        cmd.Parameters.AddWithValue(paramName, value ?? DBNull.Value);
                    }
                    rowValueGroups.Add("(" + string.Join(",", paramNames) + ")");
                }

                sb.Append(string.Join(",", rowValueGroups));
                cmd.CommandText = sb.ToString();

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            return allRows.Count;
        }

        private static async Task ExecuteNonQueryAsync(
            MySqlConnection conn,
            MySqlTransaction? transaction,
            string sql,
            CancellationToken cancellationToken)
        {
            using var cmd = new MySqlCommand(sql, conn, transaction);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}