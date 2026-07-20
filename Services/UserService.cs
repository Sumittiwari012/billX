using MySql.Data.MySqlClient;
using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWPFCRUDApp.Services
{
    public class UserService
    {
        private string Con => DatabaseHelper.ConnectionString;

        public bool InsertUser(MUserType c)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"INSERT INTO MUserType (
                UserTypeName,
                CreatedBy,
                CreatedDate,
                ModifiedBy,
                ModifiedDate
            ) 
            VALUES (
                @UserTypeName,
                @createdBy,
                @createdDate,
                @modifiedBy,
                @modifiedDate
            )";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserTypeName", c.UserTypeName);
            cmd.Parameters.AddWithValue("@createdDate", DateTime.Now);
            cmd.Parameters.AddWithValue("@modifiedDate", DateTime.Now);
            cmd.Parameters.AddWithValue("@createdBy", "ADMIN");
            cmd.Parameters.AddWithValue("@modifiedBy", "");
            return cmd.ExecuteNonQuery() > 0;
        }

        public List<MUserType> GetUserType()
        {
            var list = new List<MUserType>();
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var cmd = new MySqlCommand("SELECT * FROM MUserType", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new MUserType
                {
                    Id = reader.GetInt32("Id"),
                    UserTypeName = reader.GetString("UserTypeName"),
                });
            }
            return list;
        }

        public bool UpdateUserType(MUserType c)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"UPDATE MUserType SET 
                UserTypeName = @UserTypeName, 
                ModifiedBy = @ModifiedBy,
                ModifiedDate = CURRENT_TIMESTAMP
            WHERE Id = @Id";
            var cmd = new MySqlCommand(sql, conn);
            // Primary Key for WHERE clause
            cmd.Parameters.AddWithValue("@Id", c.Id);
            // Basic Details
            cmd.Parameters.AddWithValue("@UserTypeName", c.UserTypeName);
            cmd.Parameters.AddWithValue("@ModifiedBy", "ADMIN");
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool DeleteUserType(long Id)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"DELETE FROM MUserType WHERE Id = @Id";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", Id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }
}