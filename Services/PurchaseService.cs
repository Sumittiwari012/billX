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
                    Discount, PaymentMode, Remarks, CreatedBy, CreatedDate
                ) VALUES (
                    @InvoiceNumber, @SupplierId, @PurchaseDate, @TotalAmount,
                    @Discount, @PaymentMode, @Remarks, @CreatedBy, @CreatedDate
                ); SELECT LAST_INSERT_ID();";

                long masterId;
                using (var cmdMaster = new MySqlCommand(masterSql, conn, trans))
                {
                    cmdMaster.Parameters.AddWithValue("@InvoiceNumber", purchase.InvoiceNumber);
                    cmdMaster.Parameters.AddWithValue("@SupplierId",    purchase.SupplierId);
                    cmdMaster.Parameters.AddWithValue("@PurchaseDate",  purchase.PurchaseDate);
                    cmdMaster.Parameters.AddWithValue("@TotalAmount",   purchase.TotalAmount);
                    cmdMaster.Parameters.AddWithValue("@Discount",      purchase.Discount);
                    cmdMaster.Parameters.AddWithValue("@PaymentMode",   purchase.PaymentMode ?? (object)DBNull.Value);
                    cmdMaster.Parameters.AddWithValue("@Remarks",       purchase.Remarks     ?? (object)DBNull.Value);
                    cmdMaster.Parameters.AddWithValue("@CreatedBy",     "WPFUser");
                    cmdMaster.Parameters.AddWithValue("@CreatedDate",   DateTime.Now);

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
                        PurchaseMasterId, ProductId, Quantity, PurchasePrice, AfterTaxation
                    ) VALUES (
                        @MasterId, @ProductId, @Qty, @Price, @AfterTax
                    )";

                    using (var cmdDetail = new MySqlCommand(detailSql, conn, trans))
                    {
                        cmdDetail.Parameters.AddWithValue("@MasterId",  masterId);
                        cmdDetail.Parameters.AddWithValue("@ProductId", detail.ProductId);
                        cmdDetail.Parameters.AddWithValue("@Qty",       detail.Quantity);
                        cmdDetail.Parameters.AddWithValue("@Price",     detail.PurchasePrice);
                        cmdDetail.Parameters.AddWithValue("@AfterTax",  detail.AfterTaxation);
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
                        cmdProd.Parameters.AddWithValue("@Price",     detail.PurchasePrice);
                        cmdProd.Parameters.AddWithValue("@Wholesale", detail.WholesalePrice);
                        cmdProd.Parameters.AddWithValue("@MRP",       detail.MRP);
                        cmdProd.Parameters.AddWithValue("@Now",       DateTime.Now);
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
                        cmdStock.Parameters.AddWithValue("@Qty",       detail.Quantity);
                        cmdStock.Parameters.AddWithValue("@Now",       DateTime.Now);
                        cmdStock.Parameters.AddWithValue("@ProductId", detail.ProductId);
                        cmdStock.ExecuteNonQuery();
                    }
                }

                trans.Commit();
                return true;
            }
            catch (Exception)
            {
                trans.Rollback();
                return false;
            }
        }

        // ── Auto-insert a new product row from a scanned bill line ────────────
        // Uses the Barcode + ProductName + prices already set on MPurchaseDetail.
        // Returns the new product's Id.
        private long InsertNewProduct(MPurchaseDetail detail, MySqlConnection conn, MySqlTransaction trans)
        {
            // Derive a ProductCode from the barcode (same value is fine)
            string productCode = detail.Barcode;
            string productName = string.IsNullOrWhiteSpace(detail.ProductName)
                ? detail.Barcode   // fallback — shouldn't happen
                : detail.ProductName.Trim();

            var productSql = @"INSERT INTO MProducts (
                ProductCode, ProductName, Barcode,
                CategoryId, SubCategoryId, UnitId,
                PurchasePrice, RetailSalePrice, WholesalePrice, MRP,
                DiscountPercentage, CGST, SGST, IGST, CESS,
                createdDate, createdBy, modifiedDate, modifiedBy
            ) VALUES (
                @ProductCode, @ProductName, @Barcode,
                0, 0, 0,
                @PurchasePrice, @RetailSalePrice, @WholesalePrice, @MRP,
                0, 0, 0, 0, 0,
                @CreatedDate, 'WPFUser', @CreatedDate, 'WPFUser'
            ); SELECT LAST_INSERT_ID();";

            long newProductId;
            using (var cmdProd = new MySqlCommand(productSql, conn, trans))
            {
                cmdProd.Parameters.AddWithValue("@ProductCode",    productCode);
                cmdProd.Parameters.AddWithValue("@ProductName",    productName);
                cmdProd.Parameters.AddWithValue("@Barcode",        detail.Barcode);
                cmdProd.Parameters.AddWithValue("@PurchasePrice",  detail.PurchasePrice);
                // RetailSalePrice = MRP as a sensible default
                cmdProd.Parameters.AddWithValue("@RetailSalePrice", detail.MRP > 0 ? detail.MRP : detail.PurchasePrice);
                cmdProd.Parameters.AddWithValue("@WholesalePrice", detail.WholesalePrice);
                cmdProd.Parameters.AddWithValue("@MRP",            detail.MRP);
                cmdProd.Parameters.AddWithValue("@CreatedDate",    DateTime.Now);

                newProductId = Convert.ToInt64(cmdProd.ExecuteScalar());
            }

            // Insert initial stock row (Quantity = 0; AddPurchase will increment it)
            var qtySql = @"INSERT INTO ProductQuantity (
                ProductCode, Barcode, Quantity, MinimumSellingQuantity, createdDate, createdBy
            ) VALUES (
                @ProductCode, @Barcode, 0, 1, @CreatedDate, 'WPFUser'
            )";

            using (var cmdQty = new MySqlCommand(qtySql, conn, trans))
            {
                cmdQty.Parameters.AddWithValue("@ProductCode", productCode);
                cmdQty.Parameters.AddWithValue("@Barcode",     detail.Barcode);
                cmdQty.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
                cmdQty.ExecuteNonQuery();
            }

            return newProductId;
        }

        /// <summary>
        /// Retrieves purchase history for a specific supplier.
        /// </summary>
        public List<MPurchaseMaster> GetPurchasesBySupplier(long supplierId)
        {
            var list = new List<MPurchaseMaster>();
            using var conn = new MySqlConnection(Con);
            conn.Open();

            var sql = "SELECT * FROM MPurchaseMaster WHERE SupplierId = @SupplierId ORDER BY PurchaseDate DESC";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@SupplierId", supplierId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new MPurchaseMaster
                {
                    InvoiceNumber = reader.GetString("InvoiceNumber"),
                    SupplierId    = reader.GetInt64("SupplierId"),
                    PurchaseDate  = reader.GetDateTime("PurchaseDate"),
                    TotalAmount   = reader.GetDecimal("TotalAmount"),
                    Discount      = reader.GetDecimal("Discount"),
                    PaymentMode   = reader["PaymentMode"]?.ToString(),
                    Remarks       = reader["Remarks"]?.ToString()
                });
            }
            return list;
        }
    }
}
