using MySql.Data.MySqlClient;
using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;

namespace MyWPFCRUDApp.Services
{
    public class UserAccountService
    {
        private string Con => DatabaseHelper.ConnectionString;

        public bool InsertUser(MUser u)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"INSERT INTO MUser (
                UserName, UserTypeId, StoreId, MobileNumber,
                CreatedBy, CreatedDate, ModifiedBy, ModifiedDate)
            VALUES (
                @UserName, @UserTypeId, @StoreId, @MobileNumber,
                @CreatedBy, @CreatedDate, @ModifiedBy, @ModifiedDate)";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserName", u.UserName);
            cmd.Parameters.AddWithValue("@UserTypeId", u.UserTypeId);
            cmd.Parameters.AddWithValue("@StoreId", (object)u.StoreId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MobileNumber", u.MobileNumber);
            cmd.Parameters.AddWithValue("@CreatedBy", "ADMIN");
            cmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
            cmd.Parameters.AddWithValue("@ModifiedBy", "ADMIN");
            cmd.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);
            return cmd.ExecuteNonQuery() > 0;
        }

        public List<MUser> GetUsers()
        {
            var list = new List<MUser>();
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"SELECT
                u.Id, u.UserName, u.UserTypeId, u.StoreId, u.MobileNumber,
                t.UserTypeName, s.StoreName
            FROM MUser u
            INNER JOIN MUserType t ON u.UserTypeId = t.Id
            LEFT JOIN MStore s ON u.StoreId = s.Id
            ORDER BY u.UserName, s.StoreName";
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            int storeIdOrd = reader.GetOrdinal("StoreId");
            int storeNameOrd = reader.GetOrdinal("StoreName");
            while (reader.Read())
            {
                list.Add(new MUser
                {
                    Id = reader.GetInt64("Id"),
                    UserName = reader.GetString("UserName"),
                    UserTypeId = reader.GetInt64("UserTypeId"),
                    StoreId = reader.IsDBNull(storeIdOrd) ? (long?)null : reader.GetInt64(storeIdOrd),
                    StoreName = reader.IsDBNull(storeNameOrd) ? "" : reader.GetString(storeNameOrd),
                    MobileNumber = reader.GetInt64("MobileNumber"),
                    UserTypeName = reader.GetString("UserTypeName"),
                });
            }
            return list;
        }

        public List<Store> GetStores()
        {
            var list = new List<Store>();
            using var conn = new MySqlConnection(Con);
            conn.Open();
            using var cmd = new MySqlCommand(
                "SELECT Id, StoreName FROM MStore WHERE IsActive = 1 ORDER BY StoreName", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Store
                {
                    Id = reader.GetInt64("Id"),
                    StoreName = reader.GetString("StoreName")
                });
            }
            return list;
        }

        public bool UpdateUser(MUser u)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"UPDATE MUser SET
                UserName = @UserName,
                UserTypeId = @UserTypeId,
                StoreId = @StoreId,
                MobileNumber = @MobileNumber,
                ModifiedBy = @ModifiedBy,
                ModifiedDate = CURRENT_TIMESTAMP
            WHERE Id = @Id";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", u.Id);
            cmd.Parameters.AddWithValue("@UserName", u.UserName);
            cmd.Parameters.AddWithValue("@UserTypeId", u.UserTypeId);
            cmd.Parameters.AddWithValue("@StoreId", (object)u.StoreId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MobileNumber", u.MobileNumber);
            cmd.Parameters.AddWithValue("@ModifiedBy", "ADMIN");
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool DeleteUser(long Id)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            using var cmd = new MySqlCommand("DELETE FROM MUser WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", Id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }
}