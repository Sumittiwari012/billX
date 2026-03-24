using MySql.Data.MySqlClient;
using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWPFCRUDApp.Services
{
    public class CompanyService
    {
        private string Con => DatabaseHelper.ConnectionString;
        // ─── CREATE ───────────────────────────────────────────────

        public bool InsertCompany(MCompanyInfo s)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var sql = @"INSERT INTO MCompanyInfo (
                CompanyName, OwnerName, Phone, Mobile, Email, Website, 
                AddressLine1, AddressLine2, City, State, Pincode, 
                GSTNumber, PANNumber, CINNumber, IECCode, 
                LogoPath, InvoiceStartNumber, ShowLogoOnInvoice, InvoiceFooterNote, 
                BankName, Branch, AccountNumber, IFSCCode,
                CreatedBy, ModifiedBy
            ) 
            VALUES (
                @CompanyName, @OwnerName, @Phone, @Mobile, @Email, @Website, 
                @AddressLine1, @AddressLine2, @City, @State, @Pincode, 
                @GSTNumber, @PANNumber, @CINNumber, @IECCode, 
                @LogoPath, @InvoiceStartNumber, @ShowLogoOnInvoice, @InvoiceFooterNote, 
                @BankName, @Branch, @AccountNumber, @IFSCCode,
                @CreatedBy, @ModifiedBy
            )";

            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@CompanyName", s.CompanyName);
            cmd.Parameters.AddWithValue("@OwnerName", s.OwnerName);
            cmd.Parameters.AddWithValue("@Phone", s.Phone);
            cmd.Parameters.AddWithValue("@Mobile", s.Mobile);
            cmd.Parameters.AddWithValue("@Email", s.Email);
            cmd.Parameters.AddWithValue("@Website", s.Website);
            cmd.Parameters.AddWithValue("@AddressLine1", s.AddressLine1);
            cmd.Parameters.AddWithValue("@AddressLine2", s.AddressLine2);
            cmd.Parameters.AddWithValue("@City", s.City);
            cmd.Parameters.AddWithValue("@State", s.State);
            cmd.Parameters.AddWithValue("@Pincode", s.Pincode);
            cmd.Parameters.AddWithValue("@GSTNumber", s.GSTNumber);
            cmd.Parameters.AddWithValue("@PANNumber", s.PANNumber);
            cmd.Parameters.AddWithValue("@CINNumber", s.CINNumber);
            cmd.Parameters.AddWithValue("@IECCode", s.IECCode);
            cmd.Parameters.AddWithValue("@LogoPath", s.LogoPath);
            cmd.Parameters.AddWithValue("@InvoiceStartNumber", s.InvoiceStartNumber);
            cmd.Parameters.AddWithValue("@ShowLogoOnInvoice", s.ShowLogoOnInvoice);
            cmd.Parameters.AddWithValue("@InvoiceFooterNote", s.InvoiceFooterNote);
            cmd.Parameters.AddWithValue("@BankName", s.BankName);
            cmd.Parameters.AddWithValue("@Branch", s.Branch);
            cmd.Parameters.AddWithValue("@AccountNumber", s.AccountNumber);
            cmd.Parameters.AddWithValue("@IFSCCode", s.IFSCCode);
            cmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
            cmd.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);
            cmd.Parameters.AddWithValue("@CreatedBy", "ADMIN");
            cmd.Parameters.AddWithValue("@ModifiedBy", "");
           
            return cmd.ExecuteNonQuery() > 0;
        }

        // ─── READ ─────────────────────────────────────────────────
        public List<MCompanyInfo> GetCompanyInfo()
        {
            var list = new List<MCompanyInfo>();
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var cmd = new MySqlCommand("SELECT * FROM MCompanyInfo", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new MCompanyInfo
                {
                    Id = reader.GetInt32("Id"),
                    CompanyName = reader.GetString("CompanyName"),
                    OwnerName = reader.GetString("OwnerName"),
                    Phone = reader.GetString("Phone"),
                    Mobile = reader.GetString("Mobile"),
                    Email = reader.GetString("Email"),
                    Website = reader.GetString("Website"),
                    AddressLine1 = reader.GetString("AddressLine1"),
                    AddressLine2 = reader.GetString("AddressLine2"),
                    City = reader.GetString("City"),
                    State = reader.GetString("State"),
                    Pincode = reader.GetString("Pincode"),
                    GSTNumber = reader.GetString("GSTNumber"),
                    PANNumber = reader.GetString("PANNumber"),
                    CINNumber = reader.GetString("CINNumber"),
                    IECCode = reader.GetString("IECCode"),
                    LogoPath = reader.GetString("LogoPath"),
                    InvoiceStartNumber = reader.GetInt32("InvoiceStartNumber"),
                    ShowLogoOnInvoice = reader.GetBoolean("ShowLogoOnInvoice"),
                    InvoiceFooterNote = reader.GetString("InvoiceFooterNote"),
                    BankName = reader.GetString("BankName"),
                    Branch = reader.GetString("Branch"),
                    AccountNumber = reader.GetString("AccountNumber"),
                    IFSCCode = reader.GetString("IFSCCode"),
                    
                });
            }
            return list;
        }

        // ─── UPDATE ───────────────────────────────────────────────
        public bool UpdateCompanyInfo(MCompanyInfo company)
        {
            using var conn = new MySqlConnection(Con);
            var sql = @"UPDATE MCompanyInfo SET 
                CompanyName = @CompanyName, 
                OwnerName = @OwnerName, 
                Phone = @Phone, 
                Mobile = @Mobile, 
                Email = @Email, 
                Website = @Website, 
                AddressLine1 = @AddressLine1, 
                AddressLine2 = @AddressLine2, 
                City = @City, 
                State = @State, 
                Pincode = @Pincode, 
                GSTNumber = @GSTNumber, 
                PANNumber = @PANNumber, 
                CINNumber = @CINNumber, 
                IECCode = @IECCode, 
                LogoPath = @LogoPath, 
                InvoiceStartNumber = @InvoiceStartNumber, 
                ShowLogoOnInvoice = @ShowLogoOnInvoice, 
                InvoiceFooterNote = @InvoiceFooterNote, 
                BankName = @BankName, 
                Branch = @Branch, 
                AccountNumber = @AccountNumber, 
                IFSCCode = @IFSCCode,
                ModifiedBy = @ModifiedBy,
                ModifiedDate = CURRENT_TIMESTAMP
            WHERE Id = @Id";

            var cmd = new MySqlCommand(sql, conn);

            // Primary Key for WHERE clause
            cmd.Parameters.AddWithValue("@Id", company.Id);

            // Basic Company Details
            cmd.Parameters.AddWithValue("@CompanyName", company.CompanyName);
            cmd.Parameters.AddWithValue("@OwnerName", (object)company.OwnerName ?? DBNull.Value);

            // Contact & Address
            cmd.Parameters.AddWithValue("@Phone", (object)company.Phone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Mobile", (object)company.Mobile ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Email", (object)company.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Website", (object)company.Website ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AddressLine1", (object)company.AddressLine1 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AddressLine2", (object)company.AddressLine2 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@City", (object)company.City ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@State", (object)company.State ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Pincode", (object)company.Pincode ?? DBNull.Value);

            // Registration & Branding
            cmd.Parameters.AddWithValue("@GSTNumber", (object)company.GSTNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PANNumber", (object)company.PANNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CINNumber", (object)company.CINNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IECCode", (object)company.IECCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LogoPath", (object)company.LogoPath ?? DBNull.Value);

            // Invoice & Bank
            cmd.Parameters.AddWithValue("@InvoiceStartNumber", company.InvoiceStartNumber);
            cmd.Parameters.AddWithValue("@ShowLogoOnInvoice", company.ShowLogoOnInvoice);
            cmd.Parameters.AddWithValue("@InvoiceFooterNote", (object)company.InvoiceFooterNote ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@BankName", (object)company.BankName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Branch", (object)company.Branch ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AccountNumber", (object)company.AccountNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IFSCCode", (object)company.IFSCCode ?? DBNull.Value);

            // Audit Field
            cmd.Parameters.AddWithValue("@ModifiedBy", company.modifiedBy);

            conn.Open();
            return cmd.ExecuteNonQuery() > 0;
        }

        // ─── DELETE ───────────────────────────────────────────────
        public bool DeleteCompany(long id)
        {
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var cmd = new MySqlCommand("DELETE FROM MCompanyInfo WHERE Id=@id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }
}
