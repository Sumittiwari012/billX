
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization; // Required for JsonIgnore

namespace MyWPFCRUDApp.Models
{
    
    public class MProducts : BaseEntity
    {
        public string? ProductCode { get; set; }

        [Required, StringLength(200)]
        public string ProductName { get; set; }

        // --- Category Links ---
        public long CategoryId { get; set; }

        [ForeignKey(nameof(CategoryId))]
        
        public MCategory MCategory { get; set; }

        public long SubCategoryId { get; set; }

        [ForeignKey(nameof(SubCategoryId))]
        
        public MSubCategory MSubCategory { get; set; }

        // --- Item Details (Made Nullable) ---
        public string? HSNCode { get; set; }
        public string? PartGroup { get; set; }
        public string? Description { get; set; }

        // --- Price ---
        [Column(TypeName = "decimal(18,2)")]
        public decimal PurchasePrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RetailSalePrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal WholesalePrice { get; set; }

        public double DiscountPercentage { get; set; }
        public double CGST { get; set; }
        public double SGST { get; set; }
        public double CESS { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MRP { get; set; }

        // --- Inventory (Made Nullable) ---
        public string? Godown { get; set; }
        public string? Rack { get; set; }
        
        public string? Batch { get; set; }
        public DateTime? MfgDate { get; set; }
        public DateTime? ExpDate { get; set; }
        

        // --- Attributes (Made Nullable) ---
        public string? Size { get; set; }
        public string? Colour { get; set; }

        [Required]
        public string Barcode { get; set; }
        public string? IMEI1 { get; set; }
        public string? IMEI2 { get; set; }

        // --- Units ---
        public long UnitId { get; set; }
        [ForeignKey(nameof(UnitId))]
        public MUnit MUnit { get; set; }
    }
}