using MySql.Data.MySqlClient;
using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWPFCRUDApp.Services
{
    public class CategoryService
    {
        private string Con => DatabaseHelper.ConnectionString;

        public bool InsertCategory(MCategory c)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"INSERT INTO MCategory (
                CategoryName
            ) 
            VALUES (
                @CategoryName
            )";

            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@CategoryName", c.CategoryName);
            cmd.Parameters.AddWithValue("@createdDate", DateTime.Now);
            cmd.Parameters.AddWithValue("@modifiedDate", DateTime.Now);
            cmd.Parameters.AddWithValue("@createdBy", "ADMIN");
            cmd.Parameters.AddWithValue("@modifiedBy", "");

            return cmd.ExecuteNonQuery() > 0;
        }
        public List<MCategory> GetCategory()
        {
            var list = new List<MCategory>();
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var cmd = new MySqlCommand("SELECT * FROM MCategory", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new MCategory
                {
                    Id = reader.GetInt32("Id"),
                    CategoryName = reader.GetString("CategoryName"),
                });
            }
            return list;
        }
        public bool UpdateCategory(MCategory c)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"UPDATE MCategory SET 
                CategoryName = @CategoryName, 
                
                ModifiedBy = @ModifiedBy,
                ModifiedDate = CURRENT_TIMESTAMP
            WHERE Id = @Id";

            var cmd = new MySqlCommand(sql, conn);

            // Primary Key for WHERE clause
            cmd.Parameters.AddWithValue("@Id", c.Id);

            // Basic Company Details
            cmd.Parameters.AddWithValue("@CategoryName", c.CategoryName);

            cmd.Parameters.AddWithValue("@ModifiedBy", "ADMIN");

            return cmd.ExecuteNonQuery() > 0;
        }
        public bool DeleteCategory(long Id)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"DELETE FROM MCategory WHERE Id = @Id";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", Id);
            
            return cmd.ExecuteNonQuery() > 0;
        }
    }
}
