using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWPFCRUDApp.Models
{
    public class MCompanyInfo:BaseEntity
    {

        // Basic Company Details
        [Required, StringLength(200)]
        public string CompanyName { get; set; }

        [StringLength(200)]
        public string OwnerName { get; set; }

        // Contact Details
        [StringLength(20)]
        public string Phone { get; set; }

        [StringLength(20)]
        public string Mobile { get; set; }

        [StringLength(200)]
        public string Email { get; set; }

        [StringLength(200)]
        public string Website { get; set; }

        // Address
        [StringLength(300)]
        public string AddressLine1 { get; set; }

        [StringLength(300)]
        public string AddressLine2 { get; set; }

        [StringLength(100)]
        public string City { get; set; }

        [StringLength(100)]
        public string State { get; set; }

        [StringLength(20)]
        public string Pincode { get; set; }

        // Registration Numbers
        [StringLength(50)]
        public string GSTNumber { get; set; }

        [StringLength(20)]
        public string PANNumber { get; set; }

        [StringLength(50)]
        public string CINNumber { get; set; }

        [StringLength(50)]
        public string IECCode { get; set; }

        // Branding
        public string LogoPath { get; set; }

        // Invoice Settings
        public int InvoiceStartNumber { get; set; }
        public bool ShowLogoOnInvoice { get; set; }

        [StringLength(300)]
        public string InvoiceFooterNote { get; set; }

        // Bank Details
        [StringLength(200)]
        public string BankName { get; set; }

        [StringLength(200)]
        public string Branch { get; set; }

        [StringLength(50)]
        public string AccountNumber { get; set; }

        [StringLength(20)]
        public string IFSCCode { get; set; }
    }
}
