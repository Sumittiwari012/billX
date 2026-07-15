using MySql.Data.MySqlClient;
using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWPFCRUDApp.Services
{
    public class BankAccountService
    {
        private string Con => DatabaseHelper.ConnectionString;

        public bool InsertAccountNumber(MBankAccountMaster c)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"INSERT INTO MBankAccountMaster (
                AccountNumber
            ) 
            VALUES (
                @AccountNumber
            )";

            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@AccountNumber", c.AccountNumber);
            cmd.Parameters.AddWithValue("@createdDate", DateTime.Now);
            cmd.Parameters.AddWithValue("@modifiedDate", DateTime.Now);
            cmd.Parameters.AddWithValue("@createdBy", "ADMIN");
            cmd.Parameters.AddWithValue("@modifiedBy", "");

            return cmd.ExecuteNonQuery() > 0;
        }
        public List<MBankAccountMaster> GetAccountNumber()
        {
            var list = new List<MBankAccountMaster>();
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var cmd = new MySqlCommand("SELECT * FROM MBankAccountMaster", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new MBankAccountMaster
                {
                    Id = reader.GetInt32("Id"),
                    AccountNumber = reader.GetString("AccountNumber"),
                });
            }
            return list;
        }
        public bool UpdateUnit(MBankAccountMaster c)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"UPDATE MBankAccountMaster SET 
                AccountNumber = @AccountNumber, 
                
                ModifiedBy = @ModifiedBy,
                ModifiedDate = CURRENT_TIMESTAMP
            WHERE Id = @Id";

            var cmd = new MySqlCommand(sql, conn);

            // Primary Key for WHERE clause
            cmd.Parameters.AddWithValue("@Id", c.Id);

            // Basic Company Details
            cmd.Parameters.AddWithValue("@AccountNumber", c.AccountNumber);

            cmd.Parameters.AddWithValue("@ModifiedBy", "ADMIN");

            return cmd.ExecuteNonQuery() > 0;
        }
        public bool DeleteUnit(long Id)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"DELETE FROM MBankAccountMaster WHERE Id = @Id";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", Id);

            return cmd.ExecuteNonQuery() > 0;
        }
    }
}
