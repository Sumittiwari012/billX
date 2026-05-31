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
    // ── Value converter: matched product → text colour ────────────────────────
    public class ProductMatchColorConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is long id && id > 0
                ? new SolidColorBrush(Color.FromRgb(25, 113, 194))   // blue = matched
                : new SolidColorBrush(Color.FromRgb(224, 49, 49));   // red  = unmatched

        public object ConvertBack(object v, Type t, object p, CultureInfo c)
            => throw new NotImplementedException();
    }

    // ── Window ────────────────────────────────────────────────────────────────
    public partial class BillScanReviewWindow : Window, INotifyPropertyChanged
    {
        // ── Public result: set when user clicks Approve ───────────────────────
        public ScannedBillResult ApprovedBill { get; private set; }

        // ── Bound properties ──────────────────────────────────────────────────
        private ScannedBillResult _scannedBill;
        public ScannedBillResult ScannedBill
        {
            get => _scannedBill;
            set { _scannedBill = value; OnPropertyChanged(); }
        }

        public ObservableCollection<MProducts> Products { get; }

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
                    ? $"⚠  {n} item(s) not matched. Use the dropdown to link them, or they will be offered as new products after Approve."
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
        }

        // ── Auto-match: try to link scanned descriptions to existing products ─
        private void TryAutoMatch()
        {
            foreach (var item in ScannedBill.Items)
            {
                if (string.IsNullOrWhiteSpace(item.Description)) continue;
                string desc = item.Description.ToLower();

                var match = Products.FirstOrDefault(p =>
                    (!string.IsNullOrEmpty(p.ProductName) &&
                     desc.Contains(p.ProductName.ToLower())) ||
                    (!string.IsNullOrEmpty(p.Barcode) &&
                     desc.Contains(p.Barcode.ToLower())) ||
                    (!string.IsNullOrEmpty(p.ProductCode) &&
                     desc.Contains(p.ProductCode.ToLower())));

                if (match != null)
                {
                    item.MatchedProductId   = match.Id;
                    item.MatchedProductName = match.ProductName;
                    // Auto-fill rate from product's purchase price if rate is 0
                    if (item.Rate == 0 && match.PurchasePrice > 0)
                        item.Rate = match.PurchasePrice;
                }
            }
            RefreshWarnings();
        }

        // ── ComboBox selection in editing template ────────────────────────────
        private void ProductComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.SelectedItem is MProducts p &&
                cb.DataContext is ScannedBillItem item)
            {
                item.MatchedProductId   = p.Id;
                item.MatchedProductName = p.ProductName;
                if (item.Rate == 0 && p.PurchasePrice > 0)
                    item.Rate = p.PurchasePrice;
            }
            RefreshWarnings();
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

        // ── Approve: pass ALL items (including unmatched) back to ViewModel ───
        // The ViewModel will then offer "Add New Product" for unmatched ones.
        private void ApproveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ScannedBill.Items.Any())
            {
                MessageBox.Show(
                    "No items to transfer. Please add items or cancel.",
                    "Nothing to Transfer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int matched = ScannedBill.Items.Count(i => i.MatchedProductId > 0);
            if (matched == 0)
            {
                var cont = MessageBox.Show(
                    "None of the items are matched to products yet.\n\n" +
                    "You can still proceed — you will be offered the option to add " +
                    "unmatched items as new products on the next screen.\n\n" +
                    "Continue?",
                    "No Matches Yet",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
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

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
