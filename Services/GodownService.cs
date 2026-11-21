using MyWPFCRUDApp.Models;
using MySql.Data.MySqlClient;

namespace MyWPFCRUDApp.Services
{
    public static class GodownService
    {
        public static List<Godown> GetAll()
        {
            var list = new List<Godown>();
            using var conn = new MySqlConnection(DatabaseHelper.ConnectionString);
            conn.Open();
            using var cmd = new MySqlCommand(
                "SELECT Id, GodownName, Address, IsActive FROM MGodown ORDER BY GodownName", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new Godown
                {
                    Id = r.GetInt64("Id"),
                    GodownName = r.GetString("GodownName"),
                    Address = r.IsDBNull(r.GetOrdinal("Address")) ? null : r.GetString("Address"),
                    IsActive = r.GetBoolean("IsActive")
                });
            }
            return list;
        }

        public static long Insert(Godown g)
        {
            using var conn = new MySqlConnection(DatabaseHelper.ConnectionString);
            conn.Open();
            using var cmd = new MySqlCommand(
                @"INSERT INTO MGodown (GodownName, Address, IsActive)
                  VALUES (@name, @addr, @active);
                  SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@name", g.GodownName);
            cmd.Parameters.AddWithValue("@addr", (object?)g.Address ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@active", g.IsActive);
            return Convert.ToInt64(cmd.ExecuteScalar());
        }

        public static void Update(Godown g)
        {
            using var conn = new MySqlConnection(DatabaseHelper.ConnectionString);
            conn.Open();
            using var cmd = new MySqlCommand(
                @"UPDATE MGodown SET GodownName=@name, Address=@addr, IsActive=@active
                  WHERE Id=@id", conn);
            cmd.Parameters.AddWithValue("@id", g.Id);
            cmd.Parameters.AddWithValue("@name", g.GodownName);
            cmd.Parameters.AddWithValue("@addr", (object?)g.Address ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@active", g.IsActive);
            cmd.ExecuteNonQuery();
        }

        // Stores linked to this godown get GodownId = NULL (ON DELETE SET NULL)
        public static void Delete(long id)
        {
            using var conn = new MySqlConnection(DatabaseHelper.ConnectionString);
            conn.Open();
            using var cmd = new MySqlCommand("DELETE FROM MGodown WHERE Id=@id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
