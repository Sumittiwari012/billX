using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using MyWPFCRUDApp.Views;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using WPFCRUDApp.Models;

namespace MyWPFCRUDApp.ViewModels
{
    public class PurchaseViewModel : BaseViewModel
    {
        // ── Services ───────────────────────────────────────────────────────────
        private readonly PurchaseService _purchaseService;
        private readonly SupplierService _supplierService;
        private readonly ProductService _productService;
        private readonly BillScanService _billScanService;

        // ── Commands ───────────────────────────────────────────────────────────
        public ICommand AddItemCommand { get; }
        public ICommand PurchaseDeleteCommand { get; }
        public ICommand PurchaseSaveCommand { get; }
        public ICommand PurchaseResetCommand { get; }
        public ICommand BarcodeSearchCommand { get; }
        public ICommand OpenAddSupplierCommand { get; }
        public ICommand ScanBillCommand { get; }

        // ── Collections ────────────────────────────────────────────────────────
        public ObservableCollection<MSupplier> Suppliers { get; set; }
        public ObservableCollection<MProducts> Products { get; set; }
        public ObservableCollection<MPurchaseDetail> PurchaseItems { get; set; }

        // ── Form Models ────────────────────────────────────────────────────────
        private MPurchaseMaster _purchaseMaster;
        public MPurchaseMaster PurchaseMaster
        {
            get => _purchaseMaster;
            set => SetProperty(ref _purchaseMaster, value);
        }

        private MPurchaseDetail _newItem;
        public MPurchaseDetail NewItem
        {
            get => _newItem;
            set => SetProperty(ref _newItem, value);
        }

        // ── Selected ───────────────────────────────────────────────────────────
        private MSupplier _selectedSupplier;
        public MSupplier SelectedSupplier
        {
            get => _selectedSupplier;
            set
            {
                if (SetProperty(ref _selectedSupplier, value) && value != null)
                    PurchaseMaster.SupplierId = value.Id;
            }
        }

