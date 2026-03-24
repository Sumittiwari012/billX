using MySql.Data.MySqlClient;
using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace MyWPFCRUDApp.Services
{
    public class SubCategoryService
    {
        private string Con => DatabaseHelper.ConnectionString;
        public class SubCategoryDisplayModel
        {
            public long Id { get; set; }
            public string SubCategoryName { get; set; }
            public string CategoryName { get; set; } // This is what the user sees
            public long CategoryId { get; set; }    // This is for logic/editing
        }
        public bool InsertSubCategory(MSubCategory c)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"INSERT INTO MSubCategory (
                SubCategoryName,CategoryId
            ) 
            VALUES (
                @SubCategoryName,@CategoryId
            )";

            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@SubCategoryName", c.SubCategoryName);
            cmd.Parameters.AddWithValue("@CategoryId", c.CategoryId);
            cmd.Parameters.AddWithValue("@createdDate", DateTime.Now);
            cmd.Parameters.AddWithValue("@modifiedDate", DateTime.Now);
            cmd.Parameters.AddWithValue("@createdBy", "ADMIN");
            cmd.Parameters.AddWithValue("@modifiedBy", "");

            return cmd.ExecuteNonQuery() > 0;
        }
        public List<SubCategoryDisplayModel> GetSubCategory()
        {
            var List = new List<SubCategoryDisplayModel>();
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"SELECT sc.*, c.CategoryName 
                FROM MSubCategory sc 
                JOIN MCategory c ON sc.CategoryId = c.Id";
            var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                List.Add(new SubCategoryDisplayModel
                {
                    Id = reader.GetInt32("Id"),
                    SubCategoryName = reader.GetString("SubCategoryName"),
                    CategoryId = reader.GetInt64("CategoryId"),
                    CategoryName= reader.GetString("CategoryName")
                });
            }
            return List;
        }
        public bool UpdateSubCategory(MSubCategory c)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"UPDATE MSubCategory SET 
                SubCategoryName = @SubCategoryName, 
                CategoryId = @CategoryId,
                ModifiedBy = @ModifiedBy,
                ModifiedDate = CURRENT_TIMESTAMP
            WHERE Id = @Id";

            var cmd = new MySqlCommand(sql, conn);

            // Primary Key for WHERE clause
            cmd.Parameters.AddWithValue("@Id", c.Id);

            // Basic Company Details
            cmd.Parameters.AddWithValue("@SubCategoryName", c.SubCategoryName);
            cmd.Parameters.AddWithValue("@CategoryId", c.CategoryId);

            cmd.Parameters.AddWithValue("@ModifiedBy", "ADMIN");

            return cmd.ExecuteNonQuery() > 0;
        }
        public bool DeleteSubCategory(long Id)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"DELETE FROM MSubCategory WHERE Id = @Id";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", Id);

            return cmd.ExecuteNonQuery() > 0;
        }
    }
}
