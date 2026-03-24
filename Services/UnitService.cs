using MySql.Data.MySqlClient;
using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWPFCRUDApp.Services
{
    public class UnitService
    {
        private string Con => DatabaseHelper.ConnectionString;

        public bool InsertUnit(MUnit c)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"INSERT INTO MUnit (
                UnitName
            ) 
            VALUES (
                @UnitName
            )";

            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UnitName", c.UnitName);
            cmd.Parameters.AddWithValue("@createdDate", DateTime.Now);
            cmd.Parameters.AddWithValue("@modifiedDate", DateTime.Now);
            cmd.Parameters.AddWithValue("@createdBy", "ADMIN");
            cmd.Parameters.AddWithValue("@modifiedBy", "");

            return cmd.ExecuteNonQuery() > 0;
        }
        public List<MUnit> GetUnit()
        {
            var list = new List<MUnit>();
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var cmd = new MySqlCommand("SELECT * FROM MUnit", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new MUnit
                {
                    Id = reader.GetInt32("Id"),
                    UnitName = reader.GetString("UnitName"),
                });
            }
            return list;
        }
        public bool UpdateUnit(MUnit c)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"UPDATE MUnit SET 
                UnitName = @UnitName, 
                
                ModifiedBy = @ModifiedBy,
                ModifiedDate = CURRENT_TIMESTAMP
            WHERE Id = @Id";

            var cmd = new MySqlCommand(sql, conn);

            // Primary Key for WHERE clause
            cmd.Parameters.AddWithValue("@Id", c.Id);

            // Basic Company Details
            cmd.Parameters.AddWithValue("@UnitName", c.UnitName);

            cmd.Parameters.AddWithValue("@ModifiedBy", "ADMIN");

            return cmd.ExecuteNonQuery() > 0;
        }
        public bool DeleteUnit(long Id)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"DELETE FROM MUnit WHERE Id = @Id";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", Id);

            return cmd.ExecuteNonQuery() > 0;
        }
    }
}
