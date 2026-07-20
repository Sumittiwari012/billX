using MySql.Data.MySqlClient;
using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;

namespace MyWPFCRUDApp.Services
{
    public class PettyCashService
    {
        private string Con => DatabaseHelper.ConnectionString;

        // Accept always starts as Pending (0) here — the separate counter
        // system is solely responsible for setting it true/false later.
        public bool InsertPettyCash(MPettyCash p)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"INSERT INTO MPettyCash (
                PettyCash,
                CounterId,
                Date,
                Accepted,
                CreatedBy,
                CreatedDate,
                ModifiedBy,
                ModifiedDate
            ) 
            VALUES (
                @PettyCash,
                @CounterId,
                @Date,
                0,
                @CreatedBy,
                @CreatedDate,
                @ModifiedBy,
                @ModifiedDate
            )";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@PettyCash", p.PettyCash);
            cmd.Parameters.AddWithValue("@CounterId", p.CounterId);
            cmd.Parameters.AddWithValue("@Date", p.Date?.Date ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedBy", "ADMIN");
            cmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
            cmd.Parameters.AddWithValue("@ModifiedBy", "ADMIN");
            cmd.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);
            return cmd.ExecuteNonQuery() > 0;
        }

        public List<MPettyCash> GetPettyCash()
        {
            var list = new List<MPettyCash>();
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"SELECT 
                p.Id, 
                p.PettyCash, 
                p.CounterId, 
                p.Date,
                p.Accepted,
                c.CounterName
            FROM MPettyCash p
            INNER JOIN MCounter c ON p.CounterId = c.Id
            ORDER BY p.Date DESC, p.Id DESC";
            var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new MPettyCash
                {
                    Id = reader.GetInt64("Id"),
                    PettyCash = reader.GetDecimal("PettyCash"),
                    CounterId = reader.GetInt64("CounterId"),
                    Date = reader.IsDBNull(reader.GetOrdinal("Date")) ? (DateTime?)null : reader.GetDateTime("Date"),
                    Accept = reader.GetBoolean("Accepted"),
                    CounterName = reader.GetString("CounterName"),
                });
            }
            return list;
        }

        // Accept is excluded here too — this app never modifies it, only
        // reads and displays whatever the counter system has set.
        public bool UpdatePettyCash(MPettyCash p)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"UPDATE MPettyCash SET 
                PettyCash = @PettyCash,
                CounterId = @CounterId,
                Date = @Date,
                ModifiedBy = @ModifiedBy,
                ModifiedDate = CURRENT_TIMESTAMP
            WHERE Id = @Id";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", p.Id);
            cmd.Parameters.AddWithValue("@PettyCash", p.PettyCash);
            cmd.Parameters.AddWithValue("@CounterId", p.CounterId);
            cmd.Parameters.AddWithValue("@Date", p.Date?.Date ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ModifiedBy", "ADMIN");
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool DeletePettyCash(long Id)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"DELETE FROM MPettyCash WHERE Id = @Id";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", Id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }
}