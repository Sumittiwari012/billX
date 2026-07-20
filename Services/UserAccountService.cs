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
                UserName,
                UserTypeId,
                MobileNumber,
                CreatedBy,
                CreatedDate,
                ModifiedBy,
                ModifiedDate
            ) 
            VALUES (
                @UserName,
                @UserTypeId,
                @MobileNumber,
                @CreatedBy,
                @CreatedDate,
                @ModifiedBy,
                @ModifiedDate
            )";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserName", u.UserName);
            cmd.Parameters.AddWithValue("@UserTypeId", u.UserTypeId);
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
                u.Id, 
                u.UserName, 
                u.UserTypeId, 
                u.MobileNumber,
                t.UserTypeName
            FROM MUser u
            INNER JOIN MUserType t ON u.UserTypeId = t.Id";
            var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new MUser
                {
                    Id = reader.GetInt64("Id"),
                    UserName = reader.GetString("UserName"),
                    UserTypeId = reader.GetInt64("UserTypeId"),
                    MobileNumber = reader.GetInt64("MobileNumber"),
                    UserTypeName = reader.GetString("UserTypeName"),
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
                MobileNumber = @MobileNumber,
                ModifiedBy = @ModifiedBy,
                ModifiedDate = CURRENT_TIMESTAMP
            WHERE Id = @Id";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", u.Id);
            cmd.Parameters.AddWithValue("@UserName", u.UserName);
            cmd.Parameters.AddWithValue("@UserTypeId", u.UserTypeId);
            cmd.Parameters.AddWithValue("@MobileNumber", u.MobileNumber);
            cmd.Parameters.AddWithValue("@ModifiedBy", "ADMIN");
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool DeleteUser(long Id)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"DELETE FROM MUser WHERE Id = @Id";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", Id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }
}