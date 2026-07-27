using MySql.Data.MySqlClient;
using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;

namespace MyWPFCRUDApp.Services
{
    // CRUD for MCounterUser — the users (and their passwords) assigned to a counter.
    public class CounterUserService
    {
        private string Con => DatabaseHelper.ConnectionString;

        public bool InsertCounterUser(MCounterUser cu)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"INSERT INTO MCounterUser (
                CounterId,
                UserId,
                Password,
                CreatedBy,
                CreatedDate,
                ModifiedBy,
                ModifiedDate
            )
            VALUES (
                @CounterId,
                @UserId,
                @Password,
                @CreatedBy,
                @CreatedDate,
                @ModifiedBy,
                @ModifiedDate
            )";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@CounterId", cu.CounterId);
            cmd.Parameters.AddWithValue("@UserId", cu.UserId);
            cmd.Parameters.AddWithValue("@Password", cu.Password);
            cmd.Parameters.AddWithValue("@CreatedBy", "ADMIN");
            cmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
            cmd.Parameters.AddWithValue("@ModifiedBy", "ADMIN");
            cmd.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);
            return cmd.ExecuteNonQuery() > 0;
        }

        public List<MCounterUser> GetCounterUsers(long counterId)
        {
            var list = new List<MCounterUser>();
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"SELECT
                cu.Id,
                cu.CounterId,
                cu.UserId,
                cu.Password,
                u.UserName
            FROM MCounterUser cu
            INNER JOIN MUser u ON cu.UserId = u.Id
            WHERE cu.CounterId = @CounterId
            ORDER BY u.UserName";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@CounterId", counterId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new MCounterUser
                {
                    Id = reader.GetInt64("Id"),
                    CounterId = reader.GetInt64("CounterId"),
                    UserId = reader.GetInt64("UserId"),
                    Password = reader.GetString("Password"),
                    UserName = reader.GetString("UserName"),
                });
            }
            return list;
        }

        public bool UpdateCounterUser(MCounterUser cu)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"UPDATE MCounterUser SET
                UserId = @UserId,
                Password = @Password,
                ModifiedBy = @ModifiedBy,
                ModifiedDate = CURRENT_TIMESTAMP
            WHERE Id = @Id";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", cu.Id);
            cmd.Parameters.AddWithValue("@UserId", cu.UserId);
            cmd.Parameters.AddWithValue("@Password", cu.Password);
            cmd.Parameters.AddWithValue("@ModifiedBy", "ADMIN");
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool DeleteCounterUser(long id)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"DELETE FROM MCounterUser WHERE Id = @Id";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            return cmd.ExecuteNonQuery() > 0;
        }

        // Stops the same user being assigned to the same counter twice.
        // Pass the current row's Id as excludeId when checking during an update.
        public bool ExistsForCounterAndUser(long counterId, long userId, long excludeId = 0)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"SELECT COUNT(*) FROM MCounterUser
                WHERE CounterId = @CounterId AND UserId = @UserId AND Id <> @ExcludeId";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@CounterId", counterId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@ExcludeId", excludeId);
            return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
        }
    }
}
