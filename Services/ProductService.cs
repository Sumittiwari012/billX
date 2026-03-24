using MySql.Data.MySqlClient;
using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWPFCRUDApp.Services
{
    public class ProductService
    {
        private string Con => DatabaseHelper.ConnectionString;
        public bool InsertProduct(MProducts p)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            using var trans = conn.BeginTransaction(); // CRITICAL: Start transaction

            try
            {
                // 1. Insert into MProducts
                var productSql = @"INSERT INTO MProducts (
            ProductCode, ProductName, Barcode, CategoryId, SubCategoryId, 
            HSNCode, PartGroup, Description, PurchasePrice, RetailSalePrice, 
            WholesalePrice, DiscountPercentage, CGST, SGST, CESS, MRP, 
            Godown, Rack, Batch, MfgDate, ExpDate, Size, Colour, 
            IMEI1, IMEI2, CreatedDate, CreatedBy, ModifiedBy
        ) VALUES (
            @ProductCode, @ProductName, @Barcode, @CategoryId, @SubCategoryId, 
            @HSNCode, @PartGroup, @Description, @PurchasePrice, @RetailSalePrice, 
            @WholesalePrice, @DiscountPercentage, @CGST, @SGST, @CESS, @MRP, 
            @Godown, @Rack, @Batch, @MfgDate, @ExpDate, @Size, @Colour, 
            @IMEI1, @IMEI2, @CreatedDate, @CreatedBy, @ModifiedBy
        )";

                using var cmdProd = new MySqlCommand(productSql, conn, trans);
                cmdProd.Parameters.AddWithValue("@ProductCode", p.ProductCode ?? (object)DBNull.Value);
                cmdProd.Parameters.AddWithValue("@ProductName", p.ProductName);
                cmdProd.Parameters.AddWithValue("@Barcode", p.Barcode);
                cmdProd.Parameters.AddWithValue("@CategoryId", p.CategoryId);
                cmdProd.Parameters.AddWithValue("@SubCategoryId", p.SubCategoryId);
                cmdProd.Parameters.AddWithValue("@HSNCode", p.HSNCode ?? (object)DBNull.Value);
                cmdProd.Parameters.AddWithValue("@PartGroup", p.PartGroup ?? (object)DBNull.Value);
                cmdProd.Parameters.AddWithValue("@Description", p.Description ?? (object)DBNull.Value);
                cmdProd.Parameters.AddWithValue("@PurchasePrice", p.PurchasePrice);
                cmdProd.Parameters.AddWithValue("@RetailSalePrice", p.RetailSalePrice);
                cmdProd.Parameters.AddWithValue("@WholesalePrice", p.WholesalePrice);
                cmdProd.Parameters.AddWithValue("@DiscountPercentage", p.DiscountPercentage);
                cmdProd.Parameters.AddWithValue("@CGST", p.CGST);
                cmdProd.Parameters.AddWithValue("@SGST", p.SGST);
                cmdProd.Parameters.AddWithValue("@CESS", p.CESS);
                cmdProd.Parameters.AddWithValue("@MRP", p.MRP);
                cmdProd.Parameters.AddWithValue("@Godown", p.Godown ?? (object)DBNull.Value);
                cmdProd.Parameters.AddWithValue("@Rack", p.Rack ?? (object)DBNull.Value);
                cmdProd.Parameters.AddWithValue("@Batch", p.Batch ?? (object)DBNull.Value);
                cmdProd.Parameters.AddWithValue("@MfgDate", p.MfgDate ?? (object)DBNull.Value);
                cmdProd.Parameters.AddWithValue("@ExpDate", p.ExpDate ?? (object)DBNull.Value);
                cmdProd.Parameters.AddWithValue("@Size", p.Size ?? (object)DBNull.Value);
                cmdProd.Parameters.AddWithValue("@Colour", p.Colour ?? (object)DBNull.Value);
                cmdProd.Parameters.AddWithValue("@IMEI1", p.IMEI1 ?? (object)DBNull.Value);
                cmdProd.Parameters.AddWithValue("@IMEI2", p.IMEI2 ?? (object)DBNull.Value);
                cmdProd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
                cmdProd.Parameters.AddWithValue("@CreatedBy", "ADMIN");
                cmdProd.Parameters.AddWithValue("@ModifiedBy", "");

                cmdProd.ExecuteNonQuery();

                // 2. Initialize Stock (Quantity = 0) in ProductQuantity table
                var qtySql = @"INSERT INTO ProductQuantity (
            ProductCode, Barcode, Quantity, MinimumSellingQuantity, CreatedDate, CreatedBy
        ) VALUES (
            @ProductCode, @Barcode, 0, 1, @CreatedDate, @CreatedBy
        )";

                using var cmdQty = new MySqlCommand(qtySql, conn, trans);
                cmdQty.Parameters.AddWithValue("@ProductCode", p.ProductCode ?? "");
                cmdQty.Parameters.AddWithValue("@Barcode", p.Barcode);
                cmdQty.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
                cmdQty.Parameters.AddWithValue("@CreatedBy", "ADMIN");

                cmdQty.ExecuteNonQuery();

                // 3. If everything is fine, commit to database
                trans.Commit();
                return true;
            }
            catch (Exception)
            {
                trans.Rollback(); // If ANY step fails, undo everything
                return false;
            }
        }
    }
}
