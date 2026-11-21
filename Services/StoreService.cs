using MyWPFCRUDApp.Models;
using MySql.Data.MySqlClient;

namespace MyWPFCRUDApp.Services
{
    public static class StoreService
    {
        public static List<Store> GetAll()
        {
            var list = new List<Store>();
            using var conn = new MySqlConnection(DatabaseHelper.ConnectionString);
            conn.Open();
            using var cmd = new MySqlCommand(
                @"SELECT s.Id, s.StoreName, s.GodownId, g.GodownName, s.Address, s.IsActive
                  FROM MStore s
                  LEFT JOIN MGodown g ON g.Id = s.GodownId
                  ORDER BY s.StoreName", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                int gi = r.GetOrdinal("GodownId");
                int gn = r.GetOrdinal("GodownName");
                int ad = r.GetOrdinal("Address");
                list.Add(new Store
                {
                    Id = r.GetInt64("Id"),
                    StoreName = r.GetString("StoreName"),
                    GodownId = r.IsDBNull(gi) ? null : r.GetInt64(gi),
                    GodownName = r.IsDBNull(gn) ? null : r.GetString(gn),
                    Address = r.IsDBNull(ad) ? null : r.GetString(ad),
                    IsActive = r.GetBoolean("IsActive")
                });
            }
            return list;
        }

        public static long Insert(Store s)
        {
            using var conn = new MySqlConnection(DatabaseHelper.ConnectionString);
            conn.Open();
            using var cmd = new MySqlCommand(
                @"INSERT INTO MStore (StoreName, GodownId, Address, IsActive)
                  VALUES (@name, @gid, @addr, @active);
                  SELECT LAST_INSERT_ID();", conn);
            Bind(cmd, s);
            return Convert.ToInt64(cmd.ExecuteScalar());
        }

        public static void Update(Store s)
        {
            using var conn = new MySqlConnection(DatabaseHelper.ConnectionString);
            conn.Open();
            using var cmd = new MySqlCommand(
                @"UPDATE MStore SET StoreName=@name, GodownId=@gid, Address=@addr, IsActive=@active
                  WHERE Id=@id", conn);
            Bind(cmd, s);
            cmd.Parameters.AddWithValue("@id", s.Id);
            cmd.ExecuteNonQuery();
        }

        public static void Delete(long id)
        {
            using var conn = new MySqlConnection(DatabaseHelper.ConnectionString);
            conn.Open();
            using var cmd = new MySqlCommand("DELETE FROM MStore WHERE Id=@id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static void Bind(MySqlCommand cmd, Store s)
        {
            cmd.Parameters.AddWithValue("@name", s.StoreName);
            cmd.Parameters.AddWithValue("@gid", (object?)s.GodownId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@addr", (object?)s.Address ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@active", s.IsActive);
        }
    }
}
