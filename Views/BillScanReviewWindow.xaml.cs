using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace MyWPFCRUDApp.Views
{
    public class ProductMatchColorConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is long id && id > 0
                ? new SolidColorBrush(Color.FromRgb(25, 113, 194))
                : new SolidColorBrush(Color.FromRgb(224, 49, 49));

        public object ConvertBack(object v, Type t, object p, CultureInfo c)
            => throw new NotImplementedException();
    }

    public partial class BillScanReviewWindow : Window, INotifyPropertyChanged
    {
        public ScannedBillResult ApprovedBill { get; private set; }

        // ── Bound properties ──────────────────────────────────────────────────
        private ScannedBillResult _scannedBill;
        public ScannedBillResult ScannedBill
        {
            get => _scannedBill;
            set { _scannedBill = value; OnPropertyChanged(); }
        }

        public ObservableCollection<MProducts> Products { get; }

        // Default 20% wholesale, 40% MRP — user can change live
        private decimal _wholesalePercentage = 20;
        public decimal WholesalePercentage
        {
            get => _wholesalePercentage;
            set
            {
                if (SetField(ref _wholesalePercentage, value))
                    RecalculatePrices();
            }
        }

        private decimal _mrpPercentage = 40;
        public decimal MRPPercentage
        {
            get => _mrpPercentage;
            set
            {
                if (SetField(ref _mrpPercentage, value))
                    RecalculatePrices();
            }
        }

        public int MatchedCount =>
            ScannedBill?.Items.Count(i => i.MatchedProductId > 0) ?? 0;

        public int UnmatchedCount =>
            ScannedBill?.Items.Count(i => i.MatchedProductId <= 0) ?? 0;

        public Visibility UnmatchedWarningVisibility =>
            ScannedBill?.Items.Any(i => i.MatchedProductId <= 0) == true
                ? Visibility.Visible : Visibility.Collapsed;

        public string UnmatchedWarningText
        {
            get
            {
                int n = ScannedBill?.Items.Count(i => i.MatchedProductId <= 0) ?? 0;
                return n > 0
                    ? $"⚠  {n} item(s) not matched — they will be offered as new products after Approve."
                    : string.Empty;
            }
        }

        // ── Constructor ───────────────────────────────────────────────────────
        public BillScanReviewWindow(ScannedBillResult bill, List<MProducts> products)
        {
            Resources.Add("ProductMatchColorConverter", new ProductMatchColorConverter());
            InitializeComponent();
            DataContext = this;

            ScannedBill = bill;
            Products    = new ObservableCollection<MProducts>(products);

            TryAutoMatch();

            // Calculate Wholesale and MRP once items are loaded
            RecalculatePrices();
        }

        // ── Auto-match descriptions to existing products ───────────────────────
        private void TryAutoMatch()
        {
            foreach (var item in ScannedBill.Items)
            {
                if (string.IsNullOrWhiteSpace(item.Description)) continue;
                string desc = item.Description.ToLower();

                var match = Products.FirstOrDefault(p =>
                    (!string.IsNullOrEmpty(p.ProductName) && desc.Contains(p.ProductName.ToLower())) ||
                    (!string.IsNullOrEmpty(p.Barcode)     && desc.Contains(p.Barcode.ToLower()))     ||
                    (!string.IsNullOrEmpty(p.ProductCode) && desc.Contains(p.ProductCode.ToLower())));

                if (match != null)
                {
                    item.MatchedProductId   = match.Id;
                    item.MatchedProductName = match.ProductName;
                    if (item.PurchasePrice == 0 && match.PurchasePrice > 0)
                        item.PurchasePrice = match.PurchasePrice;
                }
            }
            RefreshWarnings();
        }

        // ── Recalculate Wholesale and MRP for all rows ─────────────────────────
        // Called: on load, when % inputs change, after grid cell edit
        private void RecalculatePrices()
        {
            if (ScannedBill?.Items == null) return;

            foreach (var item in ScannedBill.Items)
            {
                if (item.PurchasePrice <= 0) continue;

                item.WholesalePrice = item.PurchasePrice
                    + (item.PurchasePrice * _wholesalePercentage / 100m);

                item.MRP = item.PurchasePrice
                    + (item.PurchasePrice * _mrpPercentage / 100m);
            }
        }

        // ── Grid cell edit ended — recalc if Purchase Price was edited ─────────
        private void ItemsGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Delay slightly so the binding has time to push the value to the model
            Dispatcher.BeginInvoke(new Action(RecalculatePrices),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        // ── Delete a row ──────────────────────────────────────────────────────
        private void DeleteRowButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ScannedBillItem item)
            {
                ScannedBill.Items.Remove(item);
                RefreshWarnings();
            }
        }

        // ── Approve ───────────────────────────────────────────────────────────
        private void ApproveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ScannedBill.Items.Any())
            {
                MessageBox.Show("No items to transfer.", "Nothing to Transfer",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int matched = ScannedBill.Items.Count(i => i.MatchedProductId > 0);
            if (matched == 0)
            {
                var cont = MessageBox.Show(
                    "None of the items are matched to existing products yet.\n\n" +
                    "You will be offered the option to add them as new products.\n\nContinue?",
                    "No Matches Yet", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (cont != MessageBoxResult.Yes) return;
            }

            ApprovedBill = ScannedBill;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void RefreshWarnings()
        {
            OnPropertyChanged(nameof(MatchedCount));
            OnPropertyChanged(nameof(UnmatchedCount));
            OnPropertyChanged(nameof(UnmatchedWarningVisibility));
            OnPropertyChanged(nameof(UnmatchedWarningText));
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
