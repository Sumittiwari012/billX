using MySql.Data.MySqlClient;
using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MyWPFCRUDApp.Services
{
    public class PurchaseReturnService
    {
        private string Con => DatabaseHelper.ConnectionString;

        private static object ToDbString(string? s) =>
            string.IsNullOrWhiteSpace(s) ? DBNull.Value : s.Trim();
        private static object ToDbDate(DateTime? d) =>
            d.HasValue ? (object)d.Value : DBNull.Value;

        public long GetReturnCount()
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            using var cmd = new MySqlCommand("SELECT COUNT(*) FROM MPurchaseReturnMaster", conn);
            var result = cmd.ExecuteScalar();
            return result != null && result != DBNull.Value ? Convert.ToInt64(result) : 0;
        }

        private static string? GetBarcodeById(long productId, MySqlConnection conn, MySqlTransaction trans)
        {
            using var cmd = new MySqlCommand("SELECT Barcode FROM MProducts WHERE Id = @Id", conn, trans);
            cmd.Parameters.AddWithValue("@Id", productId);
            var result = cmd.ExecuteScalar();
            return result == null || result == DBNull.Value ? null : result.ToString();
        }

        // ─── ADD RETURN ────────────────────────────────────────────────────────
        public bool AddReturn(MPurchaseReturnMaster ret)
        {
            if (ret == null || ret.MPurchaseReturnDetail == null) return false;

            using var conn = new MySqlConnection(Con);
            conn.Open();
            using var trans = conn.BeginTransaction();

            try
            {
                var masterSql = @"INSERT INTO MPurchaseReturnMaster (
    ReturnInvoiceNumber, InvoiceNumber, SupplierId, ReturnDate, TotalAmount, Remarks,
    CreatedBy, CreatedDate
) VALUES (
    @ReturnInvoiceNumber, @InvoiceNumber, @SupplierId, @ReturnDate, @TotalAmount, @Remarks,
    @CreatedBy, @CreatedDate
); SELECT LAST_INSERT_ID();";

                long masterId;
                using (var cmd = new MySqlCommand(masterSql, conn, trans))
                {
                    cmd.Parameters.AddWithValue("@ReturnInvoiceNumber", ret.ReturnInvoiceNumber);
                    cmd.Parameters.AddWithValue("@InvoiceNumber", ToDbString(ret.InvoiceNumber));
                    cmd.Parameters.AddWithValue("@SupplierId", ret.SupplierId);
                    cmd.Parameters.AddWithValue("@ReturnDate", ret.ReturnDate);
                    cmd.Parameters.AddWithValue("@TotalAmount", ret.TotalAmount);
                    cmd.Parameters.AddWithValue("@Remarks", ret.Remarks ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@CreatedBy", "WPFUser");
                    cmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
                    masterId = Convert.ToInt64(cmd.ExecuteScalar());
                }

                foreach (var detail in ret.MPurchaseReturnDetail)
                {
                    var detailSql = @"INSERT INTO MPurchaseReturnDetail (
    ReturnInvoiceNumber, ProductId, Quantity, PurchasePrice, Batch, MfgDate, ExpDate, Reason
) VALUES (
    @ReturnInvoiceNumber, @ProductId, @Qty, @Price, @Batch, @MfgDate, @ExpDate, @Reason
)";
                    using (var cmd = new MySqlCommand(detailSql, conn, trans))
                    {
                        cmd.Parameters.AddWithValue("@ReturnInvoiceNumber", ret.ReturnInvoiceNumber);
                        cmd.Parameters.AddWithValue("@ProductId", detail.ProductId);
                        cmd.Parameters.AddWithValue("@Qty", detail.Quantity);
                        cmd.Parameters.AddWithValue("@Price", detail.PurchasePrice);
                        cmd.Parameters.AddWithValue("@Batch", ToDbString(detail.Batch));
                        cmd.Parameters.AddWithValue("@MfgDate", ToDbDate(detail.MfgDate));
                        cmd.Parameters.AddWithValue("@ExpDate", ToDbDate(detail.ExpDate));
                        cmd.Parameters.AddWithValue("@Reason", ToDbString(detail.Reason));
                        cmd.ExecuteNonQuery();
                    }

                    // Reduce stock + trim the matching batch entry
                    string? barcode = detail.Barcode ?? GetBarcodeById(detail.ProductId, conn, trans);
                    if (!string.IsNullOrWhiteSpace(barcode))
                        ApplyReturnToStock(barcode, detail.Quantity, ret.InvoiceNumber, conn, trans);
                }

                trans.Commit();
                return true;
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }

        // Subtracts qty from ProductQuantity.Quantity (clamped at 0) and
        // reduces the matching batch entry's own Quantity by the same amount.
        private static void ApplyReturnToStock(
            string barcode, double qty, string? invoiceNumber,
            MySqlConnection conn, MySqlTransaction trans)
        {
            string? currentJson = null;
            using (var sel = new MySqlCommand(
                "SELECT PurchaseQuantity FROM ProductQuantity WHERE Barcode = @Barcode FOR UPDATE",
                conn, trans))
            {
                sel.Parameters.AddWithValue("@Barcode", barcode);
                var result = sel.ExecuteScalar();
                currentJson = (result == null || result == DBNull.Value) ? null : result.ToString();
            }

            string? updatedJson = PurchaseBatchHelper.ReduceQuantity(currentJson, invoiceNumber, qty);

            using var upd = new MySqlCommand(@"
                UPDATE ProductQuantity SET
                    Quantity         = GREATEST(0, Quantity - @Qty),
                    PurchaseQuantity = @Json,
                    ModifiedBy       = 'WPFUser',
                    ModifiedDate     = @Now
                WHERE Barcode = @Barcode", conn, trans);
            upd.Parameters.AddWithValue("@Qty", qty);
            upd.Parameters.AddWithValue("@Json", (object?)updatedJson ?? DBNull.Value);
            upd.Parameters.AddWithValue("@Now", DateTime.Now);
            upd.Parameters.AddWithValue("@Barcode", barcode);
            upd.ExecuteNonQuery();
        }

        // Reverses ApplyReturnToStock — used by Update (on the old quantity)
        // and Delete.
        private static void RevertReturnFromStock(
            string barcode, double qty, string? invoiceNumber,
            MySqlConnection conn, MySqlTransaction trans)
        {
            string? currentJson = null;
            using (var sel = new MySqlCommand(
                "SELECT PurchaseQuantity FROM ProductQuantity WHERE Barcode = @Barcode FOR UPDATE",
                conn, trans))
            {
                sel.Parameters.AddWithValue("@Barcode", barcode);
                var result = sel.ExecuteScalar();
                currentJson = (result == null || result == DBNull.Value) ? null : result.ToString();
            }

            string? updatedJson = PurchaseBatchHelper.RestoreQuantity(currentJson, invoiceNumber, qty);

            using var upd = new MySqlCommand(@"
                UPDATE ProductQuantity SET
                    Quantity         = Quantity + @Qty,
                    PurchaseQuantity = @Json,
                    ModifiedBy       = 'WPFUser',
                    ModifiedDate     = @Now
                WHERE Barcode = @Barcode", conn, trans);
            upd.Parameters.AddWithValue("@Qty", qty);
            upd.Parameters.AddWithValue("@Json", (object?)updatedJson ?? DBNull.Value);
            upd.Parameters.AddWithValue("@Now", DateTime.Now);
            upd.Parameters.AddWithValue("@Barcode", barcode);
            upd.ExecuteNonQuery();
        }

        // ─── UPDATE RETURN ─────────────────────────────────────────────────────
        // Same delta pattern as UpdatePurchase, but inverted: a return removes
        // stock, so increasing a return's quantity removes MORE stock, and
        // decreasing it gives stock back.
        public bool UpdateReturn(long masterId, MPurchaseReturnMaster ret)
        {
            if (ret == null || ret.MPurchaseReturnDetail == null) return false;

            using var conn = new MySqlConnection(Con);
            conn.Open();
            using var trans = conn.BeginTransaction();

            try
            {
                // Snapshot OLD quantities per barcode before deleting old lines
                var oldQtyByBarcode = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                string? oldReturnInvoiceNumber;
                string? oldInvoiceNumber;
                using (var cmd = new MySqlCommand(
                    "SELECT ReturnInvoiceNumber, InvoiceNumber FROM MPurchaseReturnMaster WHERE Id = @Id",
                    conn, trans))
                {
                    cmd.Parameters.AddWithValue("@Id", masterId);
                    using var rdr = cmd.ExecuteReader();
                    if (!rdr.Read()) { trans.Rollback(); return false; }
                    oldReturnInvoiceNumber = rdr["ReturnInvoiceNumber"] as string;
                    oldInvoiceNumber = rdr["InvoiceNumber"] as string;
                }

                using (var cmd = new MySqlCommand(@"
                    SELECT p.Barcode, d.Quantity
                    FROM MPurchaseReturnDetail d
                    LEFT JOIN MProducts p ON p.Id = d.ProductId
                    WHERE d.ReturnInvoiceNumber = @Rin", conn, trans))
                {
                    cmd.Parameters.AddWithValue("@Rin", oldReturnInvoiceNumber);
                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        string? barcode = rdr["Barcode"] as string;
                        if (string.IsNullOrWhiteSpace(barcode)) continue;
                        double qty = rdr.GetDouble("Quantity");
                        oldQtyByBarcode[barcode] = oldQtyByBarcode.TryGetValue(barcode, out var e) ? e + qty : qty;
                    }
                }

                // First, revert ALL old quantities back to stock (undo the old return)
                foreach (var (barcode, qty) in oldQtyByBarcode)
                    RevertReturnFromStock(barcode, qty, oldInvoiceNumber, conn, trans);

                // Update master
                using (var cmd = new MySqlCommand(@"
                    UPDATE MPurchaseReturnMaster SET
                        ReturnInvoiceNumber = @ReturnInvoiceNumber,
                        InvoiceNumber       = @InvoiceNumber,
                        SupplierId          = @SupplierId,
                        ReturnDate          = @ReturnDate,
                        TotalAmount         = @TotalAmount,
                        Remarks             = @Remarks,
                        ModifiedBy          = 'WPFUser',
                        ModifiedDate        = @Now
                    WHERE Id = @Id", conn, trans))
                {
                    cmd.Parameters.AddWithValue("@ReturnInvoiceNumber", ret.ReturnInvoiceNumber);
                    cmd.Parameters.AddWithValue("@InvoiceNumber", ToDbString(ret.InvoiceNumber));
                    cmd.Parameters.AddWithValue("@SupplierId", ret.SupplierId);
                    cmd.Parameters.AddWithValue("@ReturnDate", ret.ReturnDate);
                    cmd.Parameters.AddWithValue("@TotalAmount", ret.TotalAmount);
                    cmd.Parameters.AddWithValue("@Remarks", ret.Remarks ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Now", DateTime.Now);
                    cmd.Parameters.AddWithValue("@Id", masterId);
                    cmd.ExecuteNonQuery();
                }

                // Delete + re-insert detail lines
                using (var cmd = new MySqlCommand(
                    "DELETE FROM MPurchaseReturnDetail WHERE ReturnInvoiceNumber = @Rin", conn, trans))
                {
                    cmd.Parameters.AddWithValue("@Rin", oldReturnInvoiceNumber);
                    cmd.ExecuteNonQuery();
                }

                foreach (var detail in ret.MPurchaseReturnDetail)
                {
                    using (var cmd = new MySqlCommand(@"INSERT INTO MPurchaseReturnDetail (
    ReturnInvoiceNumber, ProductId, Quantity, PurchasePrice, Batch, MfgDate, ExpDate, Reason
) VALUES (
    @ReturnInvoiceNumber, @ProductId, @Qty, @Price, @Batch, @MfgDate, @ExpDate, @Reason
)", conn, trans))
                    {
                        cmd.Parameters.AddWithValue("@ReturnInvoiceNumber", ret.ReturnInvoiceNumber);
                        cmd.Parameters.AddWithValue("@ProductId", detail.ProductId);
                        cmd.Parameters.AddWithValue("@Qty", detail.Quantity);
                        cmd.Parameters.AddWithValue("@Price", detail.PurchasePrice);
                        cmd.Parameters.AddWithValue("@Batch", ToDbString(detail.Batch));
                        cmd.Parameters.AddWithValue("@MfgDate", ToDbDate(detail.MfgDate));
                        cmd.Parameters.AddWithValue("@ExpDate", ToDbDate(detail.ExpDate));
                        cmd.Parameters.AddWithValue("@Reason", ToDbString(detail.Reason));
                        cmd.ExecuteNonQuery();
                    }

                    string? barcode = detail.Barcode ?? GetBarcodeById(detail.ProductId, conn, trans);
                    if (!string.IsNullOrWhiteSpace(barcode))
                        ApplyReturnToStock(barcode, detail.Quantity, ret.InvoiceNumber, conn, trans);
                }

                trans.Commit();
                return true;
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }

        // ─── DELETE RETURN ─────────────────────────────────────────────────────
        public bool DeleteReturn(long masterId)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            using var trans = conn.BeginTransaction();
            try
            {
                string? returnInvoiceNumber = null;
                string? invoiceNumber = null;
                using (var cmd = new MySqlCommand(
                    "SELECT ReturnInvoiceNumber, InvoiceNumber FROM MPurchaseReturnMaster WHERE Id = @Id",
                    conn, trans))
                {
                    cmd.Parameters.AddWithValue("@Id", masterId);
                    using var rdr = cmd.ExecuteReader();
                    if (rdr.Read())
                    {
                        returnInvoiceNumber = rdr["ReturnInvoiceNumber"] as string;
                        invoiceNumber = rdr["InvoiceNumber"] as string;
                    }
                }

                var qtyByBarcode = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                using (var cmd = new MySqlCommand(@"
                    SELECT p.Barcode, d.Quantity
                    FROM MPurchaseReturnDetail d
                    LEFT JOIN MProducts p ON p.Id = d.ProductId
                    WHERE d.ReturnInvoiceNumber = @Rin", conn, trans))
                {
                    cmd.Parameters.AddWithValue("@Rin", returnInvoiceNumber);
                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        string? barcode = rdr["Barcode"] as string;
                        if (string.IsNullOrWhiteSpace(barcode)) continue;
                        double qty = rdr.GetDouble("Quantity");
                        qtyByBarcode[barcode] = qtyByBarcode.TryGetValue(barcode, out var e) ? e + qty : qty;
                    }
                }

                using (var cmd = new MySqlCommand(
                    "DELETE FROM MPurchaseReturnDetail WHERE ReturnInvoiceNumber = @Rin", conn, trans))
                {
                    cmd.Parameters.AddWithValue("@Rin", returnInvoiceNumber);
                    cmd.ExecuteNonQuery();
                }

                int rowsDeleted;
                using (var cmd = new MySqlCommand(
                    "DELETE FROM MPurchaseReturnMaster WHERE Id = @Id", conn, trans))
                {
                    cmd.Parameters.AddWithValue("@Id", masterId);
                    rowsDeleted = cmd.ExecuteNonQuery();
                }

                foreach (var (barcode, qty) in qtyByBarcode)
                    RevertReturnFromStock(barcode, qty, invoiceNumber, conn, trans);

                trans.Commit();
                return rowsDeleted > 0;
            }
            catch
            {
                trans.Rollback();
                return false;
            }
        }

        // ─── GET FILTERED ──────────────────────────────────────────────────────
        public List<MPurchaseReturnMaster> GetFilteredReturns(
            long? supplierId = null, string? returnInvoiceNumber = null,
            DateTime? fromDate = null, DateTime? toDate = null)
        {
            var list = new List<MPurchaseReturnMaster>();
            using var conn = new MySqlConnection(Con);
            conn.Open();

            var where = new List<string>();
            if (supplierId.HasValue) where.Add("m.SupplierId = @SupplierId");
            if (!string.IsNullOrWhiteSpace(returnInvoiceNumber)) where.Add("m.ReturnInvoiceNumber LIKE @Rin");
            if (fromDate.HasValue) where.Add("m.ReturnDate >= @FromDate");
            if (toDate.HasValue) where.Add("m.ReturnDate <= @ToDate");
            string whereStr = where.Any() ? "WHERE " + string.Join(" AND ", where) : "";

            var sql = $@"SELECT m.Id, m.ReturnInvoiceNumber, m.InvoiceNumber, m.SupplierId,
                                m.ReturnDate, m.TotalAmount, m.Remarks, s.SupplierName
                         FROM MPurchaseReturnMaster m
                         LEFT JOIN MSupplier s ON s.Id = m.SupplierId
                         {whereStr}
                         ORDER BY m.ReturnDate DESC";

            using var cmd = new MySqlCommand(sql, conn);
            if (supplierId.HasValue) cmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);
            if (!string.IsNullOrWhiteSpace(returnInvoiceNumber)) cmd.Parameters.AddWithValue("@Rin", $"%{returnInvoiceNumber}%");
            if (fromDate.HasValue) cmd.Parameters.AddWithValue("@FromDate", fromDate.Value.Date);
            if (toDate.HasValue) cmd.Parameters.AddWithValue("@ToDate", toDate.Value.Date.AddDays(1).AddSeconds(-1));

            var masterIds = new List<(long Id, MPurchaseReturnMaster Master)>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var master = new MPurchaseReturnMaster
                    {
                        Id = reader.GetInt64("Id"),
                        ReturnInvoiceNumber = reader["ReturnInvoiceNumber"] as string ?? "",
                        InvoiceNumber = reader["InvoiceNumber"] as string ?? "",
                        SupplierId = reader.GetInt64("SupplierId"),
                        SupplierName = reader["SupplierName"] as string,
                        ReturnDate = reader["ReturnDate"] == DBNull.Value ? DateTime.MinValue : reader.GetDateTime("ReturnDate"),
                        TotalAmount = reader["TotalAmount"] == DBNull.Value ? 0m : reader.GetDecimal("TotalAmount"),
                        Remarks = reader["Remarks"] as string
                    };
                    masterIds.Add((master.Id, master));
                    list.Add(master);
                }
            }

            foreach (var (id, master) in masterIds)
            {
                var detailSql = @"SELECT d.ProductId, d.Quantity, d.PurchasePrice, d.Batch, d.MfgDate, d.ExpDate, d.Reason,
                                          p.ProductName, p.Barcode
                                   FROM MPurchaseReturnDetail d
                                   LEFT JOIN MProducts p ON p.Id = d.ProductId
                                   WHERE d.ReturnInvoiceNumber = @Rin";
                using var dcmd = new MySqlCommand(detailSql, conn);
                dcmd.Parameters.AddWithValue("@Rin", master.ReturnInvoiceNumber);
                using var dr = dcmd.ExecuteReader();
                while (dr.Read())
                {
                    master.MPurchaseReturnDetail.Add(new MPurchaseReturnDetail
                    {
                        ProductId = dr.GetInt64("ProductId"),
                        ProductName = dr["ProductName"] as string,
                        Barcode = dr["Barcode"] as string,
                        Quantity = dr.GetDouble("Quantity"),
                        PurchasePrice = dr.GetDecimal("PurchasePrice"),
                        Batch = dr["Batch"] as string,
                        MfgDate = dr["MfgDate"] == DBNull.Value ? null : dr.GetDateTime("MfgDate"),
                        ExpDate = dr["ExpDate"] == DBNull.Value ? null : dr.GetDateTime("ExpDate"),
                        Reason = dr["Reason"] as string
                    });
                }
            }

            return list;
        }
    }
}