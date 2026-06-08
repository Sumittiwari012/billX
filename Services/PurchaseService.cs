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
                var masterSql = @"INSERT INTO MPurchaseMaster (
                    InvoiceNumber, SupplierId, PurchaseDate, TotalAmount,
                    Discount, PaymentMode,AmountPaid, Remarks, CreatedBy, CreatedDate
                ) VALUES (
                    @InvoiceNumber, @SupplierId, @PurchaseDate, @TotalAmount,
                    @Discount, @PaymentMode,@AmountPaid, @Remarks, @CreatedBy, @CreatedDate
                ); SELECT LAST_INSERT_ID();";

                long masterId;
                using (var cmdMaster = new MySqlCommand(masterSql, conn, trans))
                {
                    cmdMaster.Parameters.AddWithValue("@InvoiceNumber", purchase.InvoiceNumber);
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
                        PurchasePrice, WholesalePrice, MRP, AfterTaxation
                    ) VALUES (
                        @MasterId, @ProductId, @Qty,
                        @Price, @Wholesale, @MRP, @AfterTax
                    )";

                    using (var cmdDetail = new MySqlCommand(detailSql, conn, trans))
                    {
                        cmdDetail.Parameters.AddWithValue("@MasterId", masterId);
                        cmdDetail.Parameters.AddWithValue("@ProductId", detail.ProductId);
                        cmdDetail.Parameters.AddWithValue("@Qty", detail.Quantity);
                        cmdDetail.Parameters.AddWithValue("@Price", detail.PurchasePrice);
                        cmdDetail.Parameters.AddWithValue("@Wholesale", detail.WholesalePrice);
                        cmdDetail.Parameters.AddWithValue("@MRP", detail.MRP);
                        cmdDetail.Parameters.AddWithValue("@AfterTax", detail.AfterTaxation);
                        cmdDetail.ExecuteNonQuery();
                    }

                    // ── C. Update Product Prices ─────────────────────────────────────
                    var updateProductSql = @"UPDATE MProducts SET
                        PurchasePrice  = @Price,
                        WholesalePrice = @Wholesale,
                        MRP            = @MRP,
                        ModifiedBy     = 'WPFUser',
                        ModifiedDate   = @Now
                        WHERE Id = @ProductId";

                    using (var cmdProd = new MySqlCommand(updateProductSql, conn, trans))
                    {
                        cmdProd.Parameters.AddWithValue("@Price", detail.PurchasePrice);
                        cmdProd.Parameters.AddWithValue("@Wholesale", detail.WholesalePrice);
                        cmdProd.Parameters.AddWithValue("@MRP", detail.MRP);
                        cmdProd.Parameters.AddWithValue("@Now", DateTime.Now);
                        cmdProd.Parameters.AddWithValue("@ProductId", detail.ProductId);
                        cmdProd.ExecuteNonQuery();
                    }

                    // ── D. Increment Stock ───────────────────────────────────────────
                    var updateStockSql = @"UPDATE ProductQuantity SET
                        Quantity     = Quantity + @Qty,
                        ModifiedBy   = 'WPFUser',
                        ModifiedDate = @Now
                        WHERE Barcode = (SELECT Barcode FROM MProducts WHERE Id = @ProductId LIMIT 1)";

                    using (var cmdStock = new MySqlCommand(updateStockSql, conn, trans))
                    {
                        cmdStock.Parameters.AddWithValue("@Qty", detail.Quantity);
                        cmdStock.Parameters.AddWithValue("@Now", DateTime.Now);
                        cmdStock.Parameters.AddWithValue("@ProductId", detail.ProductId);
                        cmdStock.ExecuteNonQuery();
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
                @PurchasePrice, @RetailSalePrice, @WholesalePrice, @MRP,
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
                cmdProd.Parameters.AddWithValue("@RetailSalePrice", detail.MRP > 0 ? detail.MRP : detail.PurchasePrice);
                cmdProd.Parameters.AddWithValue("@WholesalePrice", detail.WholesalePrice);
                cmdProd.Parameters.AddWithValue("@MRP", detail.MRP);
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
            Console.WriteLine($"[GetPurchasesBySupplier] Querying for SupplierId={supplierId}");

            try
            {
                using var conn = new MySqlConnection(Con);
                conn.Open();
                Console.WriteLine("[GetPurchasesBySupplier] DB connection opened.");

                var sql = @"SELECT InvoiceNumber, SupplierId, PurchaseDate, 
                           TotalAmount, Discount, PaymentMode, Remarks
                    FROM MPurchaseMaster 
                    WHERE SupplierId = @SupplierId 
                    ORDER BY PurchaseDate DESC";

                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@SupplierId", supplierId);

                using var reader = cmd.ExecuteReader();
                int rowCount = 0;
                while (reader.Read())
                {
                    rowCount++;
                    list.Add(new MPurchaseMaster
                    {
                        InvoiceNumber = reader["InvoiceNumber"] == DBNull.Value ? ""
                                        : reader.GetString("InvoiceNumber"),
                        SupplierId = reader["SupplierId"] == DBNull.Value ? 0
                                        : reader.GetInt64("SupplierId"),
                        PurchaseDate = reader["PurchaseDate"] == DBNull.Value ? DateTime.MinValue
                                        : reader.GetDateTime("PurchaseDate"),
                        TotalAmount = reader["TotalAmount"] == DBNull.Value ? 0m
                                        : reader.GetDecimal("TotalAmount"),
                        Discount = reader["Discount"] == DBNull.Value ? 0m
                                        : reader.GetDecimal("Discount"),
                        PaymentMode = reader["PaymentMode"] == DBNull.Value ? null
                                        : reader.GetString("PaymentMode"),
                        Remarks = reader["Remarks"] == DBNull.Value ? null
                                        : reader.GetString("Remarks"),
                    });
                }
                Console.WriteLine($"[GetPurchasesBySupplier] Read {rowCount} rows.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetPurchasesBySupplier] EXCEPTION: {ex.Message}\n{ex.StackTrace}");
            }

            return list;
        }
    }
}