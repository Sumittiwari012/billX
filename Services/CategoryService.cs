using MySql.Data.MySqlClient;
using MyWPFCRUDApp.Models;

namespace MyWPFCRUDApp.Services
{
    public class CategoryService
    {
        private string Con => DatabaseHelper.ConnectionString;

        // ─── CREATE ───────────────────────────────────────────────

        public bool InsertCategory(MCategory c)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var cmd = new MySqlCommand(
                "INSERT INTO MCategory (CategoryName) VALUES (@name)", conn);
            cmd.Parameters.AddWithValue("@name", c.CategoryName);
            
            return cmd.ExecuteNonQuery() > 0;
        }

        // ─── READ ─────────────────────────────────────────────────
        public List<MCategory> GetAllCategory()
        {
            var list = new List<MCategory>();
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var cmd = new MySqlCommand("SELECT Id, CategoryName FROM MCategory", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new MCategory
                {
                    Id = reader.GetInt64("Id"),
                    CategoryName = reader.GetString("CategoryName"),
                    
                });
            }
            return list;
        }

        // ─── UPDATE ───────────────────────────────────────────────
        public bool UpdateCategory(MCategory c)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var cmd = new MySqlCommand(
                "UPDATE MCategory SET CategoryName=@name WHERE Id=@id", conn);
            cmd.Parameters.AddWithValue("@name", c.CategoryName);
            cmd.Parameters.AddWithValue("@id", c.Id);

            return cmd.ExecuteNonQuery() > 0;
        }

        // ─── DELETE ───────────────────────────────────────────────
        public bool DeleteCategory(long id)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var cmd = new MySqlCommand("DELETE FROM MCategory WHERE Id=@id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }
}
