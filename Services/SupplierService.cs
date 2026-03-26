using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using WPFCRUDApp.Models;

namespace MyWPFCRUDApp.Services
{
    public class SupplierService
    {
        private string Con => DatabaseHelper.ConnectionString;

        // ─── INSERT ────────────────────────────────────────────────────────────
        public bool InsertSupplier(MSupplier s)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"INSERT INTO MSupplier (
                SupplierName, ContactPerson, MobileNumber, Email, GSTIN, 
                Address, City, State, OpeningBalance, CurrentBalance, 
                AccountNumber, BankName, IFSCCode, IsActive,
                CreatedBy, CreatedDate, ModifiedBy, ModifiedDate
            ) VALUES (
                @SupplierName, @ContactPerson, @MobileNumber, @Email, @GSTIN, 
                @Address, @City, @State, @OpeningBalance, @CurrentBalance, 
                @AccountNumber, @BankName, @IFSCCode, @IsActive,
                @CreatedBy, @CreatedDate, @ModifiedBy, @ModifiedDate
            )";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@SupplierName", s.SupplierName);
            cmd.Parameters.AddWithValue("@ContactPerson", s.ContactPerson ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@MobileNumber", s.MobileNumber);
            cmd.Parameters.AddWithValue("@Email", s.Email ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@GSTIN", s.GSTIN ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Address", s.Address ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@City", s.City ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@State", s.State ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@OpeningBalance", s.OpeningBalance);
            cmd.Parameters.AddWithValue("@CurrentBalance", s.CurrentBalance);
            cmd.Parameters.AddWithValue("@AccountNumber", s.AccountNumber ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@BankName", s.BankName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@IFSCCode", s.IFSCCode ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@IsActive", s.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@CreatedBy", "ADMIN");
            cmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
            cmd.Parameters.AddWithValue("@ModifiedBy", "ADMIN");
            cmd.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);

            return cmd.ExecuteNonQuery() > 0;
        }

        // ─── GET ALL ───────────────────────────────────────────────────────────
        public List<MSupplier> GetAllSuppliers()
        {
            var list = new List<MSupplier>();
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var cmd = new MySqlCommand("SELECT * FROM MSupplier ORDER BY SupplierName", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new MSupplier
                {
                    Id = Convert.ToInt64(reader["Id"]),
                    SupplierName = reader.GetString("SupplierName"),
                    ContactPerson = reader["ContactPerson"]?.ToString(),
                    MobileNumber = reader.GetString("MobileNumber"),
                    Email = reader["Email"]?.ToString(),
                    GSTIN = reader["GSTIN"]?.ToString(),
                    Address = reader["Address"]?.ToString(),
                    City = reader["City"]?.ToString(),
                    State = reader["State"]?.ToString(),
                    OpeningBalance = reader.GetDecimal("OpeningBalance"),
                    CurrentBalance = reader.GetDecimal("CurrentBalance"),
                    AccountNumber = reader["AccountNumber"]?.ToString(),
                    BankName = reader["BankName"]?.ToString(),
                    IFSCCode = reader["IFSCCode"]?.ToString(),
                    IsActive = Convert.ToBoolean(reader["IsActive"])
                });
            }
            return list;
        }

        // ─── UPDATE ────────────────────────────────────────────────────────────
        public bool UpdateSupplier(MSupplier s)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"UPDATE MSupplier SET 
                SupplierName = @SupplierName, 
                ContactPerson = @ContactPerson, 
                MobileNumber = @MobileNumber, 
                Email = @Email, 
                GSTIN = @GSTIN, 
                Address = @Address, 
                City = @City, 
                State = @State, 
                OpeningBalance = @OpeningBalance, 
                CurrentBalance = @CurrentBalance, 
                AccountNumber = @AccountNumber, 
                BankName = @BankName, 
                IFSCCode = @IFSCCode, 
                IsActive = @IsActive,
                ModifiedBy = @ModifiedBy,
                ModifiedDate = @ModifiedDate
                WHERE Id = @Id";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", s.Id);
            cmd.Parameters.AddWithValue("@SupplierName", s.SupplierName);
            cmd.Parameters.AddWithValue("@ContactPerson", s.ContactPerson ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@MobileNumber", s.MobileNumber);
            cmd.Parameters.AddWithValue("@Email", s.Email ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@GSTIN", s.GSTIN ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Address", s.Address ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@City", s.City ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@State", s.State ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@OpeningBalance", s.OpeningBalance);
            cmd.Parameters.AddWithValue("@CurrentBalance", s.CurrentBalance);
            cmd.Parameters.AddWithValue("@AccountNumber", s.AccountNumber ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@BankName", s.BankName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@IFSCCode", s.IFSCCode ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@IsActive", s.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@ModifiedBy", "ADMIN");
            cmd.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);

            return cmd.ExecuteNonQuery() > 0;
        }

        // ─── DELETE ────────────────────────────────────────────────────────────
        public bool DeleteSupplier(long id)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = "DELETE FROM MSupplier WHERE Id = @Id";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }
}