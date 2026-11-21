using MySql.Data.MySqlClient;
using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;

namespace MyWPFCRUDApp.Services
{
    internal static class Db
    {
        public static MySqlConnection Open()
        {
            var c = new MySqlConnection(DatabaseHelper.ConnectionString);
            c.Open();
            return c;
        }

        public static MySqlCommand Cmd(MySqlConnection c, MySqlTransaction t, string sql, params (string Name, object Value)[] p)
        {
            var cmd = new MySqlCommand(sql, c, t);
            foreach (var (n, v) in p) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
            return cmd;
        }

        public static List<T> Query<T>(string sql, Func<MySqlDataReader, T> map, params (string Name, object Value)[] p)
        {
            using var conn = Open();
            using var cmd = Cmd(conn, null, sql, p);
            using var r = cmd.ExecuteReader();
            var list = new List<T>();
            while (r.Read()) list.Add(map(r));
            return list;
        }

        public static T InTransaction<T>(Func<MySqlConnection, MySqlTransaction, T> work)
        {
            using var conn = Open();
            using var tx = conn.BeginTransaction();
            try
            {
                var result = work(conn, tx);
                tx.Commit();
                return result;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public static void InTransaction(Action<MySqlConnection, MySqlTransaction> work)
            => InTransaction<object>((c, t) => { work(c, t); return null; });
    }

    internal static class Rd
    {
        public static string Str(MySqlDataReader r, string c) { var o = r[c]; return o == DBNull.Value ? null : Convert.ToString(o); }
        public static long Lng(MySqlDataReader r, string c) => Convert.ToInt64(r[c]);
        public static long? NLng(MySqlDataReader r, string c) { var o = r[c]; return o == DBNull.Value ? (long?)null : Convert.ToInt64(o); }
        public static decimal Dec(MySqlDataReader r, string c) => Convert.ToDecimal(r[c]);
        public static decimal? NDec(MySqlDataReader r, string c) { var o = r[c]; return o == DBNull.Value ? (decimal?)null : Convert.ToDecimal(o); }
        public static DateTime Date(MySqlDataReader r, string c) => Convert.ToDateTime(r[c]);
        public static DateTime? NDate(MySqlDataReader r, string c) { var o = r[c]; return o == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(o); }
        public static bool Bool(MySqlDataReader r, string c) => Convert.ToInt32(r[c]) != 0;
    }

    /// <summary>
    /// The only place that changes stock quantities. Every change writes a ledger row
    /// in the same transaction. There is deliberately no "set quantity" method.
    /// </summary>
    public static class StockService
    {
        internal static (string Table, string Col, string LocTable, string LocName) Map(string type) => type switch
        {
            LocationType.Godown => ("GodownStock", "GodownId", "MGodown", "GodownName"),
            LocationType.Store => ("StoreStock", "StoreId", "MStore", "StoreName"),
            _ => throw new ArgumentException("Unknown location type: " + type)
        };

        /// <summary>Increase (delta &gt; 0) or decrease (delta &lt; 0) stock and write the ledger row.</summary>
        internal static void Apply(MySqlConnection conn, MySqlTransaction tx, string locType, long locId,
            long productId, string barcode, decimal delta, string movement, string refType, long? refId,
            string user, string remarks)
        {
            if (delta == 0) return;
            var m = Map(locType);

            if (delta > 0)
            {
                using var up = Db.Cmd(conn, tx,
                    $@"INSERT INTO {m.Table} ({m.Col}, ProductId, Barcode, Quantity, CreatedBy, ModifiedBy)
                       VALUES (@loc, @pid, @bc, @q, @u, @u)
                       ON DUPLICATE KEY UPDATE Quantity = Quantity + @q, ModifiedBy = @u",
                    ("@loc", locId), ("@pid", productId), ("@bc", barcode), ("@q", delta), ("@u", user));
                up.ExecuteNonQuery();
            }
            else
            {
                using var down = Db.Cmd(conn, tx,
                    $@"UPDATE {m.Table} SET Quantity = Quantity - @q, ModifiedBy = @u
                       WHERE {m.Col} = @loc AND Barcode = @bc AND Quantity >= @q",
                    ("@loc", locId), ("@bc", barcode), ("@q", -delta), ("@u", user));
                if (down.ExecuteNonQuery() == 0)
                    throw new InvalidOperationException($"Insufficient stock for barcode {barcode} at {locType} #{locId}.");
            }

            decimal balance;
            using (var b = Db.Cmd(conn, tx, $"SELECT Quantity FROM {m.Table} WHERE {m.Col} = @loc AND Barcode = @bc",
                ("@loc", locId), ("@bc", barcode)))
                balance = Convert.ToDecimal(b.ExecuteScalar());

            using var led = Db.Cmd(conn, tx,
                @"INSERT INTO StockLedger (ProductId, Barcode, LocationType, LocationId, QtyChange, BalanceAfter,
                                           MovementType, ReferenceType, ReferenceId, Remarks, CreatedBy)
                  VALUES (@pid, @bc, @lt, @loc, @chg, @bal, @mv, @rt, @rid, @rem, @u)",
                ("@pid", productId), ("@bc", barcode), ("@lt", locType), ("@loc", locId), ("@chg", delta),
                ("@bal", balance), ("@mv", movement), ("@rt", refType), ("@rid", refId), ("@rem", remarks), ("@u", user));
            led.ExecuteNonQuery();
        }

        public static (long Id, string Name)? FindProduct(string barcode)
        {
            var rows = Db.Query("SELECT Id, ProductName FROM MProducts WHERE Barcode = @bc",
                r => (Rd.Lng(r, "Id"), Rd.Str(r, "ProductName") ?? ""), ("@bc", barcode?.Trim()));
            return rows.Count == 0 ? ((long, string)?)null : rows[0];
        }

        public static decimal GetQuantity(string locType, long locId, string barcode)
        {
            var m = Map(locType);
            using var conn = Db.Open();
            using var cmd = Db.Cmd(conn, null, $"SELECT Quantity FROM {m.Table} WHERE {m.Col} = @loc AND Barcode = @bc",
                ("@loc", locId), ("@bc", barcode));
            var o = cmd.ExecuteScalar();
            return o == null || o == DBNull.Value ? 0 : Convert.ToDecimal(o);
        }

        public static List<StockRow> GetStock(string locType, long? locId, string search)
        {
            var m = Map(locType);
            search = search?.Trim() ?? "";
            return Db.Query($@"
                SELECT s.Id, s.{m.Col} AS LocationId, l.{m.LocName} AS LocationName, s.ProductId, s.Barcode,
                       p.ProductName, s.Quantity, s.ModifiedDate
                FROM {m.Table} s
                JOIN MProducts p ON p.Id = s.ProductId
                JOIN {m.LocTable} l ON l.Id = s.{m.Col}
                WHERE (@loc IS NULL OR s.{m.Col} = @loc)
                  AND (@s = '' OR s.Barcode LIKE @like OR p.ProductName LIKE @like)
                ORDER BY p.ProductName, s.Barcode",
                r => new StockRow
                {
                    Id = Rd.Lng(r, "Id"),
                    LocationId = Rd.Lng(r, "LocationId"),
                    LocationName = Rd.Str(r, "LocationName"),
                    ProductId = Rd.Lng(r, "ProductId"),
                    Barcode = Rd.Str(r, "Barcode"),
                    ProductName = Rd.Str(r, "ProductName"),
                    Quantity = Rd.Dec(r, "Quantity"),
                    ModifiedDate = Rd.Date(r, "ModifiedDate")
                },
                ("@loc", locId), ("@s", search), ("@like", "%" + search + "%"));
        }

        /// <summary>
        /// Manual posting into a godown (opening stock or a reasoned adjustment).
        /// Positive = in, negative = out (adjustment only). Purchase entry will call Apply-based
        /// methods later; nothing here is connected to purchases yet.
        /// </summary>
        public static void PostGodownStock(long godownId, string barcode, decimal qty, string movement, string reason, string user)
        {
            barcode = barcode?.Trim();
            if (string.IsNullOrEmpty(barcode)) throw new InvalidOperationException("Barcode is required.");
            if (qty == 0) throw new InvalidOperationException("Quantity cannot be zero.");
            if (movement == MovementType.OpeningStock && qty < 0) throw new InvalidOperationException("Opening stock must be positive.");
            if (movement == MovementType.Adjustment && string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("A reason is required for adjustments.");

            var prod = FindProduct(barcode);
            if (prod == null) throw new InvalidOperationException($"No product found with barcode '{barcode}'.");

            Db.InTransaction((c, t) =>
                Apply(c, t, LocationType.Godown, godownId, prod.Value.Id, barcode, qty, movement, "Manual", null, user, reason));
        }

        public static List<LedgerRow> GetLedger(string locType, long? locId, string barcode)
        {
            barcode = barcode?.Trim() ?? "";
            return Db.Query(@"
                SELECT l.Id, l.Barcode, p.ProductName, l.LocationType, l.LocationId,
                       CASE l.LocationType
                            WHEN 'Godown' THEN (SELECT GodownName FROM MGodown WHERE Id = l.LocationId)
                            ELSE (SELECT StoreName FROM MStore WHERE Id = l.LocationId) END AS LocationName,
                       l.QtyChange, l.BalanceAfter, l.MovementType, l.ReferenceType, l.ReferenceId,
                       l.Remarks, l.CreatedBy, l.CreatedDate
                FROM StockLedger l
                JOIN MProducts p ON p.Id = l.ProductId
                WHERE (@t IS NULL OR l.LocationType = @t)
                  AND (@loc IS NULL OR l.LocationId = @loc)
                  AND (@bc = '' OR l.Barcode LIKE @like)
                ORDER BY l.Id DESC
                LIMIT 500",
                r => new LedgerRow
                {
                    Id = Rd.Lng(r, "Id"),
                    Barcode = Rd.Str(r, "Barcode"),
                    ProductName = Rd.Str(r, "ProductName"),
                    LocationType = Rd.Str(r, "LocationType"),
                    LocationId = Rd.Lng(r, "LocationId"),
                    LocationName = Rd.Str(r, "LocationName"),
                    QtyChange = Rd.Dec(r, "QtyChange"),
                    BalanceAfter = Rd.Dec(r, "BalanceAfter"),
                    MovementType = Rd.Str(r, "MovementType"),
                    ReferenceType = Rd.Str(r, "ReferenceType"),
                    ReferenceId = Rd.NLng(r, "ReferenceId"),
                    Remarks = Rd.Str(r, "Remarks"),
                    CreatedBy = Rd.Str(r, "CreatedBy"),
                    CreatedDate = Rd.Date(r, "CreatedDate")
                },
                ("@t", locType), ("@loc", locId), ("@bc", barcode), ("@like", "%" + barcode + "%"));
        }
    }
}
