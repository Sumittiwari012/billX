using MySql.Data.MySqlClient;
using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;

namespace MyWPFCRUDApp.Services
{
    // CRUD for the counter itself (MCounterNew). User/password assignment
    // now lives on MCounterUser — see CounterUserService.
    public class CounterService
    {
        private string Con => DatabaseHelper.ConnectionString;

        public bool InsertCounter(MCounterNew c)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"INSERT INTO MCounterNew (
                CounterName,
                CreatedBy,
                CreatedDate,
                ModifiedBy,
                ModifiedDate
            )
            VALUES (
                @CounterName,
                @CreatedBy,
                @CreatedDate,
                @ModifiedBy,
                @ModifiedDate
            )";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@CounterName", c.CounterName);
            cmd.Parameters.AddWithValue("@CreatedBy", "ADMIN");
            cmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
            cmd.Parameters.AddWithValue("@ModifiedBy", "ADMIN");
            cmd.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);
            return cmd.ExecuteNonQuery() > 0;
        }

        public List<MCounterNew> GetCounters()
        {
            var list = new List<MCounterNew>();
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"SELECT Id, CounterName FROM MCounterNew ORDER BY CounterName";
            var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new MCounterNew
                {
                    Id = reader.GetInt64("Id"),
                    CounterName = reader.GetString("CounterName"),
                });
            }
            return list;
        }

        public bool UpdateCounter(MCounterNew c)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"UPDATE MCounterNew SET
                CounterName = @CounterName,
                ModifiedBy = @ModifiedBy,
                ModifiedDate = CURRENT_TIMESTAMP
            WHERE Id = @Id";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", c.Id);
            cmd.Parameters.AddWithValue("@CounterName", c.CounterName);
            cmd.Parameters.AddWithValue("@ModifiedBy", "ADMIN");
            return cmd.ExecuteNonQuery() > 0;
        }

        // MCounterUser rows for this counter are removed automatically
        // (FK_MCounterUser_MCounterNew has ON DELETE CASCADE).
        public bool DeleteCounter(long id)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"DELETE FROM MCounterNew WHERE Id = @Id";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }
}