using MyWPFCRUDApp.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WPFCRUDApp.Models
{
    public class MSupplier : BaseEntity
    {
        [Required, StringLength(200)]
        public string SupplierName { get; set; }

        public string? ContactPerson { get; set; }

        [Required, StringLength(15)]
        public string MobileNumber { get; set; }

        public string? Email { get; set; }

        public string? GSTIN { get; set; }

        public string? Address { get; set; }

        public string? City { get; set; }

        public string? State { get; set; }

        // --- Financials ---
        [Column(TypeName = "decimal(18,2)")]
        public decimal OpeningBalance { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentBalance { get; set; } // Tracks credit/debit

        public string? AccountNumber { get; set; }
        public string? BankName { get; set; }
        public string? IFSCCode { get; set; }

        // --- Metadata ---
        public bool IsActive { get; set; } = true;
    }
}