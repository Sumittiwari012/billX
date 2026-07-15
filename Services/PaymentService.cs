using MySql.Data.MySqlClient;
using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;

namespace MyWPFCRUDApp.Services
{
    public class PaymentService
    {
        private string Con => DatabaseHelper.ConnectionString;

        public bool InsertPayment(MPayment payment)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"INSERT INTO MPayment 
                        (SupplierId, InvoiceNumber, PaymentMethod, BankAccountNumber, AmountPaid, CreatedBy, CreatedDate, ModifiedBy, ModifiedDate)
                        VALUES 
                        (@SupplierId, @InvoiceNumber, @PaymentMethod, @BankAccountNumber, @AmountPaid, 'ADMIN', @Now, 'ADMIN', @Now)";
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@SupplierId", payment.SupplierId);
            cmd.Parameters.AddWithValue("@InvoiceNumber", payment.InvoiceNumber);
            cmd.Parameters.AddWithValue("@PaymentMethod", payment.PaymentMethod);
            cmd.Parameters.AddWithValue("@BankAccountNumber", payment.BankAccountNumber ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@AmountPaid", payment.AmountPaid);
            cmd.Parameters.AddWithValue("@Now", DateTime.Now);
            return cmd.ExecuteNonQuery() > 0;
        }

        public List<MPayment> GetByInvoice(string invoiceNumber)
        {
            var list = new List<MPayment>();
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var cmd = new MySqlCommand(
                @"SELECT Id, SupplierId, InvoiceNumber, PaymentMethod, 
                         BankAccountNumber, AmountPaid, CreatedDate
                  FROM MPayment 
                  WHERE InvoiceNumber = @InvoiceNumber
                  ORDER BY CreatedDate ASC", conn);
            cmd.Parameters.AddWithValue("@InvoiceNumber", invoiceNumber);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new MPayment
                {
                    Id = reader.GetInt64("Id"),
                    SupplierId = reader.GetInt64("SupplierId"),
                    InvoiceNumber = reader.GetString("InvoiceNumber"),
                    PaymentMethod = reader.GetString("PaymentMethod"),
                    BankAccountNumber = reader["BankAccountNumber"] == DBNull.Value ? null : reader.GetString("BankAccountNumber"),
                    AmountPaid = reader.GetDecimal("AmountPaid"),
                    createdDate = reader.GetDateTime("CreatedDate")
                });
            }
            return list;
        }

        public decimal GetTotalPaidByInvoice(string invoiceNumber)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var cmd = new MySqlCommand(
                "SELECT COALESCE(SUM(AmountPaid), 0) FROM MPayment WHERE InvoiceNumber = @InvoiceNumber", conn);
            cmd.Parameters.AddWithValue("@InvoiceNumber", invoiceNumber);
            return Convert.ToDecimal(cmd.ExecuteScalar());
        }
    }
}