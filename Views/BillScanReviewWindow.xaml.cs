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

        // Wholesale % and MRP % — loaded from persistent user settings
        private decimal _wholesalePercentage;
        public decimal WholesalePercentage
        {
            get => _wholesalePercentage;
            set
            {
                if (SetField(ref _wholesalePercentage, value))
                {
                    UserSettings.Instance.WholesalePercentage = value;
                    UserSettings.Instance.Save();
                    RecalculatePrices();
                }
            }
        }

        private decimal _mrpPercentage;
        public decimal MRPPercentage
        {
            get => _mrpPercentage;
            set
            {
                if (SetField(ref _mrpPercentage, value))
                {
                    UserSettings.Instance.MRPPercentage = value;
                    UserSettings.Instance.Save();
                    RecalculatePrices();
                }
            }
        }

        // ── Constructor ───────────────────────────────────────────────────────
        public BillScanReviewWindow(ScannedBillResult bill)
        {
            InitializeComponent();
            DataContext = this;

            // Load last-used percentages (defaults: 20% wholesale, 40% MRP on first run)
            _wholesalePercentage = UserSettings.Instance.WholesalePercentage;
            _mrpPercentage       = UserSettings.Instance.MRPPercentage;

            ScannedBill = bill;

            // Calculate Wholesale and MRP once items are loaded
            RecalculatePrices();
        }

        // ── Recalculate Wholesale and MRP for all rows ─────────────────────────
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
            Dispatcher.BeginInvoke(new Action(RecalculatePrices),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        // ── Delete a row ──────────────────────────────────────────────────────
        private void DeleteRowButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ScannedBillItem item)
                ScannedBill.Items.Remove(item);
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
