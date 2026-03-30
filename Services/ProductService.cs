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
        public class ProductDisplayModel
        {
            public long Id { get; set; }
            public string? ProductCode { get; set; }
            public string ProductName { get; set; }
            public string Barcode { get; set; }

            // Category
            public long CategoryId { get; set; }
            public string CategoryName { get; set; }

            // SubCategory
            public long SubCategoryId { get; set; }
            public string SubCategoryName { get; set; }

            // Unit
            public long UnitId { get; set; }
            public string UnitName { get; set; }

            // Pricing
            public decimal PurchasePrice { get; set; }
            public decimal RetailSalePrice { get; set; }
            public decimal WholesalePrice { get; set; }
            public decimal MRP { get; set; }
            public double DiscountPercentage { get; set; }
            public double CGST { get; set; }
            public double SGST { get; set; }
            public double CESS { get; set; }

            // Details
            public string? HSNCode { get; set; }
            public string? PartGroup { get; set; }
            public string? Description { get; set; }

            // Inventory
            public string? Godown { get; set; }
            public string? Rack { get; set; }
            public string? Batch { get; set; }
            public DateTime? MfgDate { get; set; }
            public DateTime? ExpDate { get; set; }

            // Attributes
            public string? Size { get; set; }
            public string? Colour { get; set; }
            public string? IMEI1 { get; set; }
            public string? IMEI2 { get; set; }
        }
        // ─── INSERT ────────────────────────────────────────────────────────────

        // Returns display list with joined CategoryName, SubCategoryName, UnitName
        public List<ProductDisplayModel> GetProductDisplay()
        {
            var list = new List<ProductDisplayModel>();
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"SELECT p.*, 
                    c.CategoryName, 
                    sc.SubCategoryName, 
                    u.UnitName
                FROM MProducts p
                LEFT JOIN MCategory c    ON p.CategoryId    = c.Id
                LEFT JOIN MSubCategory sc ON p.SubCategoryId = sc.Id
                LEFT JOIN MUnit u         ON p.UnitId        = u.Id
                ORDER BY p.createdDate";
            var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ProductDisplayModel
                {
                    Id = reader.GetInt64("Id"),
                    ProductCode = reader["ProductCode"] == DBNull.Value ? null : reader.GetString("ProductCode"),
                    ProductName = reader.GetString("ProductName"),
                    Barcode = reader.GetString("Barcode"),
                    CategoryId = reader.GetInt64("CategoryId"),
                    CategoryName = reader["CategoryName"] == DBNull.Value ? "N/A" : reader.GetString("CategoryName"),
                    SubCategoryId = reader.GetInt64("SubCategoryId"),
                    SubCategoryName = reader["SubCategoryName"] == DBNull.Value ? "N/A" : reader.GetString("SubCategoryName"),
                    UnitId = reader.GetInt64("UnitId"),
                    UnitName = reader["UnitName"] == DBNull.Value ? "N/A" : reader.GetString("UnitName"),
                    PurchasePrice = reader.GetDecimal("PurchasePrice"),
                    RetailSalePrice = reader.GetDecimal("RetailSalePrice"),
                    WholesalePrice = reader.GetDecimal("WholesalePrice"),
                    MRP = reader.GetDecimal("MRP"),
                    DiscountPercentage = reader.GetDouble("DiscountPercentage"),
                    CGST = reader.GetDouble("CGST"),
                    SGST = reader.GetDouble("SGST"),
                    CESS = reader.GetDouble("CESS"),
                    HSNCode = reader["HSNCode"] == DBNull.Value ? null : reader.GetString("HSNCode"),
                    PartGroup = reader["PartGroup"] == DBNull.Value ? null : reader.GetString("PartGroup"),
                    Description = reader["Description"] == DBNull.Value ? null : reader.GetString("Description"),
                    Godown = reader["Godown"] == DBNull.Value ? null : reader.GetString("Godown"),
                    Rack = reader["Rack"] == DBNull.Value ? null : reader.GetString("Rack"),
                    Batch = reader["Batch"] == DBNull.Value ? null : reader.GetString("Batch"),
                    MfgDate = reader["MfgDate"] == DBNull.Value ? null : reader.GetDateTime("MfgDate"),
                    ExpDate = reader["ExpDate"] == DBNull.Value ? null : reader.GetDateTime("ExpDate"),
                    Size = reader["Size"] == DBNull.Value ? null : reader.GetString("Size"),
                    Colour = reader["Colour"] == DBNull.Value ? null : reader.GetString("Colour"),
                    IMEI1 = reader["IMEI1"] == DBNull.Value ? null : reader.GetString("IMEI1"),
                    IMEI2 = reader["IMEI2"] == DBNull.Value ? null : reader.GetString("IMEI2"),
                });
            }
            return list;
        }
        public bool InsertProduct(MProducts p)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            using var trans = conn.BeginTransaction();
            try
            {
                var productSql = @"INSERT INTO MProducts (
    ProductCode, ProductName, Barcode, CategoryId, SubCategoryId, UnitId, 
    HSNCode, PartGroup, Description, PurchasePrice, RetailSalePrice, 
    WholesalePrice, DiscountPercentage, CGST, SGST, CESS, MRP, 
    Godown, Rack, Batch, MfgDate, ExpDate, Size, Colour, 
    IMEI1, IMEI2, CreatedDate, CreatedBy -- Match case sensitivity of SQL
) VALUES (
    @ProductCode, @ProductName, @Barcode, @CategoryId, @SubCategoryId, @UnitId,
    @HSNCode, @PartGroup, @Description, @PurchasePrice, @RetailSalePrice, 
    @WholesalePrice, @DiscountPercentage, @CGST, @SGST, @CESS, @MRP, 
    @Godown, @Rack, @Batch, @MfgDate, @ExpDate, @Size, @Colour, 
    @IMEI1, @IMEI2, @CreatedDate, @CreatedBy
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
                cmdProd.Parameters.AddWithValue("@UnitId", p.UnitId);
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
                cmdProd.Parameters.AddWithValue("@createdDate", DateTime.Now);
                cmdProd.Parameters.AddWithValue("@createdBy", "ADMIN");
                cmdProd.Parameters.AddWithValue("@modifiedBy", "");
                cmdProd.Parameters.AddWithValue("@modifiedDate", DateTime.Now);
                cmdProd.ExecuteNonQuery();

                var qtySql = @"INSERT INTO ProductQuantity (
    ProductCode, Barcode, Quantity, MinimumSellingQuantity, createdDate, createdBy
) VALUES (
    @ProductCode, @Barcode, 0, 1, @createdDate, @createdBy 
)";

                using var cmdQty = new MySqlCommand(qtySql, conn, trans);
                cmdQty.Parameters.AddWithValue("@ProductCode", p.ProductCode ?? "");
                cmdQty.Parameters.AddWithValue("@Barcode", p.Barcode);
                cmdQty.Parameters.AddWithValue("@createdDate", DateTime.Now);
                cmdQty.Parameters.AddWithValue("@createdBy", "ADMIN");
                cmdQty.ExecuteNonQuery();

                trans.Commit();
                return true;
            }
            catch (Exception)
            {
                trans.Rollback();
                return false;
            }
        }

        // ─── UPDATE ────────────────────────────────────────────────────────────
        public bool UpdateProduct(MProducts p)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            using var trans = conn.BeginTransaction();
            try
            {
                var sql = @"UPDATE MProducts SET
                    ProductName        = @ProductName,
                    ProductCode        = @ProductCode,
                    Barcode            = @Barcode,
                    CategoryId         = @CategoryId,
                    SubCategoryId      = @SubCategoryId,
                    HSNCode            = @HSNCode,
                    Description        = @Description,
                    PurchasePrice      = @PurchasePrice,
                    RetailSalePrice    = @RetailSalePrice,
                    WholesalePrice     = @WholesalePrice,
                    MRP                = @MRP,
                    CGST               = @CGST,
                    SGST               = @SGST,
                    CESS               = @CESS,
                    Batch              = @Batch,
                    ExpDate            = @ExpDate,
                    Size               = @Size,
                    Colour             = @Colour,
                    modifiedDate       = @modifiedDate,
                    modifiedBy         = @modifiedBy
                WHERE Id = @Id";

                using var cmd = new MySqlCommand(sql, conn, trans);
                cmd.Parameters.AddWithValue("@ProductName", p.ProductName);
                cmd.Parameters.AddWithValue("@ProductCode", p.ProductCode ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Barcode", p.Barcode);
                cmd.Parameters.AddWithValue("@CategoryId", p.CategoryId);
                cmd.Parameters.AddWithValue("@SubCategoryId", p.SubCategoryId);
                cmd.Parameters.AddWithValue("@HSNCode", p.HSNCode ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Description", p.Description ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@PurchasePrice", p.PurchasePrice);
                cmd.Parameters.AddWithValue("@RetailSalePrice", p.RetailSalePrice);
                cmd.Parameters.AddWithValue("@WholesalePrice", p.WholesalePrice);
                cmd.Parameters.AddWithValue("@MRP", p.MRP);
                cmd.Parameters.AddWithValue("@CGST", p.CGST);
                cmd.Parameters.AddWithValue("@SGST", p.SGST);
                cmd.Parameters.AddWithValue("@CESS", p.CESS);
                cmd.Parameters.AddWithValue("@Batch", p.Batch ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ExpDate", p.ExpDate ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Size", p.Size ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Colour", p.Colour ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@modifiedDate", DateTime.Now);
                cmd.Parameters.AddWithValue("@modifiedBy", "admin");
                cmd.Parameters.AddWithValue("@Id", p.Id);
                cmd.ExecuteNonQuery();

                trans.Commit();
                return true;
            }
            catch (Exception)
            {
                trans.Rollback();
                return false;
            }
        }

        // ─── DELETE ────────────────────────────────────────────────────────────
        public bool DeleteProduct(long id)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            using var trans = conn.BeginTransaction();
            try
            {
                string? barcode = null;
                using (var cmdGet = new MySqlCommand(
                    "SELECT Barcode FROM MProducts WHERE Id = @Id", conn, trans))
                {
                    cmdGet.Parameters.AddWithValue("@Id", id);
                    barcode = cmdGet.ExecuteScalar() as string;
                }

                using (var cmdDel = new MySqlCommand(
                    "DELETE FROM MProducts WHERE Id = @Id", conn, trans))
                {
                    cmdDel.Parameters.AddWithValue("@Id", id);
                    cmdDel.ExecuteNonQuery();
                }

                if (!string.IsNullOrEmpty(barcode))
                {
                    using var cmdQty = new MySqlCommand(
                        "DELETE FROM ProductQuantity WHERE Barcode = @Barcode", conn, trans);
                    cmdQty.Parameters.AddWithValue("@Barcode", barcode);
                    cmdQty.ExecuteNonQuery();
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

        // ─── GET ALL ───────────────────────────────────────────────────────────
        public List<MProducts> GetProducts()
        {
            var list = new List<MProducts>();
            using var conn = new MySqlConnection(Con);
            conn.Open();

            using var cmd = new MySqlCommand(
                "SELECT * FROM MProducts ORDER BY CreatedDate", conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(new MProducts
                {
                    Id = Convert.ToInt64(reader["Id"]),
                    ProductCode = reader["ProductCode"] == DBNull.Value ? null : reader["ProductCode"].ToString(),
                    ProductName = reader["ProductName"].ToString()!,
                    Barcode = reader["Barcode"].ToString()!,
                    CategoryId = Convert.ToInt64(reader["CategoryId"]),
                    SubCategoryId = Convert.ToInt64(reader["SubCategoryId"]),
                    HSNCode = reader["HSNCode"] == DBNull.Value ? null : reader["HSNCode"].ToString(),
                    PartGroup = reader["PartGroup"] == DBNull.Value ? null : reader["PartGroup"].ToString(),
                    Description = reader["Description"] == DBNull.Value ? null : reader["Description"].ToString(),
                    PurchasePrice = Convert.ToDecimal(reader["PurchasePrice"]),
                    RetailSalePrice = Convert.ToDecimal(reader["RetailSalePrice"]),
                    WholesalePrice = Convert.ToDecimal(reader["WholesalePrice"]),
                    DiscountPercentage = Convert.ToDouble(reader["DiscountPercentage"]),
                    CGST = Convert.ToDouble(reader["CGST"]),
                    SGST = Convert.ToDouble(reader["SGST"]),
                    CESS = Convert.ToDouble(reader["CESS"]),
                    MRP = Convert.ToDecimal(reader["MRP"]),
                    Godown = reader["Godown"] == DBNull.Value ? null : reader["Godown"].ToString(),
                    Rack = reader["Rack"] == DBNull.Value ? null : reader["Rack"].ToString(),
                    Batch = reader["Batch"] == DBNull.Value ? null : reader["Batch"].ToString(),
                    MfgDate = reader["MfgDate"] == DBNull.Value ? null : Convert.ToDateTime(reader["MfgDate"]),
                    ExpDate = reader["ExpDate"] == DBNull.Value ? null : Convert.ToDateTime(reader["ExpDate"]),
                    Size = reader["Size"] == DBNull.Value ? null : reader["Size"].ToString(),
                    Colour = reader["Colour"] == DBNull.Value ? null : reader["Colour"].ToString(),
                    IMEI1 = reader["IMEI1"] == DBNull.Value ? null : reader["IMEI1"].ToString(),
                    IMEI2 = reader["IMEI2"] == DBNull.Value ? null : reader["IMEI2"].ToString(),
                    createdDate = Convert.ToDateTime(reader["createdDate"]),
                    createdBy = reader["createdBy"] == DBNull.Value ? null : reader["createdBy"].ToString(),
                    modifiedDate = Convert.ToDateTime(reader["modifiedDate"]),
                    modifiedBy = reader["modifiedBy"] == DBNull.Value ? null : reader["modifiedBy"].ToString(),
                });
            }

            return list;
        }

        // ─── GET BY BARCODE ────────────────────────────────────────────────────
        public MProducts? GetByBarcode(string barcode)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();

            using var cmd = new MySqlCommand(
                "SELECT * FROM MProducts WHERE Barcode = @Barcode LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@Barcode", barcode);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            return new MProducts
            {
                Id = Convert.ToInt64(reader["Id"]),
                ProductCode = reader["ProductCode"] == DBNull.Value ? null : reader["ProductCode"].ToString(),
                ProductName = reader["ProductName"].ToString()!,
                Barcode = reader["Barcode"].ToString()!,
                CategoryId = Convert.ToInt64(reader["CategoryId"]),
                SubCategoryId = Convert.ToInt64(reader["SubCategoryId"]),
                HSNCode = reader["HSNCode"] == DBNull.Value ? null : reader["HSNCode"].ToString(),
                Description = reader["Description"] == DBNull.Value ? null : reader["Description"].ToString(),
                Size = reader["Size"] == DBNull.Value ? null : reader["Size"].ToString(),
                Colour = reader["Colour"] == DBNull.Value ? null : reader["Colour"].ToString(),
                Batch = reader["Batch"] == DBNull.Value ? null : reader["Batch"].ToString(),
                ExpDate = reader["ExpDate"] == DBNull.Value ? null : Convert.ToDateTime(reader["ExpDate"]),
                CGST = Convert.ToDouble(reader["CGST"]),
                SGST = Convert.ToDouble(reader["SGST"]),
            };
        }

        // ─── GET BY PRODUCT CODE ───────────────────────────────────────────────
        public MProducts? GetByProductCode(string code)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();

            using var cmd = new MySqlCommand(
                "SELECT * FROM MProducts WHERE ProductCode = @Code LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@Code", code);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            return new MProducts
            {
                Id = Convert.ToInt64(reader["Id"]),
                ProductCode = reader["ProductCode"] == DBNull.Value ? null : reader["ProductCode"].ToString(),
                ProductName = reader["ProductName"].ToString()!,
                Barcode = reader["Barcode"].ToString()!,
                CategoryId = Convert.ToInt64(reader["CategoryId"]),
                SubCategoryId = Convert.ToInt64(reader["SubCategoryId"]),
                HSNCode = reader["HSNCode"] == DBNull.Value ? null : reader["HSNCode"].ToString(),
                Description = reader["Description"] == DBNull.Value ? null : reader["Description"].ToString(),
                Size = reader["Size"] == DBNull.Value ? null : reader["Size"].ToString(),
                Colour = reader["Colour"] == DBNull.Value ? null : reader["Colour"].ToString(),
                CGST = Convert.ToDouble(reader["CGST"]),
                SGST = Convert.ToDouble(reader["SGST"]),
            };
        }

        // ─── REDUCE STOCK ──────────────────────────────────────────────────────
        public (bool Success, long RemainingQty, string? Error) ReduceStock(
            string barcode, long quantity)
        {
            if (string.IsNullOrEmpty(barcode) || quantity <= 0)
                return (false, 0, "Invalid barcode or quantity.");

            using var conn = new MySqlConnection(Con);
            conn.Open();
            using var trans = conn.BeginTransaction();
            try
            {
                long currentQty = 0;
                object? rowId = null;

                using (var cmdSelect = new MySqlCommand(
                    "SELECT Id, Quantity FROM ProductQuantity WHERE Barcode = @Barcode LIMIT 1 FOR UPDATE",
                    conn, trans))
                {
                    cmdSelect.Parameters.AddWithValue("@Barcode", barcode);
                    using var reader = cmdSelect.ExecuteReader();
                    if (!reader.Read())
                        return (false, 0, "Product not found in stock.");

                    rowId = reader["Id"];
                    currentQty = Convert.ToInt64(reader["Quantity"]);
                }

                if (currentQty < quantity)
                    return (false, currentQty, $"Insufficient stock. Only {currentQty} left.");

                using var cmdUpdate = new MySqlCommand(@"
                    UPDATE ProductQuantity
                    SET    Quantity     = Quantity - @Qty,
                           ModifiedDate = @ModifiedDate,
                           ModifiedBy   = @ModifiedBy
                    WHERE  Id           = @Id", conn, trans);

                cmdUpdate.Parameters.AddWithValue("@Qty", quantity);
                cmdUpdate.Parameters.AddWithValue("@modifiedDate", DateTime.Now);
                cmdUpdate.Parameters.AddWithValue("@modifiedBy", "WPFUser");
                cmdUpdate.Parameters.AddWithValue("@Id", rowId);
                cmdUpdate.ExecuteNonQuery();

                trans.Commit();
                return (true, currentQty - quantity, null);
            }
            catch (Exception ex)
            {
                trans.Rollback();
                return (false, 0, ex.Message);
            }
        }
    }
}