using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Views;
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
        private readonly ProductService  _productService;
        private readonly BillScanService _billScanService;

        // ── Commands ───────────────────────────────────────────────────────────
        public ICommand AddItemCommand         { get; }
        public ICommand PurchaseDeleteCommand  { get; }
        public ICommand PurchaseSaveCommand    { get; }
        public ICommand PurchaseResetCommand   { get; }
        public ICommand BarcodeSearchCommand   { get; }
        public ICommand OpenAddSupplierCommand { get; }
        public ICommand ScanBillCommand        { get; }
        public ICommand OpenApiKeySetupCommand { get; }

        // ── Collections ────────────────────────────────────────────────────────
        public ObservableCollection<MSupplier>       Suppliers     { get; set; }
        public ObservableCollection<MProducts>       Products      { get; set; }
        public ObservableCollection<MPurchaseDetail> PurchaseItems { get; set; }

        // ── Form models ────────────────────────────────────────────────────────
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

                // Update scan button hint
                OnPropertyChanged(nameof(ScanHintText));
                OnPropertyChanged(nameof(ScanHintVisibility));
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
                    NewItem.ProductId     = value.Id;
                    NewItem.PurchasePrice = value.PurchasePrice;
                    OnPropertyChanged(nameof(NewItem));
                }
            }
        }

        // ── Scan hint (shown when no supplier selected) ────────────────────────
        public string ScanHintText => SelectedSupplier == null
            ? "Select supplier first" : string.Empty;

        public Visibility ScanHintVisibility => SelectedSupplier == null
            ? Visibility.Visible : Visibility.Collapsed;

        // ── Constructor ────────────────────────────────────────────────────────
        public PurchaseViewModel()
        {
            _purchaseService = new PurchaseService();
            _supplierService = new SupplierService();
            _productService  = new ProductService();
            _billScanService = new BillScanService();

            AddItemCommand         = new RelayCommand(_ => AddItemToGrid());
            PurchaseDeleteCommand  = new RelayCommand(p => RemoveItemFromGrid(p as MPurchaseDetail));
            PurchaseSaveCommand    = new RelayCommand(_ => SavePurchase());
            PurchaseResetCommand   = new RelayCommand(_ => ResetForm());
            BarcodeSearchCommand   = new RelayCommand(p => HandleBarcodeSearch(p?.ToString()));
            OpenAddSupplierCommand = new RelayCommand(_ => OpenSupplierWindow());
            ScanBillCommand        = new RelayCommand(async _ => await ExecuteScanBillAsync());
            OpenApiKeySetupCommand = new RelayCommand(_ => OpenApiKeySetup());

            InitializeData();
        }

        // ════════════════════════════════════════════════════════════════════════
        // SCAN BILL FLOW
        // ════════════════════════════════════════════════════════════════════════
        private async System.Threading.Tasks.Task ExecuteScanBillAsync()
        {
            // ── STEP 1: Ensure Groq API key is configured ─────────────────────
            if (!ApiKeyManager.HasKey())
            {
                var keyWin = new ApiKeySetupWindow
                {
                    Owner = Application.Current.MainWindow
                };
                if (keyWin.ShowDialog() != true) return;
                // Key is now saved — continue
            }

            // ── STEP 2: Enforce supplier selection ────────────────────────────
            if (SelectedSupplier == null)
            {
                var result = MessageBox.Show(
                    "Please select a supplier before scanning a bill.\n\n" +
                    "Click Yes to open the Add Supplier window, or No to select an existing one.",
                    "Supplier Required", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                    OpenSupplierWindow();

                return;
            }

            // ── STEP 3: Pick bill file ─────────────────────────────────────────
            var ofd = new OpenFileDialog
            {
                Title  = "Select Purchase Bill (PDF or Image)",
                Filter = "Supported files|*.pdf;*.jpg;*.jpeg;*.png;*.webp" +
                         "|PDF|*.pdf|Images|*.jpg;*.jpeg;*.png;*.webp"
            };
            if (ofd.ShowDialog() != true) return;

            // ── STEP 4: Send to Groq AI ────────────────────────────────────────
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

            if (scanned.Items.Count == 0)
            {
                MessageBox.Show(
                    "The AI could not extract any line items from this bill.\n" +
                    "Try a clearer image or enter items manually.",
                    "No Items Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // ── STEP 5: Open review window ─────────────────────────────────────
            var reviewWin = new BillScanReviewWindow(scanned, Products.ToList())
            {
                Owner = Application.Current.MainWindow
            };

            if (reviewWin.ShowDialog() != true) return;

            // ── STEP 6: Handle new products ────────────────────────────────────
            // Reload products in case user added new ones inside the review window
            Products = new ObservableCollection<MProducts>(_productService.GetProducts());

            // ── STEP 7: Transfer approved items ───────────────────────────────
            TransferScannedItems(reviewWin.ApprovedBill);
        }

        // ── Transfer all scanned items with correct qty, price, tax ──────────
        private void TransferScannedItems(ScannedBillResult approved)
        {
            // Fill invoice header fields if blank
            if (string.IsNullOrWhiteSpace(PurchaseMaster.InvoiceNumber) &&
                !string.IsNullOrWhiteSpace(approved.InvoiceNumber))
                PurchaseMaster.InvoiceNumber = approved.InvoiceNumber;

            if (!string.IsNullOrWhiteSpace(approved.InvoiceDate) &&
                DateTime.TryParseExact(approved.InvoiceDate, "dd-MM-yyyy",
                    null, System.Globalization.DateTimeStyles.None, out DateTime d))
                PurchaseMaster.PurchaseDate = d;

            int added = 0;

            foreach (var item in approved.Items.Where(i => i.MatchedProductId > 0))
            {
                // Always look up from the refreshed Products list
                var product = Products.FirstOrDefault(p => p.Id == item.MatchedProductId);
                if (product == null) continue;

                // Use scanned rate if available, else fall back to stored purchase price
                decimal price = item.Rate > 0 ? item.Rate : product.PurchasePrice;
                double  qty   = item.Quantity > 0 ? item.Quantity : 1;

                // Tax calculation
                decimal taxRate  = (decimal)(product.CGST + product.SGST + product.CESS);
                decimal subtotal = (decimal)qty * price;
                decimal netAmt   = subtotal + subtotal * taxRate / 100m;

                var existing = PurchaseItems.FirstOrDefault(pi => pi.ProductId == product.Id);
                if (existing != null)
                {
                    // Update existing row — remove/re-insert to force UI refresh
                    int idx = PurchaseItems.IndexOf(existing);
                    existing.Quantity      = qty;
                    existing.PurchasePrice = price;
                    existing.AfterTaxation = netAmt;
                    PurchaseItems.RemoveAt(idx);
                    PurchaseItems.Insert(idx, existing);
                }
                else
                {
                    PurchaseItems.Add(new MPurchaseDetail
                    {
                        ProductId      = product.Id,
                        Product        = product,
                        Quantity       = qty,
                        PurchasePrice  = price,
                        AfterTaxation  = netAmt
                    });
                }
                added++;
            }

            RecalculateTotal();
            OnPropertyChanged(nameof(PurchaseMaster));

            MessageBox.Show(
                $"✔  {added} item(s) transferred from scanned bill.\n" +
                "You can edit quantities and prices directly in the grid.\n" +
                "Click SAVE INVOICE when ready.",
                "Transfer Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── Called from code-behind after grid cell edit ──────────────────────
        public void RecalculateTotal()
        {
            decimal gross = PurchaseItems.Sum(x => x.AfterTaxation);
            PurchaseMaster.TotalAmount = gross - PurchaseMaster.Discount;
            OnPropertyChanged(nameof(PurchaseMaster));
        }

        // ════════════════════════════════════════════════════════════════════════
        // Barcode search — with Add New Product option
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
                    $"Barcode '{barcode}' not found in your product list.\n\nCreate a new product?",
                    "Product Not Found", MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes)
                {
                    var addWin = new AddProductWindow(barcode)
                    {
                        Owner = Application.Current.MainWindow
                    };
                    if (addWin.ShowDialog() == true)
                    {
                        Products = new ObservableCollection<MProducts>(_productService.GetProducts());
                        var newProd = Products.FirstOrDefault(p => p.Barcode == barcode);
                        if (newProd != null) AddToCart(newProd);
                    }
                }
            }
        }

        private void AddToCart(MProducts product)
        {
            var existing = PurchaseItems.FirstOrDefault(i => i.ProductId == product.Id);

            if (existing != null)
            {
                int index = PurchaseItems.IndexOf(existing);
                existing.Quantity++;
                decimal taxRate  = (decimal)(product.CGST + product.SGST + product.CESS);
                decimal subtotal = (decimal)existing.Quantity * existing.PurchasePrice;
                existing.AfterTaxation = subtotal + subtotal * taxRate / 100m;
                PurchaseItems.RemoveAt(index);
                PurchaseItems.Insert(index, existing);
            }
            else
            {
                decimal taxRate = (decimal)(product.CGST + product.SGST + product.CESS);
                PurchaseItems.Add(new MPurchaseDetail
                {
                    ProductId      = product.Id,
                    Product        = product,
                    Quantity       = 1,
                    PurchasePrice  = product.PurchasePrice,
                    AfterTaxation  = product.PurchasePrice + product.PurchasePrice * taxRate / 100m
                });
            }
            RecalculateTotal();
        }

        private void OpenApiKeySetup()
        {
            var win = new ApiKeySetupWindow
            {
                Owner = Application.Current.MainWindow
            };
            win.ShowDialog();
        }

        private void OpenSupplierWindow()
        {
            var win = new AddSupplierWindow { Owner = Application.Current.MainWindow };
            if (win.ShowDialog() == true)
            {
                Suppliers = new ObservableCollection<MSupplier>(_supplierService.GetAllSuppliers());
                OnPropertyChanged(nameof(Suppliers));
                SelectedSupplier = Suppliers.FirstOrDefault(
                    s => s.SupplierName == win.NewSupplier?.SupplierName);
            }
        }

        private void InitializeData()
        {
            PurchaseMaster = new MPurchaseMaster { PurchaseDate = DateTime.Now };
            NewItem        = new MPurchaseDetail();
            PurchaseItems  = new ObservableCollection<MPurchaseDetail>();
            Suppliers      = new ObservableCollection<MSupplier>(_supplierService.GetAllSuppliers());
            Products       = new ObservableCollection<MProducts>(_productService.GetProducts());
        }

        private void AddItemToGrid()
        {
            if (SelectedProduct == null || NewItem.Quantity <= 0)
            {
                MessageBox.Show("Please select a product and enter a valid quantity.");
                return;
            }
            AddToCart(SelectedProduct);
            NewItem         = new MPurchaseDetail();
            SelectedProduct = null;
        }

        private void RemoveItemFromGrid(MPurchaseDetail item)
        {
            if (item != null && PurchaseItems.Contains(item))
            {
                PurchaseItems.Remove(item);
                RecalculateTotal();
            }
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
            OnPropertyChanged(nameof(ScanHintText));
            OnPropertyChanged(nameof(ScanHintVisibility));
        }
    }
}
