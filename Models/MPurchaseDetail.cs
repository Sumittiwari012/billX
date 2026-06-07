using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace MyWPFCRUDApp.Models
{
    public class MPurchaseDetail : INotifyPropertyChanged
    {
        public long PurchaseMasterId { get; set; }
        [ForeignKey(nameof(PurchaseMasterId))]
        public virtual MPurchaseMaster? PurchaseMaster { get; set; }

        public long ProductId { get; set; }
        [ForeignKey(nameof(ProductId))]
        public virtual MProducts? Product { get; set; }

        public double Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PurchasePrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal WholesalePrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MRP { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AfterTaxation { get; set; }

        // ── Editable display fields ────────────────────────────────────────────
        [NotMapped]
        private string _barcode = string.Empty;
        [NotMapped]
        public string Barcode
        {
            get => _barcode;
            set { _barcode = value; OnPropertyChanged(); }
        }

        [NotMapped]
        private string _productName = string.Empty;
        [NotMapped]
        public string ProductName
        {
            get => _productName;
            set { _productName = value; OnPropertyChanged(); }
        }

        // ── INotifyPropertyChanged ─────────────────────────────────────────────
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