        private MProducts _selectedProduct;
        public MProducts SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (SetProperty(ref _selectedProduct, value) && value != null)
                {
                    NewItem.ProductId = value.Id;
                    NewItem.PurchasePrice = value.PurchasePrice;
                    OnPropertyChanged(nameof(NewItem));
                }
            }
        }

        // ── Scanning state ─────────────────────────────────────────────────────
        private bool _isScanning;
        public bool IsScanning
        {
            get => _isScanning;
            set => SetProperty(ref _isScanning, value);
        }

        // ── Constructor ────────────────────────────────────────────────────────
        public PurchaseViewModel()
        {
            _purchaseService = new PurchaseService();
            _supplierService = new SupplierService();
            _productService = new ProductService();
            _billScanService = new BillScanService();

            AddItemCommand = new RelayCommand(_ => AddItemToGrid());
            PurchaseDeleteCommand = new RelayCommand(p => RemoveItemFromGrid(p as MPurchaseDetail));
            PurchaseSaveCommand = new RelayCommand(_ => SavePurchase());
            PurchaseResetCommand = new RelayCommand(_ => ResetForm());
            BarcodeSearchCommand = new RelayCommand(p => HandleBarcodeSearch(p?.ToString()));
            OpenAddSupplierCommand = new RelayCommand(_ => OpenSupplierWindow());
            ScanBillCommand = new RelayCommand(async _ => await ExecuteScanBillAsync());

            InitializeData();
        }

        // ════════════════════════════════════════════════════════════════════════
        // SCAN BILL — main entry point
        // Step 1: Ensure supplier is selected FIRST
        // Step 2: Scan the bill
        // Step 3: Review window (with add-new-product option for unmatched)
        // Step 4: Transfer all fields correctly
        // ════════════════════════════════════════════════════════════════════════
        private async System.Threading.Tasks.Task ExecuteScanBillAsync()
        {
            // ── STEP 1: Supplier must be selected before scanning ──────────────
            if (SelectedSupplier == null)
            {
                var choice = MessageBox.Show(
                    "Please select a supplier before scanning the bill.\n\n" +
                    "Click YES to create a new supplier now, or NO to select an existing one from the list.",
                    "Select Supplier First",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (choice == MessageBoxResult.Yes)
                {
                    OpenSupplierWindow();
                }

                // If still no supplier after dialog, abort
                if (SelectedSupplier == null)
                {
                    MessageBox.Show(
                        "No supplier selected. Please select a supplier and try scanning again.",
                        "Supplier Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // ── STEP 2: Open file picker and send to AI ───────────────────────
            var ofd = new OpenFileDialog
            {
                Title = "Select Purchase Bill (PDF or Image)",
                Filter = "Supported files|*.pdf;*.jpg;*.jpeg;*.png;*.webp" +
                         "|PDF|*.pdf|Images|*.jpg;*.jpeg;*.png;*.webp"
            };
            if (ofd.ShowDialog() != true) return;

            IsScanning = true;
            ScannedBillResult scanned;
            try
            {
                scanned = await _billScanService.ScanBillAsync(ofd.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"AI scan failed:\n\n{ex.Message}\n\n" +
                    "Check your internet connection and Gemini API key (gemini_key.txt).",
                    "Scan Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            finally
            {
                IsScanning = false;
            }

            if (scanned.Items.Count == 0)
            {
                MessageBox.Show(
                    "The AI could not extract any line items from this bill.\n" +
                    "Try a clearer image or enter the items manually.",
                    "No Items Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // ── STEP 3: Open review window (with add-new-product support) ─────
            var reviewWin = new BillScanReviewWindow(scanned, Products.ToList())
            {
                Owner = Application.Current.MainWindow
            };

            if (reviewWin.ShowDialog() != true) return;

            // ── STEP 4: Handle unmatched items — offer to add as new products ─
            var unmatchedItems = reviewWin.ApprovedBill.Items
                .Where(i => i.MatchedProductId <= 0)
                .ToList();

            foreach (var unmatched in unmatchedItems)
            {
                var addChoice = MessageBox.Show(
                    $"Item \"{unmatched.Description}\" is not matched to any product.\n\n" +
                    $"Scanned Rate: ₹{unmatched.Rate:N2}   Qty: {unmatched.Quantity}\n\n" +
                    "Do you want to add this as a new product in your product list?\n" +
                    "(Click YES to add product, NO to skip this item)",
                    "New Product Found",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (addChoice == MessageBoxResult.Yes)
                {
                    var addWin = new AddProductWindow(unmatched.Description)
                    {
                        Owner = Application.Current.MainWindow
                    };
                    // Pre-fill purchase price from scanned rate
                    addWin.PreFillPurchasePrice(unmatched.Rate);

                    if (addWin.ShowDialog() == true)
                    {
                        // Refresh products list
                        Products = new ObservableCollection<MProducts>(_productService.GetProducts());
                        OnPropertyChanged(nameof(Products));

                        // Try to find the newly added product by name or barcode
                        var newProd = Products.OrderByDescending(p => p.Id).FirstOrDefault();
                        if (newProd != null)
                        {
                            unmatched.MatchedProductId = newProd.Id;
                            unmatched.MatchedProductName = newProd.ProductName;
                        }
                    }
                }
            }

            // ── STEP 5: Transfer all items with full data ─────────────────────
            TransferScannedItems(reviewWin.ApprovedBill);
        }

        // ── Transfer approved scanned items into PurchaseItems ─────────────────
        // FIX: correctly maps Qty, Rate and NetAmount for ALL items
        private void TransferScannedItems(ScannedBillResult approved)
        {
            // Pre-fill invoice number and date if still blank
            if (string.IsNullOrWhiteSpace(PurchaseMaster.InvoiceNumber) &&
                !string.IsNullOrWhiteSpace(approved.InvoiceNumber))
                PurchaseMaster.InvoiceNumber = approved.InvoiceNumber;

            if (!string.IsNullOrWhiteSpace(approved.InvoiceDate) &&
                DateTime.TryParseExact(approved.InvoiceDate, "dd-MM-yyyy",
                    null, System.Globalization.DateTimeStyles.None, out DateTime d))
                PurchaseMaster.PurchaseDate = d;

            // Try to auto-select supplier if name matches and none selected
            if (SelectedSupplier == null && !string.IsNullOrWhiteSpace(approved.SupplierName))
            {
                var sup = Suppliers.FirstOrDefault(s =>
                    s.SupplierName.Contains(approved.SupplierName,
                        StringComparison.OrdinalIgnoreCase));
                if (sup != null) SelectedSupplier = sup;
            }

            int added = 0;
            int skipped = 0;

            foreach (var item in approved.Items)
            {
                // Skip unmatched items
                if (item.MatchedProductId <= 0)
                {
                    skipped++;
                    continue;
                }

                var product = Products.FirstOrDefault(p => p.Id == item.MatchedProductId);
                if (product == null)
                {
                    skipped++;
                    continue;
                }

                // ── Use scanned values directly ────────────────────────────────
                // Rate: use scanned rate if > 0, else fall back to product's purchase price
                decimal rate = item.Rate > 0 ? item.Rate : product.PurchasePrice;

                // Quantity: use scanned quantity (must be > 0)
                double qty = item.Quantity > 0 ? item.Quantity : 1;

                // Net Amount: use scanned amount if > 0, else calculate from qty × rate + tax
                decimal tax = (decimal)(product.CGST + product.SGST + product.CESS);
                decimal netAmt;
                if (item.Amount > 0)
                    netAmt = item.Amount;       // use the actual scanned net amount
                else
                    netAmt = (decimal)qty * rate * (1 + tax / 100);

                var existing = PurchaseItems.FirstOrDefault(pi => pi.ProductId == product.Id);
                if (existing != null)
                {
                    // Product already in cart — update all fields
                    int idx = PurchaseItems.IndexOf(existing);
                    existing.Quantity = qty;
                    existing.PurchasePrice = rate;
                    existing.AfterTaxation = netAmt;
                    PurchaseItems.RemoveAt(idx);
                    PurchaseItems.Insert(idx, existing);
                }
                else
                {
                    PurchaseItems.Add(new MPurchaseDetail
                    {
                        ProductId = product.Id,
                        Product = product,
                        Quantity = qty,
                        PurchasePrice = rate,
                        AfterTaxation = netAmt
                    });
                }
                added++;
            }

            CalculateTotal();
            OnPropertyChanged(nameof(PurchaseMaster));

            string msg = $"✔  {added} item(s) transferred from scanned bill.";
            if (skipped > 0) msg += $"\n{skipped} item(s) skipped (unmatched).";
            msg += "\n\nAll values are editable — review and click SAVE INVOICE when ready.";

            MessageBox.Show(msg, "Transfer Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Existing methods (unchanged)
        // ════════════════════════════════════════════════════════════════════════

        private void HandleBarcodeSearch(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return;

            var product = Products.FirstOrDefault(
                p => p.Barcode == barcode || p.ProductCode == barcode);

            if (product != null)
            {
                AddToCart(product);
            }
            else
            {
                var result = MessageBox.Show(
                    $"Barcode '{barcode}' not found. Create new product?",
                    "Product Missing", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                {
                    var addWin = new AddProductWindow(barcode);
                    addWin.Owner = Application.Current.MainWindow;
                    if (addWin.ShowDialog() == true)
                    {
                        Products = new ObservableCollection<MProducts>(
                            _productService.GetProducts());
                        var newProd = Products.FirstOrDefault(p => p.Barcode == barcode);
                        if (newProd != null) AddToCart(newProd);
                    }
                }
            }
        }

        private void AddToCart(MProducts product)
        {
            var existingItem = PurchaseItems.FirstOrDefault(i => i.ProductId == product.Id);

            if (existingItem != null)
            {
                int index = PurchaseItems.IndexOf(existingItem);
                existingItem.Quantity++;
                decimal taxRate = (decimal)(product.CGST + product.SGST + product.CESS);
                decimal subtotal = (decimal)existingItem.Quantity * existingItem.PurchasePrice;
                existingItem.AfterTaxation = subtotal + subtotal * taxRate / 100;
                PurchaseItems.RemoveAt(index);
                PurchaseItems.Insert(index, existingItem);
            }
            else
            {
                decimal taxRate = (decimal)(product.CGST + product.SGST + product.CESS);
                PurchaseItems.Add(new MPurchaseDetail
                {
                    ProductId = product.Id,
                    Product = product,
                    Quantity = 1,
                    PurchasePrice = product.PurchasePrice,
                    AfterTaxation = product.PurchasePrice +
                                     product.PurchasePrice * taxRate / 100
                });
            }
            CalculateTotal();
        }

        private void OpenSupplierWindow()
        {
            var win = new AddSupplierWindow();
            win.Owner = Application.Current.MainWindow;
            if (win.ShowDialog() == true)
            {
                Suppliers = new ObservableCollection<MSupplier>(
                    _supplierService.GetAllSuppliers());
                OnPropertyChanged(nameof(Suppliers));
                SelectedSupplier = Suppliers.FirstOrDefault(
                    s => s.SupplierName == win.NewSupplier.SupplierName);
            }
        }

        private void InitializeData()
        {
            PurchaseMaster = new MPurchaseMaster { PurchaseDate = DateTime.Now };
            NewItem = new MPurchaseDetail();
            PurchaseItems = new ObservableCollection<MPurchaseDetail>();
            Suppliers = new ObservableCollection<MSupplier>(
                                 _supplierService.GetAllSuppliers());
            Products = new ObservableCollection<MProducts>(
                                 _productService.GetProducts());
        }

        private void AddItemToGrid()
        {
            if (SelectedProduct == null || NewItem.Quantity <= 0)
            {
                MessageBox.Show("Please select a product and enter a valid quantity.");
                return;
            }
            AddToCart(SelectedProduct);
            NewItem = new MPurchaseDetail();
            SelectedProduct = null;
        }

        private void RemoveItemFromGrid(MPurchaseDetail item)
        {
            if (item != null && PurchaseItems.Contains(item))
            {
                PurchaseItems.Remove(item);
                CalculateTotal();
            }
        }

        public void RecalculateRow(MPurchaseDetail item)
        {
            if (item == null) return;
            var product = Products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product == null) return;
            decimal tax = (decimal)(product.CGST + product.SGST + product.CESS);
            item.AfterTaxation = (decimal)item.Quantity * item.PurchasePrice * (1 + tax / 100);
            CalculateTotal();

            // Force UI refresh
            int idx = PurchaseItems.IndexOf(item);
            if (idx >= 0)
            {
                PurchaseItems.RemoveAt(idx);
                PurchaseItems.Insert(idx, item);
            }
            OnPropertyChanged(nameof(PurchaseMaster));
        }

        /// <summary>Called from code-behind when user manually edits Net Amount cell.</summary>
        public void RefreshTotal() => CalculateTotal();

        private void CalculateTotal()
        {
            decimal gross = PurchaseItems.Sum(x => x.AfterTaxation);
            PurchaseMaster.TotalAmount = gross - PurchaseMaster.Discount;
            OnPropertyChanged(nameof(PurchaseMaster));
        }

        private void SavePurchase()
        {
            if (SelectedSupplier == null)
            { MessageBox.Show("Please select a supplier."); return; }
            if (!PurchaseItems.Any())
            { MessageBox.Show("Please add at least one item."); return; }

            PurchaseMaster.MPurchaseDetail = PurchaseItems.ToList();

            if (_purchaseService.AddPurchase(PurchaseMaster))
            {
                MessageBox.Show("Purchase recorded and stock updated successfully!");
                ResetForm();
            }
            else
            {
                MessageBox.Show("Error occurred while saving the purchase.");
            }
        }

        private void ResetForm()
        {
            InitializeData();
            SelectedSupplier = null;
        }
    }
}