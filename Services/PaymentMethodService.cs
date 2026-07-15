using MySql.Data.MySqlClient;
using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;

namespace MyWPFCRUDApp.Services
{
    public class PaymentMethodService
    {
        private string Con => DatabaseHelper.ConnectionString;

        public bool Insert(MPaymentMethod c)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"INSERT INTO MPaymentMethod (PaymentMethod, CreatedBy, CreatedDate, ModifiedBy, ModifiedDate)
                        VALUES (@PaymentMethod, 'ADMIN', @CreatedDate, '', @ModifiedDate)";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@PaymentMethod", c.PaymentMethod);
            cmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
            cmd.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);
            return cmd.ExecuteNonQuery() > 0;
        }

        public List<MPaymentMethod> GetAll()
        {
            var list = new List<MPaymentMethod>();
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var cmd = new MySqlCommand("SELECT * FROM MPaymentMethod", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new MPaymentMethod
                {
                    Id = reader.GetInt64("Id"),
                    PaymentMethod = reader.GetString("PaymentMethod"),
                });
            }
            return list;
        }

        public bool Update(MPaymentMethod c)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"UPDATE MPaymentMethod SET
                            PaymentMethod = @PaymentMethod,
                            ModifiedBy    = 'ADMIN',
                            ModifiedDate  = CURRENT_TIMESTAMP
                        WHERE Id = @Id";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", c.Id);
            cmd.Parameters.AddWithValue("@PaymentMethod", c.PaymentMethod);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool Delete(long id)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var cmd = new MySqlCommand("DELETE FROM MPaymentMethod WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }
}