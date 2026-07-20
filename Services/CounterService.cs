using MySql.Data.MySqlClient;
using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;

namespace MyWPFCRUDApp.Services
{
    public class CounterService
    {
        private string Con => DatabaseHelper.ConnectionString;

        public bool InsertCounter(MCounter c)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"INSERT INTO MCounter (
                CounterName,
                UserId,
                Password,
                
                CreatedBy,
                CreatedDate,
                ModifiedBy,
                ModifiedDate
            ) 
            VALUES (
                @CounterName,
                @UserId,
                @Password,
                
                @CreatedBy,
                @CreatedDate,
                @ModifiedBy,
                @ModifiedDate
            )";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@CounterName", c.CounterName);
            cmd.Parameters.AddWithValue("@UserId", c.UserId);
            cmd.Parameters.AddWithValue("@Password", c.Password);
            
            cmd.Parameters.AddWithValue("@CreatedBy", "ADMIN");
            cmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
            cmd.Parameters.AddWithValue("@ModifiedBy", "ADMIN");
            cmd.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);
            return cmd.ExecuteNonQuery() > 0;
        }

        public List<MCounter> GetCounters()
        {
            var list = new List<MCounter>();
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"SELECT 
                c.Id, 
                c.CounterName, 
                c.UserId, 
                c.Password,
                
                u.UserName
            FROM MCounter c
            INNER JOIN MUser u ON c.UserId = u.Id";
            var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new MCounter
                {
                    Id = reader.GetInt64("Id"),
                    CounterName = reader.GetString("CounterName"),
                    UserId = reader.GetInt64("UserId"),
                    Password = reader.GetString("Password"),
                    
                    UserName = reader.GetString("UserName"),
                });
            }
            return list;
        }

        public bool UpdateCounter(MCounter c)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"UPDATE MCounter SET 
                CounterName = @CounterName,
                UserId = @UserId,
                Password = @Password,
               
                ModifiedBy = @ModifiedBy,
                ModifiedDate = CURRENT_TIMESTAMP
            WHERE Id = @Id";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", c.Id);
            cmd.Parameters.AddWithValue("@CounterName", c.CounterName);
            cmd.Parameters.AddWithValue("@UserId", c.UserId);
            cmd.Parameters.AddWithValue("@Password", c.Password);
            
            cmd.Parameters.AddWithValue("@ModifiedBy", "ADMIN");
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool DeleteCounter(long Id)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"DELETE FROM MCounter WHERE Id = @Id";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", Id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }
}