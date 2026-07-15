using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WPFCRUDApp.Models;
using System.ComponentModel;
using DocumentFormat.OpenXml.Presentation;

namespace MyWPFCRUDApp.Models
{
    public class MPurchaseMaster:BaseEntity
    {
        [NotMapped]
        public string RemainingColor => RemainingAmount > 0 ? "#E03131" : "#2B8A3E";
        [NotMapped]
        public decimal TotalPaid { get; set; }

        [NotMapped]
        public string SupplierName { get; set; }

        public string VendorInvoiceNumber { get; set; }
        public decimal RemainingAmount { get; set; }
        public string InvoiceNumber { get; set; } // The Bill No. from the supplier
        public long SupplierId { get; set; }
        public DateTime PurchaseDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Discount { get; set; }

        public decimal AmountPaid { get; set; }
        public string PaymentMode { get; set; } // Cash, Credit, Online
        public string Remarks { get; set; }

        // ── Tax amounts (persisted) ────────────────────────────────────────────
        [Column(TypeName = "decimal(18,2)")]
        public decimal CGSTAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SGSTAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal IGSTAmount { get; set; }

        // ── GST applicability flags (set by DetermineGSTType in PurchaseViewModel) ─
        /// <summary>True when supplier and company share the same state code (first 2 GST digits).</summary>
        [NotMapped]
        public bool CGST_Applicable { get; set; }

        /// <summary>True when supplier and company share the same state code (first 2 GST digits).</summary>
        [NotMapped]
        public bool SGST_Applicable { get; set; }

        /// <summary>True when supplier and company are in different states.</summary>
        [NotMapped]
        public bool IGST_Applicable { get; set; }

        public virtual ICollection<MPurchaseDetail> MPurchaseDetail { get; set; }

        [ForeignKey(nameof(SupplierId))]
        [JsonIgnore] // Stops the API from requiring the full Category object
        public virtual MSupplier? MSupplier { get; set; }
        [NotMapped]
        public List<MPurchaseDetail> Details { get; set; } = new();
    }
}
