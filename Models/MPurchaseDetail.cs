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

        private double _quantity;
        public double Quantity
        {
            get => _quantity;
            set { if (_quantity != value) { _quantity = value; OnPropertyChanged(); RecalcAmount(); } }
        }

        private decimal _purchasePrice;
        [Column(TypeName = "decimal(18,2)")]
        public decimal PurchasePrice
        {
            get => _purchasePrice;
            set { if (_purchasePrice != value) { _purchasePrice = value; OnPropertyChanged(); RecalcAmount(); } }
        }

        private decimal _wholesalePrice;
        [Column(TypeName = "decimal(18,2)")]
        public decimal WholesalePrice
        {
            get => _wholesalePrice;
            set { if (_wholesalePrice != value) { _wholesalePrice = value; OnPropertyChanged(); } }
        }

        private decimal _mrp;
        [Column(TypeName = "decimal(18,2)")]
        public decimal MRP
        {
            get => _mrp;
            set { if (_mrp != value) { _mrp = value; OnPropertyChanged(); } }
        }

        private decimal _retail;
        [Column(TypeName = "decimal(18,2)")]
        public decimal Retail
        {
            get => _retail;
            set { if (_retail != value) { _retail = value; OnPropertyChanged(); } }
        }

        private decimal _afterTaxation;
        [Column(TypeName = "decimal(18,2)")]
        public decimal AfterTaxation
        {
            get => _afterTaxation;
            set { if (_afterTaxation != value) { _afterTaxation = value; OnPropertyChanged(); } }
        }

        private void RecalcAmount()
        {
            if (_quantity > 0 && _purchasePrice > 0)
                AfterTaxation = (decimal)_quantity * _purchasePrice;
        }

        // ── Editable display fields ────────────────────────────────────────────
        private string _barcode = string.Empty;
        [NotMapped]
        public string Barcode
        {
            get => _barcode;
            set { _barcode = value; OnPropertyChanged(); }
        }

        private string _productName = string.Empty;
        [NotMapped]
        public string ProductName
        {
            get => _productName;
            set { _productName = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        private string _hsnCode = string.Empty;
        [NotMapped]
        public string HSNCode
        {
            get => _hsnCode;
            set { _hsnCode = value; OnPropertyChanged(); }
        }
        private string _size = string.Empty;
        [NotMapped]
        public string Size
        {
            get => _size;
            set { _size = value; OnPropertyChanged(); }
        }
        private string _colour = string.Empty;
        [NotMapped]
        public string Colour
        {
            get => _colour;
            set { _colour = value; OnPropertyChanged(); }
        }
        private decimal _cgst;
        [NotMapped]
        public decimal CGST
        {
            get => _cgst;
            set { if (_cgst != value) { _cgst = value; OnPropertyChanged(); } }
        }
        private decimal _sgst;
        [NotMapped]
        public decimal SGST
        {
            get => _sgst;
            set { if (_sgst != value) { _sgst = value; OnPropertyChanged(); } }
        }
        private decimal _igst;
        [NotMapped]
        public decimal IGST
        {
            get => _igst;
            set { if (_igst != value) { _igst = value; OnPropertyChanged(); } }
        }
    }
}