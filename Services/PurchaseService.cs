using MySql.Data.MySqlClient;
using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;

namespace MyWPFCRUDApp.Services
{
    public class PurchaseService
    {
        private string Con => DatabaseHelper.ConnectionString;

        /// <summary>
        /// Records a new purchase. For any line where ProductId == 0 (typed name from scan),
        /// the product is first auto-inserted into MProducts + ProductQuantity using the
        /// Barcode and ProductName already on the detail row, then stock is incremented.
        /// </summary>
        public bool AddPurchase(MPurchaseMaster purchase)
        {
            if (purchase == null || purchase.MPurchaseDetail == null) return false;

            using var conn = new MySqlConnection(Con);
            conn.Open();
            using var trans = conn.BeginTransaction();

            try
            {
                // 1. Insert Purchase Master
                // Replace the AddPurchase master INSERT block:

                var masterSql = @"INSERT INTO MPurchaseMaster (
    InvoiceNumber, VendorInvoiceNumber, SupplierId, PurchaseDate, TotalAmount,
    Discount, PaymentMode, AmountPaid, Remarks, CreatedBy, CreatedDate
) VALUES (
    @InvoiceNumber, @VendorInvoiceNumber, @SupplierId, @PurchaseDate, @TotalAmount,
    @Discount, @PaymentMode, @AmountPaid, @Remarks, @CreatedBy, @CreatedDate
); SELECT LAST_INSERT_ID();";

                long masterId;
                using (var cmdMaster = new MySqlCommand(masterSql, conn, trans))
                {
                    cmdMaster.Parameters.AddWithValue("@InvoiceNumber", purchase.InvoiceNumber);
                    cmdMaster.Parameters.AddWithValue("@VendorInvoiceNumber", purchase.VendorInvoiceNumber); // ← new
                    cmdMaster.Parameters.AddWithValue("@SupplierId", purchase.SupplierId);
                    cmdMaster.Parameters.AddWithValue("@PurchaseDate", purchase.PurchaseDate);
                    cmdMaster.Parameters.AddWithValue("@TotalAmount", purchase.TotalAmount);
                    cmdMaster.Parameters.AddWithValue("@Discount", purchase.Discount);
                    cmdMaster.Parameters.AddWithValue("@PaymentMode", purchase.PaymentMode ?? (object)DBNull.Value);
                    cmdMaster.Parameters.AddWithValue("@Remarks", purchase.Remarks ?? (object)DBNull.Value);
                    cmdMaster.Parameters.AddWithValue("@CreatedBy", "WPFUser");
                    cmdMaster.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
                    cmdMaster.Parameters.AddWithValue("@AmountPaid", purchase.AmountPaid);

                    masterId = Convert.ToInt64(cmdMaster.ExecuteScalar());
                }

                // 2. Process each detail line
                foreach (var detail in purchase.MPurchaseDetail)
                {
                    // ── A. Auto-create product if this is a new scanned item ──────────
                    if (detail.ProductId == 0)
                    {
                        detail.ProductId = InsertNewProduct(detail, conn, trans);
                    }

                    // ── B. Insert Purchase Detail ────────────────────────────────────
                    var detailSql = @"INSERT INTO MPurchaseDetail (
    PurchaseMasterId, ProductId, Quantity,
    PurchasePrice, WholesalePrice, MRP, RetailPrice, AfterTaxation
) VALUES (
    @MasterId, @ProductId, @Qty,
    @Price, @Wholesale, @MRP, @Retail, @AfterTax
)";

                    using (var cmdDetail = new MySqlCommand(detailSql, conn, trans))
                    {
                        cmdDetail.Parameters.AddWithValue("@MasterId", masterId);
                        cmdDetail.Parameters.AddWithValue("@ProductId", detail.ProductId);
                        cmdDetail.Parameters.AddWithValue("@Qty", detail.Quantity);
                        cmdDetail.Parameters.AddWithValue("@Price", detail.PurchasePrice);
                        cmdDetail.Parameters.AddWithValue("@Wholesale", detail.WholesalePrice);
                        cmdDetail.Parameters.AddWithValue("@MRP", detail.MRP);
                        cmdDetail.Parameters.AddWithValue("@Retail", detail.Retail);   // ← add
                        cmdDetail.Parameters.AddWithValue("@AfterTax", detail.AfterTaxation);
                        cmdDetail.ExecuteNonQuery();
                    }

                    // ── C. Update Product Prices ─────────────────────────────────────
                    var updateProductSql = @"UPDATE MProducts SET
    PurchasePrice   = @Price,
    WholesalePrice  = @Wholesale,
    MRP             = @MRP,
    RetailSalePrice = @Retail,   -- ← was 'RetailPrice', but MProducts column is RetailSalePrice
    ModifiedBy      = 'WPFUser',
    ModifiedDate    = @Now
    WHERE Id = @ProductId";

                    using (var cmdProd = new MySqlCommand(updateProductSql, conn, trans))
                    {
                        cmdProd.Parameters.AddWithValue("@Price", detail.PurchasePrice);
                        cmdProd.Parameters.AddWithValue("@Wholesale", detail.WholesalePrice);
                        cmdProd.Parameters.AddWithValue("@MRP", detail.MRP);
                        cmdProd.Parameters.AddWithValue("@Retail", detail.Retail);   // ← add
                        cmdProd.Parameters.AddWithValue("@Now", DateTime.Now);
                        cmdProd.Parameters.AddWithValue("@ProductId", detail.ProductId);
                        cmdProd.ExecuteNonQuery();
                    }
                    if (!string.IsNullOrWhiteSpace(detail.Barcode))
                    {
                        var increaseStockSql = @"UPDATE ProductQuantity SET
        Quantity     = Quantity + @Qty,
        ModifiedBy   = 'WPFUser',
        ModifiedDate = @Now
        WHERE Barcode = @Barcode";

                        using (var cmdQty = new MySqlCommand(increaseStockSql, conn, trans))
                        {
                            cmdQty.Parameters.AddWithValue("@Qty", detail.Quantity);
                            cmdQty.Parameters.AddWithValue("@Now", DateTime.Now);
                            cmdQty.Parameters.AddWithValue("@Barcode", detail.Barcode);
                            cmdQty.ExecuteNonQuery();
                        }
                    }


                }

