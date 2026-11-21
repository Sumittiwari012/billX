using MySql.Data.MySqlClient;
using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;

namespace MyWPFCRUDApp.Services
{
    /// <summary>Read-only queries for Store stock and Store stock transfers.</summary>
    public class StoreStockService
    {
        private string Con => DatabaseHelper.ConnectionString;

        public List<Store> GetStores()
        {
            var list = new List<Store>();
            using var conn = new MySqlConnection(Con);
            conn.Open();
            using var cmd = new MySqlCommand(
                "SELECT Id, StoreName FROM MStore ORDER BY StoreName", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new Store { Id = r.GetInt64("Id"), StoreName = r.GetString("StoreName") });
            return list;
        }

        public List<StoreStockRow> GetStoreStock()
        {
            var list = new List<StoreStockRow>();
            using var conn = new MySqlConnection(Con);
            conn.Open();
            const string sql = @"
                SELECT ss.Id, ss.StoreId, s.StoreName, ss.ProductId, ss.Barcode,
                       p.ProductCode, p.ProductName, u.UnitName,
                       ss.Quantity, ss.ModifiedDate
                FROM StoreStock ss
                INNER JOIN MStore s    ON ss.StoreId = s.Id
                INNER JOIN MProducts p ON ss.ProductId = p.Id
                LEFT  JOIN MUnit u     ON p.UnitId = u.Id
                ORDER BY s.StoreName, p.ProductName";
            using var cmd = new MySqlCommand(sql, conn);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new StoreStockRow
                {
                    Id = r.GetInt64("Id"),
                    StoreId = r.GetInt64("StoreId"),
                    StoreName = Str(r, "StoreName"),
                    ProductId = r.GetInt64("ProductId"),
                    Barcode = Str(r, "Barcode"),
                    ProductCode = Str(r, "ProductCode"),
                    ProductName = Str(r, "ProductName"),
                    UnitName = Str(r, "UnitName"),
                    Quantity = r.GetDecimal("Quantity"),
                    ModifiedDate = Date(r, "ModifiedDate")
                });
            }
            return list;
        }

        /// <summary>All rows of StoreStockTransfer (each has a Store on the From or To side).</summary>
        public List<StoreTransferRow> GetStoreTransfers()
        {
            var list = new List<StoreTransferRow>();
            using var conn = new MySqlConnection(Con);
            conn.Open();
            const string sql = @"
                SELECT t.Id, t.TransferNo,
                       t.FromType, t.FromId,
                       CASE WHEN t.FromType = 'Store' THEN fs.StoreName ELSE fg.GodownName END AS FromName,
                       t.ToType, t.ToId,
                       CASE WHEN t.ToType = 'Store' THEN ts.StoreName ELSE tg.GodownName END AS ToName,
                       t.Status, t.Remarks,
                       t.DispatchedBy, t.DispatchedAt, t.ReceivedBy, t.ReceivedAt, t.CreatedDate
                FROM StoreStockTransfer t
                LEFT JOIN MStore  fs ON t.FromType = 'Store'  AND fs.Id = t.FromId
                LEFT JOIN MGodown fg ON t.FromType = 'Godown' AND fg.Id = t.FromId
                LEFT JOIN MStore  ts ON t.ToType   = 'Store'  AND ts.Id = t.ToId
                LEFT JOIN MGodown tg ON t.ToType   = 'Godown' AND tg.Id = t.ToId
                ORDER BY t.CreatedDate DESC, t.Id DESC";
            using var cmd = new MySqlCommand(sql, conn);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new StoreTransferRow
                {
                    Id = r.GetInt64("Id"),
                    TransferNo = Str(r, "TransferNo"),
                    FromType = Str(r, "FromType"),
                    FromId = r.GetInt64("FromId"),
                    FromName = Str(r, "FromName"),
                    ToType = Str(r, "ToType"),
                    ToId = r.GetInt64("ToId"),
                    ToName = Str(r, "ToName"),
                    Status = Str(r, "Status"),
                    Remarks = Str(r, "Remarks"),
                    DispatchedBy = Str(r, "DispatchedBy"),
                    DispatchedAt = Date(r, "DispatchedAt"),
                    ReceivedBy = Str(r, "ReceivedBy"),
                    ReceivedAt = Date(r, "ReceivedAt"),
                    CreatedDate = Date(r, "CreatedDate")
                });
            }
            return list;
        }

        public List<StoreTransferItemRow> GetTransferItems(long transferId)
        {
            var list = new List<StoreTransferItemRow>();
            using var conn = new MySqlConnection(Con);
            conn.Open();
            const string sql = @"
                SELECT i.Id, i.TransferId, i.ProductId, i.Barcode, p.ProductName,
                       i.DispatchedQty, i.ReceivedQty, i.Remarks
                FROM StoreStockTransferItem i
                INNER JOIN MProducts p ON i.ProductId = p.Id
                WHERE i.TransferId = @TransferId
                ORDER BY i.Id";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@TransferId", transferId);
            using var r = cmd.ExecuteReader();
            int recvOrd = r.GetOrdinal("ReceivedQty");
            while (r.Read())
            {
                list.Add(new StoreTransferItemRow
                {
                    Id = r.GetInt64("Id"),
                    TransferId = r.GetInt64("TransferId"),
                    ProductId = r.GetInt64("ProductId"),
                    Barcode = Str(r, "Barcode"),
                    ProductName = Str(r, "ProductName"),
                    DispatchedQty = r.GetDecimal("DispatchedQty"),
                    ReceivedQty = r.IsDBNull(recvOrd) ? (decimal?)null : r.GetDecimal(recvOrd),
                    Remarks = Str(r, "Remarks")
                });
            }
            return list;
        }

        // ---- helpers ----
        private static string Str(MySqlDataReader r, string col)
        {
            int o = r.GetOrdinal(col);
            return r.IsDBNull(o) ? "" : r.GetString(o);
        }

        private static DateTime? Date(MySqlDataReader r, string col)
        {
            int o = r.GetOrdinal(col);
            return r.IsDBNull(o) ? (DateTime?)null : r.GetDateTime(o);
        }
    }
}