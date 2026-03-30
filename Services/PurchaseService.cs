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
        /// Records a new purchase, updates product prices, and increments stock levels.
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
                    cmdMaster.Parameters.AddWithValue("@SupplierId", purchase.SupplierId);
                    cmdMaster.Parameters.AddWithValue("@PurchaseDate", purchase.PurchaseDate);
                    cmdMaster.Parameters.AddWithValue("@TotalAmount", purchase.TotalAmount);
                    cmdMaster.Parameters.AddWithValue("@Discount", purchase.Discount);
                    cmdMaster.Parameters.AddWithValue("@PaymentMode", purchase.PaymentMode ?? (object)DBNull.Value);
                    cmdMaster.Parameters.AddWithValue("@Remarks", purchase.Remarks ?? (object)DBNull.Value);
                    cmdMaster.Parameters.AddWithValue("@CreatedBy", "WPFUser");
                    cmdMaster.Parameters.AddWithValue("@CreatedDate", DateTime.Now);

                    masterId = Convert.ToInt64(cmdMaster.ExecuteScalar());
                }

                // 2. Process Details and Inventory Updates
                foreach (var detail in purchase.MPurchaseDetail)
                {
                    // A. Insert Purchase Detail
                    var detailSql = @"INSERT INTO MPurchaseDetail (
                        PurchaseMasterId, ProductId, Quantity, PurchasePrice, AfterTaxation
                    ) VALUES (
                        @MasterId, @ProductId, @Qty, @Price, @AfterTax
                    )";

                    using (var cmdDetail = new MySqlCommand(detailSql, conn, trans))
                    {
                        cmdDetail.Parameters.AddWithValue("@MasterId", masterId);
                        cmdDetail.Parameters.AddWithValue("@ProductId", detail.ProductId);
                        cmdDetail.Parameters.AddWithValue("@Qty", detail.Quantity);
                        cmdDetail.Parameters.AddWithValue("@Price", detail.PurchasePrice);
                        cmdDetail.Parameters.AddWithValue("@AfterTax", detail.AfterTaxation);
                        cmdDetail.ExecuteNonQuery();
                    }

                    // B. Update Product Purchase Price (MProducts Table)
                    var updateProductSql = @"UPDATE MProducts SET 
                        PurchasePrice = @Price, 
                        ModifiedBy = 'WPFUser', 
                        ModifiedDate = @Now 
                        WHERE Id = @ProductId";

                    using (var cmdProd = new MySqlCommand(updateProductSql, conn, trans))
                    {
                        cmdProd.Parameters.AddWithValue("@Price", detail.PurchasePrice);
                        cmdProd.Parameters.AddWithValue("@Now", DateTime.Now);
                        cmdProd.Parameters.AddWithValue("@ProductId", detail.ProductId);
                        cmdProd.ExecuteNonQuery();
                    }

                    // C. Increment Stock Quantity (ProductQuantity Table)
                    // We use an additive update (Quantity = Quantity + @Qty) to ensure accuracy
                    var updateStockSql = @"UPDATE ProductQuantity SET 
                        Quantity = Quantity + @Qty, 
                        ModifiedBy = 'WPFUser', 
                        ModifiedDate = @Now 
                        WHERE ProductCode = (SELECT ProductCode FROM MProducts WHERE Id = @ProductId)
                        OR Barcode = (SELECT Barcode FROM MProducts WHERE Id = @ProductId)";

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
            catch (Exception)
            {
                trans.Rollback();
                return false;
            }
        }

        /// <summary>
        /// Retrieves purchase history for a specific supplier
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
                    SupplierId = reader.GetInt64("SupplierId"),
                    PurchaseDate = reader.GetDateTime("PurchaseDate"),
                    TotalAmount = reader.GetDecimal("TotalAmount"),
                    Discount = reader.GetDecimal("Discount"),
                    PaymentMode = reader["PaymentMode"]?.ToString(),
                    Remarks = reader["Remarks"]?.ToString()
                });
            }
            return list;
        }
    }
}