using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using MyWPFCRUDApp.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using WPFCRUDApp.Models;

namespace MyWPFCRUDApp.Views
{
    public partial class SupplierHistoryWindow : Window
    {
        private readonly PurchaseViewModel _vm;
        private readonly PurchaseService _purchaseService = new();
        private readonly SupplierService _supplierService = new();
        private readonly Dictionary<long, string> _supplierNameLookup;

        public SupplierHistoryWindow(PurchaseViewModel vm, MSupplier supplier = null)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;

            // Built once — used to resolve SupplierName for every row,
            // since MPurchaseMaster only stores the SupplierId.
            _supplierNameLookup = _supplierService.GetAllSuppliers()
                .ToDictionary(s => s.Id, s => s.SupplierName);

            // Optional convenience pre-fill if opened from a specific supplier;
            // boxes stay editable so the user can change/clear and search by anything.
            if (supplier != null)
            {
                SupplierIdBox.Text = supplier.Id.ToString();
                SupplierNameBox.Text = supplier.SupplierName;
            }

            LoadHistory();
        }

        private void LoadHistory()
        {
            string supplierIdText = SupplierIdBox.Text.Trim();
            string supplierNameText = SupplierNameBox.Text.Trim();
            string invoiceNumber = InvoiceNumberBox.Text.Trim();
            DateTime? fromDate = FromDatePicker.SelectedDate;
            DateTime? toDate = ToDatePicker.SelectedDate;

            List<MPurchaseMaster> records = _purchaseService.GetFilteredPurchases().ToList();

            // ── Step 1: Invoice Number ───────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(invoiceNumber))
            {
                records = records
                    .Where(r => !string.IsNullOrEmpty(r.InvoiceNumber) &&
                                r.InvoiceNumber.IndexOf(invoiceNumber, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }

            // ── Step 2: Supplier ID ───────────────────────────────────────────
            if (long.TryParse(supplierIdText, out long sid) && sid > 0)
            {
                records = records
                    .Where(r => r.SupplierId == sid)
                    .ToList();
            }

            // ── Step 3: Supplier Name (resolved via lookup, since it's not
            //            stored directly on MPurchaseMaster) ──────────────────
            if (!string.IsNullOrWhiteSpace(supplierNameText))
            {
                records = records
                    .Where(r =>
                        _supplierNameLookup.TryGetValue(r.SupplierId, out var name) &&
                        name.IndexOf(supplierNameText, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }

            // ── Step 4: From Date ─────────────────────────────────────────────
            if (fromDate.HasValue)
            {
                records = records
                    .Where(r => r.PurchaseDate.Date >= fromDate.Value.Date)
                    .ToList();
            }

            // ── Step 5: To Date ───────────────────────────────────────────────
            if (toDate.HasValue)
            {
                records = records
                    .Where(r => r.PurchaseDate.Date <= toDate.Value.Date)
                    .ToList();
            }
            records = records
    .OrderByDescending(r => r.PurchaseDate)
    .ToList();

            // Stamp SupplierName onto each record so the header can bind to it
            foreach (var r in records)
            {
                r.SupplierName = _supplierNameLookup.TryGetValue(r.SupplierId, out var n)
                    ? n
                    : "Unknown";
            }

            HistoryList.ItemsSource = new ObservableCollection<MPurchaseMaster>(records);
            EmptyText.Visibility = records.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CheckButton_Click(object sender, RoutedEventArgs e)
        {
            LoadHistory();
        }

        private void OpenInvoice_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            SupplierNameBox.Clear();
            SupplierIdBox.Clear();
            InvoiceNumberBox.Clear();
            FromDatePicker.SelectedDate = null;
            ToDatePicker.SelectedDate = null;

            LoadHistory();
        }
    }
}