                trans.Commit();

                return true;
            }
            catch (Exception ex)
            {
                trans.Rollback();
                throw;
            }
        }

        // ── Auto-insert a new product row from a scanned bill line ────────────
        // Sequence: 1) MProducts row  2) ProductQuantity row (qty=0, stock updated later)
        // Returns the new product's Id.
        private long InsertNewProduct(MPurchaseDetail detail, MySqlConnection conn, MySqlTransaction trans)
        {
            string productCode = detail.Barcode;
            string productName = string.IsNullOrWhiteSpace(detail.ProductName)
                ? detail.Barcode
                : detail.ProductName.Trim();

            // ── Fetch real default FK IDs so no FK constraint violations ─────
            long defaultCatId = GetFirstId("SELECT Id FROM MCategory    LIMIT 1", conn, trans);
            long defaultSubId = GetFirstId("SELECT Id FROM MSubCategory LIMIT 1", conn, trans);
            long defaultUnitId = GetFirstId("SELECT Id FROM MUnit        LIMIT 1", conn, trans);

            // ── 1. Insert into MProducts ──────────────────────────────────────
            var productSql = @"INSERT INTO MProducts (
    ProductCode, ProductName, Barcode,
    CategoryId, SubCategoryId, UnitId,
    PurchasePrice, RetailSalePrice, WholesalePrice, MRP,
    DiscountPercentage, CGST, SGST, IGST, CESS,
    createdDate, createdBy, modifiedDate, modifiedBy
) VALUES (
    @ProductCode, @ProductName, @Barcode,
    @CategoryId, @SubCategoryId, @UnitId,
    @PurchasePrice, @RetailPrice, @WholesalePrice, @MRP,
    0, 0, 0, 0, 0,
    @CreatedDate, 'WPFUser', @CreatedDate, 'WPFUser'
); SELECT LAST_INSERT_ID();";

            long newProductId;
            using (var cmdProd = new MySqlCommand(productSql, conn, trans))
            {
                cmdProd.Parameters.AddWithValue("@ProductCode", productCode);
                cmdProd.Parameters.AddWithValue("@ProductName", productName);
                cmdProd.Parameters.AddWithValue("@Barcode", detail.Barcode);
                cmdProd.Parameters.AddWithValue("@CategoryId", defaultCatId);
                cmdProd.Parameters.AddWithValue("@SubCategoryId", defaultSubId);
                cmdProd.Parameters.AddWithValue("@UnitId", defaultUnitId);
                cmdProd.Parameters.AddWithValue("@PurchasePrice", detail.PurchasePrice);
                
                cmdProd.Parameters.AddWithValue("@WholesalePrice", detail.WholesalePrice);
                cmdProd.Parameters.AddWithValue("@MRP", detail.MRP);
                cmdProd.Parameters.AddWithValue("@RetailPrice", detail.Retail > 0 ? detail.Retail : detail.PurchasePrice);
                cmdProd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);

                newProductId = Convert.ToInt64(cmdProd.ExecuteScalar());
            }

            // ── 2. Insert initial ProductQuantity row (qty=0) ─────────────────
            // AddPurchase will increment it in step D
            var qtySql = @"INSERT INTO ProductQuantity (
                ProductCode, Barcode, Quantity, MinimumSellingQuantity, createdDate, createdBy
            ) VALUES (
                @ProductCode, @Barcode, 0, 1, @CreatedDate, 'WPFUser'
            )";

            using (var cmdQty = new MySqlCommand(qtySql, conn, trans))
            {
                cmdQty.Parameters.AddWithValue("@ProductCode", productCode);
                cmdQty.Parameters.AddWithValue("@Barcode", detail.Barcode);
                cmdQty.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
                cmdQty.ExecuteNonQuery();
            }

            return newProductId;
        }

        // ── Helper: get first long value from a query, returns 1 as fallback ─
        private static long GetFirstId(string sql, MySqlConnection conn, MySqlTransaction trans)
        {
            using var cmd = new MySqlCommand(sql, conn, trans);
            var result = cmd.ExecuteScalar();
            return result != null && result != DBNull.Value ? Convert.ToInt64(result) : 1;
        }

        /// <summary>
        /// Returns the total number of purchase invoices saved so far.
        /// Used to auto-generate the next invoice number.
        /// </summary>
        public long GetPurchaseCount()
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            using var cmd = new MySqlCommand("SELECT COUNT(*) FROM MPurchaseMaster", conn);
            var result = cmd.ExecuteScalar();
            return result != null && result != DBNull.Value ? Convert.ToInt64(result) : 0;
        }

        /// <summary>
        /// Retrieves purchase history for a specific supplier.
        /// </summary>
        public List<MPurchaseMaster> GetPurchasesBySupplier(long supplierId)
        {
            var list = new List<MPurchaseMaster>();
            try
            {
                using var conn = new MySqlConnection(Con);
                conn.Open();

                var masterSql = @"SELECT 
    m.Id, m.InvoiceNumber, m.VendorInvoiceNumber, m.SupplierId, m.PurchaseDate, 
    m.TotalAmount, m.Discount, m.PaymentMode, m.Remarks,
    COALESCE(SUM(p.AmountPaid), 0) AS TotalPaid
  FROM MPurchaseMaster m
  LEFT JOIN MPayment p ON p.InvoiceNumber = m.InvoiceNumber 
                       AND p.SupplierId = m.SupplierId
  WHERE m.SupplierId = @SupplierId
  GROUP BY m.Id, m.InvoiceNumber, m.VendorInvoiceNumber, m.SupplierId, m.PurchaseDate,
           m.TotalAmount, m.Discount, m.PaymentMode, m.Remarks
  ORDER BY m.PurchaseDate DESC";

                using var cmdMaster = new MySqlCommand(masterSql, conn);
                cmdMaster.Parameters.AddWithValue("@SupplierId", supplierId);

                var masterIds = new List<(long Id, MPurchaseMaster Master)>();
                using (var reader = cmdMaster.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        decimal totalAmount = reader["TotalAmount"] == DBNull.Value ? 0m : reader.GetDecimal("TotalAmount");
                        decimal totalPaid = reader["TotalPaid"] == DBNull.Value ? 0m : reader.GetDecimal("TotalPaid");

                        var master = new MPurchaseMaster
                        {
                            VendorInvoiceNumber = reader["VendorInvoiceNumber"] == DBNull.Value
                          ? "" : reader.GetString("VendorInvoiceNumber"),
                            Id = reader.GetInt64("Id"),
                            InvoiceNumber = reader["InvoiceNumber"] == DBNull.Value ? "" : reader.GetString("InvoiceNumber"),
                            SupplierId = reader.GetInt64("SupplierId"),
                            PurchaseDate = reader["PurchaseDate"] == DBNull.Value ? DateTime.MinValue : reader.GetDateTime("PurchaseDate"),
                            TotalAmount = totalAmount,
                            Discount = reader["Discount"] == DBNull.Value ? 0m : reader.GetDecimal("Discount"),
                            PaymentMode = reader["PaymentMode"] == DBNull.Value ? null : reader.GetString("PaymentMode"),
                            Remarks = reader["Remarks"] == DBNull.Value ? null : reader.GetString("Remarks"),
                            TotalPaid = totalPaid,
                            RemainingAmount = totalAmount - totalPaid
                        };
                        long masterId = reader.GetInt64("Id");
                        masterIds.Add((masterId, master));
                        list.Add(master);
                    }
                }

                foreach (var (masterId, master) in masterIds)
                {
                    var detailSql = @"SELECT d.ProductId, d.Quantity, d.PurchasePrice,
                                     d.WholesalePrice, d.MRP, d.RetailPrice, d.AfterTaxation,
                                     p.ProductName, p.Barcode
                              FROM MPurchaseDetail d
                              LEFT JOIN MProducts p ON p.Id = d.ProductId
                              WHERE d.PurchaseMasterId = @MasterId";

                    using var cmdDetail = new MySqlCommand(detailSql, conn);
                    cmdDetail.Parameters.AddWithValue("@MasterId", masterId);

                    using var dr = cmdDetail.ExecuteReader();
                    while (dr.Read())
                    {
                        master.Details.Add(new MPurchaseDetail
                        {
                            ProductId = dr.GetInt64("ProductId"),
                            ProductName = dr["ProductName"] == DBNull.Value ? "" : dr.GetString("ProductName"),
                            Barcode = dr["Barcode"] == DBNull.Value ? "" : dr.GetString("Barcode"),
                            Quantity = dr.GetDouble("Quantity"),
                            PurchasePrice = dr.GetDecimal("PurchasePrice"),
                            WholesalePrice = dr.GetDecimal("WholesalePrice"),
                            MRP = dr.GetDecimal("MRP"),
                            Retail = dr["RetailPrice"] == DBNull.Value ? 0m : dr.GetDecimal("RetailPrice"),
                            AfterTaxation = dr.GetDecimal("AfterTaxation"),
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetPurchasesBySupplier] EXCEPTION: {ex.Message}\n{ex.StackTrace}");
            }

            return list;
        }
        public List<MPurchaseMaster> GetFilteredPurchases(
    long? supplierId = null,
    string invoiceNumber = null,
    DateTime? fromDate = null,
    DateTime? toDate = null)
        {
            var list = new List<MPurchaseMaster>();
            try
            {
                using var conn = new MySqlConnection(Con);
                conn.Open();

                var whereClauses = new List<string>();
                if (supplierId.HasValue)
                    whereClauses.Add("m.SupplierId = @SupplierId");
                if (!string.IsNullOrWhiteSpace(invoiceNumber))
                    whereClauses.Add("m.InvoiceNumber LIKE @InvoiceNumber");
                if (fromDate.HasValue)
                    whereClauses.Add("m.PurchaseDate >= @FromDate");
                if (toDate.HasValue)
                    whereClauses.Add("m.PurchaseDate <= @ToDate");

                string whereStr = whereClauses.Any()
                    ? "WHERE " + string.Join(" AND ", whereClauses)
                    : string.Empty;

                var masterSql = $@"SELECT 
            m.Id, m.InvoiceNumber, m.VendorInvoiceNumber, m.SupplierId, 
            m.PurchaseDate, m.TotalAmount, m.Discount, m.PaymentMode, m.Remarks,
            s.SupplierName,
            COALESCE(SUM(p.AmountPaid), 0) AS TotalPaid
          FROM MPurchaseMaster m
          LEFT JOIN MPayment p   ON p.InvoiceNumber = m.InvoiceNumber 
                                 AND p.SupplierId   = m.SupplierId
          LEFT JOIN MSupplier s  ON s.Id = m.SupplierId
          {whereStr}
          GROUP BY m.Id, m.InvoiceNumber, m.VendorInvoiceNumber, m.SupplierId,
                   m.PurchaseDate, m.TotalAmount, m.Discount, m.PaymentMode, 
                   m.Remarks, s.SupplierName
          ORDER BY m.PurchaseDate DESC";

                using var cmdMaster = new MySqlCommand(masterSql, conn);
                if (supplierId.HasValue)
                    cmdMaster.Parameters.AddWithValue("@SupplierId", supplierId.Value);
                if (!string.IsNullOrWhiteSpace(invoiceNumber))
                    cmdMaster.Parameters.AddWithValue("@InvoiceNumber", $"%{invoiceNumber}%");
                if (fromDate.HasValue)
                    cmdMaster.Parameters.AddWithValue("@FromDate", fromDate.Value.Date);
                if (toDate.HasValue)
                    cmdMaster.Parameters.AddWithValue("@ToDate", toDate.Value.Date.AddDays(1).AddSeconds(-1));

                var masterIds = new List<(long Id, MPurchaseMaster Master)>();
                using (var reader = cmdMaster.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        decimal totalAmount = reader["TotalAmount"] == DBNull.Value ? 0m : reader.GetDecimal("TotalAmount");
                        decimal totalPaid = reader["TotalPaid"] == DBNull.Value ? 0m : reader.GetDecimal("TotalPaid");

                        var master = new MPurchaseMaster
                        {
                            Id = reader.GetInt64("Id"),
                            InvoiceNumber = reader["InvoiceNumber"] == DBNull.Value ? "" : reader.GetString("InvoiceNumber"),
                            VendorInvoiceNumber = reader["VendorInvoiceNumber"] == DBNull.Value ? "" : reader.GetString("VendorInvoiceNumber"),
                            SupplierId = reader.GetInt64("SupplierId"),
                            SupplierName = reader["SupplierName"] == DBNull.Value ? "" : reader.GetString("SupplierName"),
                            PurchaseDate = reader["PurchaseDate"] == DBNull.Value ? DateTime.MinValue : reader.GetDateTime("PurchaseDate"),
                            TotalAmount = totalAmount,
                            Discount = reader["Discount"] == DBNull.Value ? 0m : reader.GetDecimal("Discount"),
                            PaymentMode = reader["PaymentMode"] == DBNull.Value ? null : reader.GetString("PaymentMode"),
                            Remarks = reader["Remarks"] == DBNull.Value ? null : reader.GetString("Remarks"),
                            TotalPaid = totalPaid,
                            RemainingAmount = totalAmount - totalPaid
                        };

                        long masterId = reader.GetInt64("Id");
                        masterIds.Add((masterId, master));
                        list.Add(master);
                    }
                }

                foreach (var (masterId, master) in masterIds)
                {
                    var detailSql = @"SELECT d.ProductId, d.Quantity, d.PurchasePrice,
                                     d.WholesalePrice, d.MRP, d.RetailPrice, d.AfterTaxation,
                                     p.ProductName, p.Barcode
                              FROM MPurchaseDetail d
                              LEFT JOIN MProducts p ON p.Id = d.ProductId
                              WHERE d.PurchaseMasterId = @MasterId";

                    using var cmdDetail = new MySqlCommand(detailSql, conn);
                    cmdDetail.Parameters.AddWithValue("@MasterId", masterId);

                    using var dr = cmdDetail.ExecuteReader();
                    while (dr.Read())
                    {
                        master.Details.Add(new MPurchaseDetail
                        {
                            ProductId = dr.GetInt64("ProductId"),
                            ProductName = dr["ProductName"] == DBNull.Value ? "" : dr.GetString("ProductName"),
                            Barcode = dr["Barcode"] == DBNull.Value ? "" : dr.GetString("Barcode"),
                            Quantity = dr.GetDouble("Quantity"),
                            PurchasePrice = dr.GetDecimal("PurchasePrice"),
                            WholesalePrice = dr.GetDecimal("WholesalePrice"),
                            MRP = dr.GetDecimal("MRP"),
                            Retail = dr["RetailPrice"] == DBNull.Value ? 0m : dr.GetDecimal("RetailPrice"),
                            AfterTaxation = dr.GetDecimal("AfterTaxation"),
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetFilteredPurchases] EXCEPTION: {ex.Message}\n{ex.StackTrace}");
            }
            return list;
        }
        public bool UpdatePurchase(long masterId, MPurchaseMaster purchase)
        {
            if (purchase == null || purchase.MPurchaseDetail == null) return false;

            using var conn = new MySqlConnection(Con);
            conn.Open();
            using var trans = conn.BeginTransaction();

            try
            {
                // 1. Update master row
                var masterSql = @"UPDATE MPurchaseMaster SET
            InvoiceNumber = @InvoiceNumber,
            SupplierId    = @SupplierId,
            PurchaseDate  = @PurchaseDate,
            TotalAmount   = @TotalAmount,
            Discount      = @Discount,
            PaymentMode   = @PaymentMode,
            AmountPaid    = @AmountPaid,
            Remarks       = @Remarks,
            ModifiedBy    = 'WPFUser',
            ModifiedDate  = @Now
            WHERE Id = @MasterId";

                using (var cmd = new MySqlCommand(masterSql, conn, trans))
                {
                    cmd.Parameters.AddWithValue("@InvoiceNumber", purchase.InvoiceNumber);
                    cmd.Parameters.AddWithValue("@SupplierId", purchase.SupplierId);
                    cmd.Parameters.AddWithValue("@PurchaseDate", purchase.PurchaseDate);
                    cmd.Parameters.AddWithValue("@TotalAmount", purchase.TotalAmount);
                    cmd.Parameters.AddWithValue("@Discount", purchase.Discount);
                    cmd.Parameters.AddWithValue("@PaymentMode", purchase.PaymentMode ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@AmountPaid", purchase.AmountPaid);
                    cmd.Parameters.AddWithValue("@Remarks", purchase.Remarks ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Now", DateTime.Now);
                    cmd.Parameters.AddWithValue("@MasterId", masterId);
                    cmd.ExecuteNonQuery();
                }

                // 2. Delete old detail lines and re-insert fresh
                using (var cmd = new MySqlCommand(
                    "DELETE FROM MPurchaseDetail WHERE PurchaseMasterId = @MasterId", conn, trans))
                {
                    cmd.Parameters.AddWithValue("@MasterId", masterId);
                    cmd.ExecuteNonQuery();
                }

                // 3. Insert updated lines + update product prices + add to stock
                foreach (var detail in purchase.MPurchaseDetail)
                {
                    if (detail.ProductId == 0)
                        detail.ProductId = InsertNewProduct(detail, conn, trans);

                    var detailSql = @"INSERT INTO MPurchaseDetail (
                PurchaseMasterId, ProductId, Quantity,
                PurchasePrice, WholesalePrice, MRP, RetailPrice, AfterTaxation
            ) VALUES (
                @MasterId, @ProductId, @Qty,
                @Price, @Wholesale, @MRP, @Retail, @AfterTax
            )";

                    using (var cmd = new MySqlCommand(detailSql, conn, trans))
                    {
                        cmd.Parameters.AddWithValue("@MasterId", masterId);
                        cmd.Parameters.AddWithValue("@ProductId", detail.ProductId);
                        cmd.Parameters.AddWithValue("@Qty", detail.Quantity);
                        cmd.Parameters.AddWithValue("@Price", detail.PurchasePrice);
                        cmd.Parameters.AddWithValue("@Wholesale", detail.WholesalePrice);
                        cmd.Parameters.AddWithValue("@MRP", detail.MRP);
                        cmd.Parameters.AddWithValue("@Retail", detail.Retail);
                        cmd.Parameters.AddWithValue("@AfterTax", detail.AfterTaxation);
                        cmd.ExecuteNonQuery();
                    }

                    // Update product prices to latest values
                    var updateProductSql = @"UPDATE MProducts SET
                PurchasePrice   = @Price,
                WholesalePrice  = @Wholesale,
                MRP             = @MRP,
                RetailSalePrice = @Retail,
                ModifiedBy      = 'WPFUser',
                ModifiedDate    = @Now
                WHERE Id = @ProductId";

                    using (var cmd = new MySqlCommand(updateProductSql, conn, trans))
                    {
                        cmd.Parameters.AddWithValue("@Price", detail.PurchasePrice);
                        cmd.Parameters.AddWithValue("@Wholesale", detail.WholesalePrice);
                        cmd.Parameters.AddWithValue("@MRP", detail.MRP);
                        cmd.Parameters.AddWithValue("@Retail", detail.Retail);
                        cmd.Parameters.AddWithValue("@Now", DateTime.Now);
                        cmd.Parameters.AddWithValue("@ProductId", detail.ProductId);
                        cmd.ExecuteNonQuery();
                    }

                    // ── Add purchased quantity onto existing stock ─────────────────────
                    // Looks up the row by Barcode and adds the new quantity to whatever
                    // is already there — same pattern as AddPurchase, no reversal.
                    if (!string.IsNullOrWhiteSpace(detail.Barcode))
                    {
                        var increaseStockSql = @"UPDATE ProductQuantity SET
                    Quantity     = Quantity + @Qty,
                    ModifiedBy   = 'WPFUser',
                    ModifiedDate = @Now
                    WHERE Barcode = @Barcode";

                        using (var cmdQty = new MySqlCommand(increaseStockSql, conn, trans))
                        {
                            cmdQty.Parameters.AddWithValue("@Qty", detail.Quantity);
                            cmdQty.Parameters.AddWithValue("@Now", DateTime.Now);
                            cmdQty.Parameters.AddWithValue("@Barcode", detail.Barcode);
                            cmdQty.ExecuteNonQuery();
                        }
                    }
                }

                trans.Commit();
                return true;
            }
            catch (Exception ex)
            {
                trans.Rollback();
                Console.WriteLine($"[UpdatePurchase] EXCEPTION: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }
        // ─── DELETE PURCHASE ───────────────────────────────────────────────────
        /// <summary>
        /// Deletes a purchase invoice and its detail lines.
        /// For every distinct product that appeared in this invoice's details,
        /// looks up its Barcode and sets ProductQuantity.Quantity to 0.
        /// </summary>
        public (bool Success, long SupplierId) DeletePurchase(long masterId)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            using var trans = conn.BeginTransaction();
            try
            {
                // 0. Grab InvoiceNumber + SupplierId BEFORE deleting the master row —
                //    needed to clean up matching MPayment rows and to recalc the balance.
                string invoiceNumber = null;
                long supplierId = 0;
                using (var cmd = new MySqlCommand(
                    "SELECT InvoiceNumber, SupplierId FROM MPurchaseMaster WHERE Id = @MasterId",
                    conn, trans))
                {
                    cmd.Parameters.AddWithValue("@MasterId", masterId);
                    using var rdr = cmd.ExecuteReader();
                    if (rdr.Read())
                    {
                        invoiceNumber = rdr.GetString("InvoiceNumber");
                        supplierId = rdr.GetInt64("SupplierId");
                    }
                }

                // 1. Get distinct ProductIds referenced by this invoice's detail lines
                var productIds = new List<long>();
                using (var cmd = new MySqlCommand(
                    "SELECT DISTINCT ProductId FROM MPurchaseDetail WHERE PurchaseMasterId = @MasterId",
                    conn, trans))
                {
                    cmd.Parameters.AddWithValue("@MasterId", masterId);
                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                        productIds.Add(rdr.GetInt64("ProductId"));
                }

                // 2. Resolve their barcodes from MProducts
                var barcodes = new List<string>();
                foreach (var productId in productIds)
                {
                    using var cmd = new MySqlCommand(
                        "SELECT Barcode FROM MProducts WHERE Id = @ProductId", conn, trans);
                    cmd.Parameters.AddWithValue("@ProductId", productId);
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                        barcodes.Add(result.ToString());
                }

                // 3. Delete the detail lines
                using (var cmd = new MySqlCommand(
                    "DELETE FROM MPurchaseDetail WHERE PurchaseMasterId = @MasterId", conn, trans))
                {
                    cmd.Parameters.AddWithValue("@MasterId", masterId);
                    cmd.ExecuteNonQuery();
                }

                // 3b. Delete orphaned payments tied to this invoice — otherwise they keep
                //     counting toward the supplier's balance after the invoice is gone.
                if (!string.IsNullOrWhiteSpace(invoiceNumber))
                {
                    using var cmd = new MySqlCommand(
                        "DELETE FROM MPayment WHERE InvoiceNumber = @InvoiceNumber AND SupplierId = @SupplierId",
                        conn, trans);
                    cmd.Parameters.AddWithValue("@InvoiceNumber", invoiceNumber);
                    cmd.Parameters.AddWithValue("@SupplierId", supplierId);
                    cmd.ExecuteNonQuery();
                }

                // 4. Delete the master row
                int rowsDeleted;
                using (var cmd = new MySqlCommand(
                    "DELETE FROM MPurchaseMaster WHERE Id = @MasterId", conn, trans))
                {
                    cmd.Parameters.AddWithValue("@MasterId", masterId);
                    rowsDeleted = cmd.ExecuteNonQuery();
                }

                // 5. Set quantity to 0 for every barcode that was in this invoice
                foreach (var barcode in barcodes)
                {
                    using var cmd = new MySqlCommand(@"
                UPDATE ProductQuantity SET
                    Quantity     = 0,
                    ModifiedBy   = 'WPFUser',
                    ModifiedDate = @Now
                WHERE Barcode = @Barcode", conn, trans);
                    cmd.Parameters.AddWithValue("@Now", DateTime.Now);
                    cmd.Parameters.AddWithValue("@Barcode", barcode);
                    cmd.ExecuteNonQuery();
                }

                trans.Commit();
                return (rowsDeleted > 0, supplierId);
            }
            catch (Exception ex)
            {
                trans.Rollback();
                Console.WriteLine($"[DeletePurchase] EXCEPTION: {ex.Message}\n{ex.StackTrace}");
                return (false, 0);
            }
        }
    }
}