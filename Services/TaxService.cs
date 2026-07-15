using MySql.Data.MySqlClient;
using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPFCRUDApp.Models;

namespace MyWPFCRUDApp.Services
{
    public class TaxService
    {
        private string Con => DatabaseHelper.ConnectionString;

        public bool InsertTax(MTaxCategory c)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"INSERT INTO MTaxCategory
(
    CGST,
    SGST,
    IGST
)
VALUES
(
    @CGST,
    @SGST,
    @IGST
)";

            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@CGST", c.CGST);
            cmd.Parameters.AddWithValue("@SGST", c.SGST);
            cmd.Parameters.AddWithValue("@IGST", c.IGST);
            cmd.Parameters.AddWithValue("@createdDate", DateTime.Now);
            cmd.Parameters.AddWithValue("@modifiedDate", DateTime.Now);
            cmd.Parameters.AddWithValue("@createdBy", "ADMIN");
            cmd.Parameters.AddWithValue("@modifiedBy", "");

            return cmd.ExecuteNonQuery() > 0;
        }
        public List<MTaxCategory> GetTaxCategory()
        {
            var list = new List<MTaxCategory>();
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var cmd = new MySqlCommand("SELECT * FROM MTaxCategory", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new MTaxCategory
                {
                    Id = reader.GetInt32("Id"),
                    CGST = reader.GetDecimal("CGST"),
                    SGST = reader.GetDecimal("SGST"),
                    IGST = reader.GetDecimal("IGST")
                });
            }
            return list;
        }
        public bool UpdateTaxCategory(MTaxCategory c)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"UPDATE MTaxCategory SET 
               
CGST=@CGST,
SGST=@SGST,
IGST=@IGST
            WHERE Id = @Id";

            var cmd = new MySqlCommand(sql, conn);

            // Primary Key for WHERE clause
            cmd.Parameters.AddWithValue("@Id", c.Id);

            // Basic Company Details
            cmd.Parameters.AddWithValue("@CGST", c.CGST);
            cmd.Parameters.AddWithValue("@SGST", c.SGST);
            cmd.Parameters.AddWithValue("@IGST", c.IGST);
            cmd.Parameters.AddWithValue("@ModifiedBy", "ADMIN");

            return cmd.ExecuteNonQuery() > 0;
        }
        public bool DeleteTaxCategory(long Id)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"DELETE FROM MTaxCategory WHERE Id = @Id";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", Id);

            return cmd.ExecuteNonQuery() > 0;
        }
    }
}
