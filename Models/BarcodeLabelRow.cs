using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace MyWPFCRUDApp.Models
{
    public class BarcodeLabelRow : INotifyPropertyChanged
    {
        public bool IsSelected { get; set; }
        public string Barcode { get; set; }
        public double Quantity { get; set; }
        public string ProductName { get; set; }
        public decimal MRP { get; set; }
        public decimal Retail { get; set; }
        public BitmapSource BarcodeImage { get; set; }

        // Controls whether the Label column shows its content for this row.
        // Starts hidden; toggled on when the user clicks the Barcode cell.
        private bool _isLabelVisible;
        public bool IsLabelVisible
        {
            get => _isLabelVisible;
            set
            {
                if (_isLabelVisible != value)
                {
                    _isLabelVisible = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLabelVisible)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}