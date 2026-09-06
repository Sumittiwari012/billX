using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace MyWPFCRUDApp.Views
{
    // ════════════════════════════════════════════════════════════════════════
    // BarcodeLabelRow — one row per invoice item in the Barcode Labels window.
    // Extended (was: Barcode, Quantity, ProductName, MRP, Retail, BarcodeImage,
    // IsLabelVisible) to carry every product-table field the column picker can
    // show, so checking a column never needs a fresh DB round-trip.
    // ════════════════════════════════════════════════════════════════════════
    public class BarcodeLabelRow : INotifyPropertyChanged
    {
        public string Barcode { get; set; } = string.Empty;
        public double Quantity { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public BitmapSource? BarcodeImage { get; set; }

        // ── Pricing ──────────────────────────────────────────────────────────
        public decimal MRP { get; set; }
        public decimal Retail { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal WholesalePrice { get; set; }
        public double DiscountPercentage { get; set; }

        // ── Tax ──────────────────────────────────────────────────────────────
        public double CGST { get; set; }
        public double SGST { get; set; }
        public double IGST { get; set; }
        public double CESS { get; set; }

        // ── Item details (from MProducts / MPurchaseDetail) ─────────────────
        public string? ProductCode { get; set; }
        public string? HSNCode { get; set; }
        public string? Size { get; set; }
        public string? Colour { get; set; }
        public string? Batch { get; set; }
        public DateTime? MfgDate { get; set; }
        public DateTime? ExpDate { get; set; }
        public string? Godown { get; set; }
        public string? Rack { get; set; }
        public string? PartGroup { get; set; }
        public string? Description { get; set; }

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ════════════════════════════════════════════════════════════════════════
    // BarcodeColumnOption — one checkbox in the "Columns" picker on the left
    // panel. IsMandatory columns (Barcode/Product Name/Quantity) are always on
    // and rendered directly in XAML — this list is only the OPTIONAL columns
    // that get added to / removed from LabelsGrid.Columns as they're toggled.
    // ════════════════════════════════════════════════════════════════════════
    public class BarcodeColumnOption : INotifyPropertyChanged
    {
        public string Header { get; set; } = string.Empty;
        public string BindingPath { get; set; } = string.Empty;
        public string? StringFormat { get; set; }
        public double Width { get; set; } = 90;

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged();
                    CheckedChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler? CheckedChanged;
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}