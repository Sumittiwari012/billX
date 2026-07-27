using MySql.Data.MySqlClient;
using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;

namespace MyWPFCRUDApp.Services
{
    public class LoginLogoutService
    {
        private string Con => DatabaseHelper.ConnectionString;

        public bool InsertLoginLogout(MLoginLogout l)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"INSERT INTO MLoginLogout (
        CounterId,
        UserId,
        loginTime,
        logoutTime,
        CreatedBy,
        CreatedDate,
        ModifiedBy,
        ModifiedDate
    ) 
    VALUES (
        @CounterId,
        @UserId,
        @LoginTime,
        @LogoutTime,
        @CreatedBy,
        @CreatedDate,
        @ModifiedBy,
        @ModifiedDate
    )";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@CounterId", l.CounterId);
            cmd.Parameters.AddWithValue("@UserId", l.UserId);
            cmd.Parameters.AddWithValue("@LoginTime", l.LoginTime == default ? DateTime.Now : l.LoginTime);
            cmd.Parameters.AddWithValue("@LogoutTime", (object)l.LogoutTime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedBy", "ADMIN");
            cmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
            cmd.Parameters.AddWithValue("@ModifiedBy", "ADMIN");
            cmd.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);
            return cmd.ExecuteNonQuery() > 0;
        }

        public List<MLoginLogout> GetLoginLogouts()
        {
            var list = new List<MLoginLogout>();
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"SELECT 
                l.Id,
                l.CounterId,
                l.UserId,
                l.loginTime,
                l.logoutTime,
                l.Settlement,
                c.CounterName,
                u.UserName
            FROM MLoginLogout l
            INNER JOIN MCounterNew c ON l.CounterId = c.Id
            INNER JOIN MUser u ON l.UserId = u.Id
            ORDER BY l.Id DESC";
            var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new MLoginLogout
                {
                    Id = reader.GetInt64("Id"),
                    CounterId = reader.GetInt64("CounterId"),
                    UserId = reader.GetInt64("UserId"),
                    LoginTime = reader.GetDateTime("loginTime"),
                    LogoutTime = reader.IsDBNull(reader.GetOrdinal("logoutTime")) ? (DateTime?)null : reader.GetDateTime("logoutTime"),
                    Settlement = reader.GetBoolean("Settlement"),
                    CounterName = reader.GetString("CounterName"),
                    UserName = reader.GetString("UserName"),
                });
            }
            return list;
        }

        public bool UpdateLoginLogout(MLoginLogout l)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"UPDATE MLoginLogout SET 
        CounterId = @CounterId,
        UserId = @UserId,
        loginTime = @LoginTime,
        logoutTime = @LogoutTime,
        ModifiedBy = @ModifiedBy,
        ModifiedDate = CURRENT_TIMESTAMP
    WHERE Id = @Id";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", l.Id);
            cmd.Parameters.AddWithValue("@CounterId", l.CounterId);
            cmd.Parameters.AddWithValue("@UserId", l.UserId);
            cmd.Parameters.AddWithValue("@LoginTime", l.LoginTime);
            cmd.Parameters.AddWithValue("@LogoutTime", (object)l.LogoutTime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ModifiedBy", "ADMIN");
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool DeleteLoginLogout(long Id)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"DELETE FROM MLoginLogout WHERE Id = @Id";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", Id);
            return cmd.ExecuteNonQuery() > 0;
        }

        // Support lookup for the CounterId dropdown.
        // MCounterNew no longer has a UserId column (that moved to
        // MCounterUser, since a counter can now have multiple users).
        public List<MCounterNew> GetCounters()
        {
            var list = new List<MCounterNew>();
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var cmd = new MySqlCommand("SELECT Id, CounterName FROM MCounterNew", conn);
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

        public List<MUser> GetUsers()
        {
            var list = new List<MUser>();
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var cmd = new MySqlCommand("SELECT Id, UserName FROM MUser", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new MUser
                {
                    Id = reader.GetInt64("Id"),
                    UserName = reader.GetString("UserName"),
                });
            }
            return list;
        }
    }
}
