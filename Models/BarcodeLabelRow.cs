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
    //
    // NOTE: this is the single canonical BarcodeLabelRow for the project. A
    // second, incompatible class with the same name used to also exist in
    // MyWPFCRUDApp.Models — that one has been deleted (see the comment left in
    // its place) since a duplicate type name across namespaces is a silent-bug
    // trap: whichever namespace a file already belongs to wins name resolution
    // over a `using` import, with no compiler warning.
    // ════════════════════════════════════════════════════════════════════════
    public class BarcodeLabelRow : INotifyPropertyChanged
    {
        public string Barcode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public BitmapSource? BarcodeImage { get; set; }

        // ── Quantity ─────────────────────────────────────────────────────────
        // Quantity: the original invoice quantity for this line. Read-only in
        // the grid — kept purely as reference so the user can see what was
        // actually purchased.
        public double Quantity { get; set; }

        // PrintQuantity: how many labels to actually print for this row.
        // Seeded from Quantity in BuildRow(), but independently editable in
        // the "Print Qty" grid column, so printing fewer labels than were
        // purchased (e.g. only 5 of 20 units still need a fresh barcode)
        // no longer wastes label stock. Clamped to >= 0 — negative counts
        // don't mean anything for a print run.
        private double _printQuantity;
        public double PrintQuantity
        {
            get => _printQuantity;
            set
            {
                double clamped = value < 0 ? 0 : value;
                if (_printQuantity != clamped)
                {
                    _printQuantity = clamped;
                    OnPropertyChanged();
                }
            }
        }

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

        // Defaults to false: rows start unchecked in the "Print?" column, and
        // the user opts in per row or via the Select All / Unselect All
        // buttons in BarcodeLabelsWindow. (Previously defaulted to true —
        // BuildRow() worked around that by overriding it in the object
        // initializer, but fixing the default here means every future call
        // site gets the right behavior automatically instead of having to
        // remember the override.)
        private bool _isSelected;
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
    // panel. IsMandatory columns (Barcode/Product Name/Inv. Qty/Print Qty) are
    // always on and rendered directly in XAML — this list is only the OPTIONAL
    // columns that get added to / removed from LabelsGrid.Columns as they're
    // toggled.
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

// ════════════════════════════════════════════════════════════════════════════
// DELETE MyWPFCRUDApp/Models/BarcodeLabelRow.cs
// ════════════════════════════════════════════════════════════════════════════
// That file previously contained a second, incompatible `BarcodeLabelRow`
// class in the `MyWPFCRUDApp.Models` namespace (Barcode/Quantity/ProductName/
// MRP/Retail/BarcodeImage/IsLabelVisible, no PrintQuantity). It was never
// constructed anywhere — BuildRow() in BarcodeLabelsWindow.xaml.cs always
// builds the Views.BarcodeLabelRow above — so it was dead code sitting in the
// project as a silent-bug trap (a duplicate type name across namespaces wins
// or loses `using` resolution unpredictably, with zero compiler warning).
// Simply delete that file from the project; nothing references it.