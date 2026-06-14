using Microsoft.Win32;
using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using MyWPFCRUDApp.Views;
using System;
using System.Windows.Media;
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
        private readonly TaxService _taxService;
        private long _editingMasterId = 0;
        // ── Commands ───────────────────────────────────────────────────────────
        public ICommand AddItemCommand { get; }
        public ICommand PurchaseDeleteCommand { get; }
        public ICommand PurchaseSaveCommand { get; }
        public ICommand PurchaseResetCommand { get; }
        public ICommand BarcodeSearchCommand { get; }
        public ICommand OpenAddSupplierCommand { get; }
        public ICommand ScanBillCommand { get; }
        public ICommand OpenApiKeySetupCommand { get; }
        public ICommand ToggleHistoryCommand { get; }
        public ICommand LoadHistoryInvoiceCommand { get; }

        // ── Collections ────────────────────────────────────────────────────────
        public ObservableCollection<MSupplier> Suppliers { get; set; }

        private ObservableCollection<MPurchaseMaster> _supplierHistory;
        public ObservableCollection<MPurchaseMaster> SupplierHistory
        {
            get => _supplierHistory;
            set => SetProperty(ref _supplierHistory, value);
        }

        private bool _isHistoryOpen;
        public bool IsHistoryOpen
        {
            get => _isHistoryOpen;
            set => SetProperty(ref _isHistoryOpen, value);
        }
        public ObservableCollection<MProducts> Products { get; set; }
        public ObservableCollection<MPurchaseDetail> PurchaseItems { get; set; }
        public ObservableCollection<MTaxCategory> TaxCategories { get; set; }


        private MTaxCategory _selectedTaxCategory;
        public MTaxCategory SelectedTaxCategory
        {
            get => _selectedTaxCategory;
            set
            {
                if (SetProperty(ref _selectedTaxCategory, value) && value != null)
                {
                    TaxContext.SelectedTax = value;

                    DetermineApplicableTaxes();
                    RecalculateTotal();
                }
            }
        }

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
                if (SetProperty(ref _selectedSupplier, value))
                {
                    if (value != null)
                    {
                        PurchaseMaster.SupplierId = value.Id;

                        // Load supplier balance
                        SupplierBalance = value.CurrentBalance;

                        LoadSupplierHistory(value.Id);
                    }
                    else
                    {
                        SupplierBalance = 0;
                        SupplierHistory = new ObservableCollection<MPurchaseMaster>();
                    }

                    IsHistoryOpen = false;
                    DetermineApplicableTaxes();
                    OnPropertyChanged(nameof(ScanHintText));
                    OnPropertyChanged(nameof(ScanHintVisibility));
                }
            }
        }
        private decimal _purchasePrice;
        public decimal PurchasePrice
        {
            get => _purchasePrice;
            set
            {
                if (_purchasePrice != value)
                {
                    _purchasePrice = value;
                    OnPropertyChanged();
                    RecalcAmount();
                }
            }
        }

        private double _quantity;
        public double Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity != value)
                {
                    _quantity = value;
                    OnPropertyChanged();
                    RecalcAmount();
                }
            }
        }

        private decimal _afterTaxation;
        public decimal AfterTaxation
        {
            get => _afterTaxation;
            set
            {
                if (_afterTaxation != value)
                {
                    _afterTaxation = value;
                    OnPropertyChanged();
                }
            }
        }

        private void RecalcAmount()
        {
            if (_quantity > 0 && _purchasePrice > 0)
                AfterTaxation = (decimal)_quantity * _purchasePrice;
        }
        private decimal _amountPaid;
        public decimal AmountPaid
        {
            get => _amountPaid;
            set
            {
                if (SetProperty(ref _amountPaid, value))
                {
                    PurchaseMaster.AmountPaid = value;

                    OnPropertyChanged(nameof(BalanceAmount));
                    OnPropertyChanged(nameof(BalanceBrush));
                }
            }
        }

        private string _paymentMethod = "Cash";
        public string PaymentMethod
        {
            get => _paymentMethod;
            set
            {
                if (SetProperty(ref _paymentMethod, value))
                {
                    PurchaseMaster.PaymentMode = value;
                }
            }
        }

        public decimal BalanceAmount =>
            AmountPaid - PurchaseMaster.TotalAmount;

        public Brush BalanceBrush
        {
            get
            {
                if (BalanceAmount > 0)
                    return Brushes.Green;

                if (BalanceAmount < 0)
                    return Brushes.Red;

                return Brushes.Black;
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

        // ── Scan hint (shown when no supplier selected) ────────────────────────
        public string ScanHintText => SelectedSupplier == null
            ? "Select supplier first" : string.Empty;

        public Visibility ScanHintVisibility => SelectedSupplier == null
            ? Visibility.Visible : Visibility.Collapsed;

        // ── My company GST number (loaded once; used for GST state comparison) ─
        private string _myGSTNumber = string.Empty;
        public string MyGSTNumber
        {
            get => _myGSTNumber;
            set
            {
                if (SetProperty(ref _myGSTNumber, value))
                    DetermineApplicableTaxes();
            }
        }
        
        // ── GST type flags ─────────────────────────────────────────────────────
        private bool _isSameState;
        public bool IsSameState
        {
            get => _isSameState;
            private set => SetProperty(ref _isSameState, value);
        }

        // ── Tax percentages (read from Tax Section via TaxService; never hardcoded) ─
        private decimal _cgstPercent;
        /// <summary>
        /// CGST% loaded from the Tax Section record whose CategoryName contains "CGST".
        /// Bound to the CGST row in the Invoice Summary panel (PurchaseViews.xaml).
        /// </summary>
        public decimal CGSTPercent
        {
            get => _cgstPercent;
            set { if (SetProperty(ref _cgstPercent, value)) RecalculateTotal(); }
        }

        private decimal _sgstPercent;
        /// <summary>
        /// SGST% loaded from the Tax Section record whose CategoryName contains "SGST".
        /// Bound to the SGST row in the Invoice Summary panel (PurchaseViews.xaml).
        /// </summary>
        public decimal SGSTPercent
        {
            get => _sgstPercent;
            set { if (SetProperty(ref _sgstPercent, value)) RecalculateTotal(); }
        }

        private decimal _igstPercent;
        /// <summary>
        /// IGST% loaded from the Tax Section record whose CategoryName contains "IGST".
        /// Bound to the IGST row in the Invoice Summary panel (PurchaseViews.xaml).
        /// </summary>
        public decimal IGSTPercent
        {
            get => _igstPercent;
            set { if (SetProperty(ref _igstPercent, value)) RecalculateTotal(); }
        }

        // ── Computed tax amounts (read-only; updated inside RecalculateTotal) ──
        private decimal _cgstAmount;
        public decimal CGSTAmount
        {
            get => _cgstAmount;
            private set => SetProperty(ref _cgstAmount, value);
        }

        private decimal _sgstAmount;
        public decimal SGSTAmount
        {
            get => _sgstAmount;
            private set => SetProperty(ref _sgstAmount, value);
        }

        private decimal _igstAmount;
        public decimal IGSTAmount
        {
            get => _igstAmount;
            private set => SetProperty(ref _igstAmount, value);
        }

        // ── NetAmount = sum of all line AfterTaxation values ──────────────────
        private decimal _netAmount;
        public decimal NetAmount
        {
            get => _netAmount;
            private set => SetProperty(ref _netAmount, value);
        }
        private decimal _supplierBalance;
        public decimal SupplierBalance
        {
            get => _supplierBalance;
            set => SetProperty(ref _supplierBalance, value);
        }
        private decimal _discount;
        public decimal Discount
        {
            get => _discount;
            set
            {
                if (SetProperty(ref _discount, value))
                {
                    PurchaseMaster.Discount = value;
                    RecalculateTotal();
                }
            }
        }
        private decimal _retail;
        public decimal Retail
        {
            get => _retail;
            set => SetProperty(ref _retail, value);
        }

        // ════════════════════════════════════════════════════════════════════════
        // ISSUE #3 — DetermineApplicableTaxes()
        //
        // PURPOSE
        //   Compares the first 2 characters of the Supplier's GSTIN against the
        //   Company's GSTNumber.  The first 2 digits of a GSTIN identify the
        //   Indian state, so a match means an intra-state transaction (CGST+SGST)
        //   and a mismatch means inter-state (IGST).
        //
        // TAX PERCENTAGES
        //   • CGSTPercent  — read from _cgstPercent (loaded from MTaxCategory DB row
        //                    whose CategoryName contains "CGST").
        //   • SGSTPercent  — read from _sgstPercent (loaded from MTaxCategory DB row
        //                    whose CategoryName contains "SGST").
        //   • IGSTPercent  — read from _igstPercent (loaded from MTaxCategory DB row
        //                    whose CategoryName contains "IGST").
        //   No percentage is ever hardcoded here.
        //
        // RULE
        //   Same first-2 chars  → CGST% and SGST% apply; IGST% = 0
        //   Different           → IGST% applies;          CGST% = SGST% = 0
        //
        // CALLED FROM
        //   1. SelectedSupplier setter  — whenever the user picks a different supplier.
        //   2. MyGSTNumber setter       — when the company GST is refreshed at startup.
        //   3. InitializeData()         — once at form load, after loading company GST.
        // ════════════════════════════════════════════════════════════════════════
        private void DetermineApplicableTaxes()
        {

            string supplierGST = SelectedSupplier?.GSTIN ?? string.Empty;
            string companyGST = MyGSTNumber ?? string.Empty;

            bool sameState =
                supplierGST.Length >= 2 &&
                companyGST.Length >= 2 &&
                supplierGST.Substring(0, 2) == companyGST.Substring(0, 2);

            if (sameState)
            {
                // Same State
                CGSTPercent = TaxContext.SelectedTax?.CGST ?? 0;
                SGSTPercent = TaxContext.SelectedTax?.SGST ?? 0;
                IGSTPercent = 0;

                PurchaseMaster.CGST_Applicable = true;
                PurchaseMaster.SGST_Applicable = true;
                PurchaseMaster.IGST_Applicable = false;
            }
            else
            {
                // Different State
                CGSTPercent = 0;
                SGSTPercent = 0;
                IGSTPercent = TaxContext.SelectedTax?.IGST ?? 0;

                PurchaseMaster.CGST_Applicable = false;
                PurchaseMaster.SGST_Applicable = false;
                PurchaseMaster.IGST_Applicable = true;
            }

            RecalculateTotal();
        }

        // ── Constructor ────────────────────────────────────────────────────────
        public PurchaseViewModel()
        {
            _purchaseService = new PurchaseService();
            _supplierService = new SupplierService();
            _productService  = new ProductService();
            _billScanService = new BillScanService();
            _taxService      = new TaxService();

            AddItemCommand        = new RelayCommand(_ => AddItemToGrid());
            PurchaseDeleteCommand = new RelayCommand(p => RemoveItemFromGrid(p as MPurchaseDetail));
            PurchaseSaveCommand   = new RelayCommand(_ => SavePurchase());
            PurchaseResetCommand  = new RelayCommand(_ => ResetForm());
            BarcodeSearchCommand  = new RelayCommand(p => HandleBarcodeSearch(p?.ToString()));
            OpenAddSupplierCommand = new RelayCommand(_ => OpenSupplierWindow());
            ScanBillCommand       = new RelayCommand(async _ => await ExecuteScanBillAsync());
            OpenApiKeySetupCommand = new RelayCommand(_ => OpenApiKeySetup());
            ToggleHistoryCommand  = new RelayCommand(_ => ToggleHistory());
            LoadHistoryInvoiceCommand = new RelayCommand(p => LoadHistoryInvoice(p as MPurchaseMaster));

            InitializeData();
        }
        private void LoadHistoryInvoice(MPurchaseMaster master)
        {
            if (master == null) return;

            _editingMasterId = master.Id;   // ← key fix
            PurchaseMaster.InvoiceNumber = master.InvoiceNumber;
            PurchaseMaster.PurchaseDate = master.PurchaseDate;
            PurchaseMaster.Discount = master.Discount;
            Discount = master.Discount;
            OnPropertyChanged(nameof(PurchaseMaster));

            PurchaseItems.Clear();
            foreach (var d in master.Details)
            {
                PurchaseItems.Add(new MPurchaseDetail
                {
                    ProductId = d.ProductId,
                    ProductName = d.ProductName,
                    Barcode = d.Barcode,
                    Quantity = d.Quantity,
                    PurchasePrice = d.PurchasePrice,
                    WholesalePrice = d.WholesalePrice,
                    MRP = d.MRP,
                    Retail = d.Retail,
                    AfterTaxation = d.AfterTaxation,
                });
            }

            RecalculateTotal();
            OnPropertyChanged(nameof(PurchaseItems));
        }
        // ════════════════════════════════════════════════════════════════════════
        // SCAN BILL FLOW
        // ════════════════════════════════════════════════════════════════════════
        private async System.Threading.Tasks.Task ExecuteScanBillAsync()
        {
            if (!ApiKeyManager.HasKey())
            {
                var keyWin = new ApiKeySetupWindow { Owner = Application.Current.MainWindow };
                if (keyWin.ShowDialog() != true) return;
            }

            if (SelectedSupplier == null)
            {
                var result = MessageBox.Show(
                    "Please select a supplier before scanning a bill.\n\n" +
                    "Click Yes to open the Add Supplier window, or No to select an existing one.",
                    "Supplier Required", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (result == MessageBoxResult.Yes) OpenSupplierWindow();
                return;
            }

            var ofd = new OpenFileDialog
            {
                Title  = "Select Purchase Bill (PDF or Image)",
                Filter = "Supported files|*.pdf;*.jpg;*.jpeg;*.png;*.webp" +
                         "|PDF|*.pdf|Images|*.jpg;*.jpeg;*.png;*.webp"
            };
            if (ofd.ShowDialog() != true) return;

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

            var reviewWin = new BillScanReviewWindow(scanned) { Owner = Application.Current.MainWindow };
            if (reviewWin.ShowDialog() != true) return;

            TransferScannedItems(reviewWin.ApprovedBill);
        }

        private void TransferScannedItems(ScannedBillResult approved)
        {
            if (string.IsNullOrWhiteSpace(PurchaseMaster.InvoiceNumber) &&
                !string.IsNullOrWhiteSpace(approved.InvoiceNumber))
                PurchaseMaster.InvoiceNumber = approved.InvoiceNumber;

            if (!string.IsNullOrWhiteSpace(approved.InvoiceDate) &&
                DateTime.TryParseExact(approved.InvoiceDate, "dd-MM-yyyy",
                    null, System.Globalization.DateTimeStyles.None, out DateTime d))
                PurchaseMaster.PurchaseDate = d;

            long existingCount = _productService.GetProductCount();
            int added = 0;

            foreach (var item in approved.Items)
            {
                double qty   = item.Quantity > 0 ? item.Quantity : 1;
                decimal price  = item.PurchasePrice;
                decimal netAmt = (decimal)qty * price;
                string barcode = $"M{existingCount + added + 1}";

                PurchaseItems.Add(new MPurchaseDetail
                {
                    ProductId = 0,
                    ProductName = item.Description,
                    Barcode = barcode,
                    Quantity = qty,
                    PurchasePrice = price,
                    WholesalePrice = item.WholesalePrice,
                    MRP = item.MRP,
                    Retail = item.RetailPrice,   // ← add this
                    AfterTaxation = netAmt
                });
                added++;
            }

            RecalculateTotal();
            OnPropertyChanged(nameof(PurchaseMaster));

            MessageBox.Show(
                $"✔  {added} item(s) transferred from scanned bill.\n" +
                "Barcodes have been pre-assigned. Edit quantities and prices directly in the grid.\n" +
                "Click SAVE INVOICE when ready.",
                "Transfer Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ════════════════════════════════════════════════════════════════════════
        // RecalculateTotal — uses the applicable percents set by
        //                    DetermineApplicableTaxes(); never hardcodes any %.
        // ════════════════════════════════════════════════════════════════════════
        public void RecalculateTotal()
        {
            // Net Amount from purchase items
            NetAmount = PurchaseItems?.Sum(x => x.AfterTaxation) ?? 0m;

            decimal discount = Discount;

            decimal balance = NetAmount - discount;

            if (balance < 0)
                balance = 0;

            CGSTAmount = Math.Round(
                balance * CGSTPercent / 100m, 2);

            SGSTAmount = Math.Round(
                balance * SGSTPercent / 100m, 2);

            IGSTAmount = Math.Round(
                balance * IGSTPercent / 100m, 2);

            PurchaseMaster.CGSTAmount = CGSTAmount;
            PurchaseMaster.SGSTAmount = SGSTAmount;
            PurchaseMaster.IGSTAmount = IGSTAmount;

            PurchaseMaster.TotalAmount =
                balance +
                CGSTAmount +
                SGSTAmount +
                IGSTAmount;

            OnPropertyChanged(nameof(NetAmount));
            OnPropertyChanged(nameof(CGSTAmount));
            OnPropertyChanged(nameof(SGSTAmount));
            OnPropertyChanged(nameof(IGSTAmount));
            OnPropertyChanged(nameof(PurchaseMaster));
            OnPropertyChanged(nameof(BalanceAmount));
            OnPropertyChanged(nameof(BalanceBrush));
        }

        // ════════════════════════════════════════════════════════════════════════
        // Barcode search
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
                    var addWin = new AddProductWindow(barcode) { Owner = Application.Current.MainWindow };
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
                decimal subtotal = (decimal)existing.Quantity * existing.PurchasePrice;
                existing.AfterTaxation = subtotal;
                PurchaseItems.RemoveAt(index);
                PurchaseItems.Insert(index, existing);
            }
            else
            {
                PurchaseItems.Add(new MPurchaseDetail
                {
                    ProductId = product.Id,
                    ProductName = product.ProductName,
                    Barcode = product.Barcode,
                    Product = product,
                    Quantity = 1,
                    PurchasePrice = product.PurchasePrice,
                    WholesalePrice = product.WholesalePrice,
                    MRP = product.MRP,
                    Retail = product.RetailSalePrice,   // ← add
                    AfterTaxation = product.PurchasePrice
                });
            }
            RecalculateTotal();
        }

        private void ToggleHistory()
        {
            Console.WriteLine($"[ToggleHistory] Called. SelectedSupplier={SelectedSupplier?.SupplierName ?? "null"}");

            if (SelectedSupplier == null)
            {
                Console.WriteLine("[ToggleHistory] No supplier selected — skipping.");
                MessageBox.Show("Please select a supplier first to view purchase history.",
                                "No Supplier", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Console.WriteLine($"[ToggleHistory] Loading history for SupplierId={SelectedSupplier.Id}");
            LoadSupplierHistory(SelectedSupplier.Id);

            IsHistoryOpen = !IsHistoryOpen;
            Console.WriteLine($"[ToggleHistory] IsHistoryOpen is now {IsHistoryOpen}. " +
                              $"SupplierHistory count={SupplierHistory?.Count ?? -1}");
        }

        private void LoadSupplierHistory(long supplierId)
        {
            Console.WriteLine($"[LoadSupplierHistory] Fetching for SupplierId={supplierId}");
            try
            {
                var records = _purchaseService.GetPurchasesBySupplier(supplierId);
                Console.WriteLine($"[LoadSupplierHistory] Got {records.Count} records from DB.");
                SupplierHistory = new ObservableCollection<MPurchaseMaster>(
                    records.OrderByDescending(r => r.PurchaseDate));
                Console.WriteLine($"[LoadSupplierHistory] SupplierHistory set with {SupplierHistory.Count} items.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LoadSupplierHistory] EXCEPTION: {ex.Message}\n{ex.StackTrace}");
                SupplierHistory = new ObservableCollection<MPurchaseMaster>();
            }
        }

        private void OpenApiKeySetup()
        {
            var win = new ApiKeySetupWindow { Owner = Application.Current.MainWindow };
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
            string nextInvoice = "";
            try
            {
                long count = _purchaseService.GetPurchaseCount();
                nextInvoice = (count + 1).ToString();
            }
            catch { }

            PurchaseMaster = new MPurchaseMaster
            {
                PurchaseDate = DateTime.Now,
                InvoiceNumber = nextInvoice,
                Discount = 0
            };
            // Add this line inside InitializeData(), alongside the other collection initializations:
            SupplierHistory = new ObservableCollection<MPurchaseMaster>();
            Discount = PurchaseMaster.Discount;
            NewItem       = new MPurchaseDetail();
            PurchaseItems = new ObservableCollection<MPurchaseDetail>();
            Suppliers     = new ObservableCollection<MSupplier>(_supplierService.GetAllSuppliers());
            Products      = new ObservableCollection<MProducts>(_productService.GetProducts());
            
            
            // ── Load company GST (the "My" side of DetermineApplicableTaxes) ──
            try
            {
                var companyService = new CompanyService();
                var company = companyService.GetCompanyInfo();
                var companies = companyService.GetCompanyInfo();

                _myGSTNumber = companies.FirstOrDefault()?.GSTNumber ?? string.Empty;
            }
            catch { _myGSTNumber = string.Empty; }

            // ── Load tax percentages from Tax Section (MTaxCategory table) ────
            // Convention: CategoryName contains "CGST", "SGST", or "IGST"
            // (case-insensitive).  The first matching record for each type wins.
            // Backing fields are set directly here to avoid three separate
            // RecalculateTotal() calls during initialisation.
            try
            {
                // Load all tax configurations for dropdown
                TaxCategories = new ObservableCollection<MTaxCategory>(
                    _taxService.GetTaxCategory());

                // Restore previously selected tax configuration
                SelectedTaxCategory =
                    TaxContext.SelectedTax ??
                    TaxCategories.FirstOrDefault();

                // Ensure global context is initialized
                TaxContext.SelectedTax = SelectedTaxCategory;

                if (SelectedTaxCategory != null)
                {
                    _cgstPercent = SelectedTaxCategory.CGST;
                    _sgstPercent = SelectedTaxCategory.SGST;
                    _igstPercent = SelectedTaxCategory.IGST;
                }
                else
                {
                    _cgstPercent = 0;
                    _sgstPercent = 0;
                    _igstPercent = 0;
                }

                OnPropertyChanged(nameof(CGSTPercent));
                OnPropertyChanged(nameof(SGSTPercent));
                OnPropertyChanged(nameof(IGSTPercent));
                OnPropertyChanged(nameof(PurchaseItems));
            }
            catch
            {
                _cgstPercent = 0;
                _sgstPercent = 0;
                _igstPercent = 0;
            }

            // ── Run DetermineApplicableTaxes() once so flags and totals are
            //    correct from the moment the form opens ─────────────────────────
            DetermineApplicableTaxes();
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

        public void RemoveItem(MPurchaseDetail item)
        {
            if (item != null && PurchaseItems.Contains(item))
            {
                PurchaseItems.Remove(item);
                RecalculateTotal();
            }
        }

        private void RemoveItemFromGrid(MPurchaseDetail item) => RemoveItem(item);

        private void SavePurchase()
        {
            if (SelectedSupplier == null)
            { MessageBox.Show("Please select a supplier."); return; }
            if (!PurchaseItems.Any())
            { MessageBox.Show("Please add at least one item."); return; }

            var cats = new CategoryService().GetCategory();
            var subs = new SubCategoryService().GetSubCategoryList();
            var units = new UnitService().GetUnit();
            long defaultCatId = cats.Any() ? cats.First().Id : 1;
            long defaultSubId = subs.Any() ? subs.First().Id : 1;
            long defaultUnitId = units.Any() ? units.First().Id : 1;

            int newProductsCreated = 0;

            foreach (var item in PurchaseItems)
            {
                if (item.ProductId > 0) continue;

                var existing = _productService.GetByBarcode(item.Barcode);
                if (existing != null) { item.ProductId = existing.Id; continue; }

                var newProduct = new MProducts
                {
                    ProductName = item.ProductName,
                    Barcode = item.Barcode,
                    CategoryId = defaultCatId,
                    SubCategoryId = defaultSubId,
                    UnitId = defaultUnitId,
                    PurchasePrice = item.PurchasePrice,
                    WholesalePrice = item.WholesalePrice,
                    RetailSalePrice = item.Retail,
                    MRP = item.MRP,
                    CGST = 0,
                    SGST = 0,
                    IGST = 0,
                    CESS = 0,
                };

                if (_productService.InsertProduct(newProduct))
                {
                    var inserted = _productService.GetByBarcode(item.Barcode);
                    if (inserted != null) { item.ProductId = inserted.Id; newProductsCreated++; }
                }
                else
                {
                    MessageBox.Show($"Failed to save product '{item.ProductName}'.",
                        "Product Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            PurchaseMaster.PaymentMode = PaymentMethod;
            PurchaseMaster.AmountPaid = AmountPaid;
            PurchaseMaster.SupplierId = SelectedSupplier.Id;
            PurchaseMaster.MPurchaseDetail = PurchaseItems.ToList();

            bool success;

            if (_editingMasterId > 0)
            {
                // UPDATE existing invoice — no supplier balance change
                success = _purchaseService.UpdatePurchase(_editingMasterId, PurchaseMaster);
            }
            else
            {
                // INSERT new invoice + adjust supplier balance
                decimal bal = BalanceAmount;
                if (bal > 0) SelectedSupplier.CurrentBalance -= bal;
                else if (bal < 0) SelectedSupplier.CurrentBalance += Math.Abs(bal);
                _supplierService.UpdateSupplier(SelectedSupplier);

                success = _purchaseService.AddPurchase(PurchaseMaster);
            }

            if (success)
            {
                var updatedSupplier = _supplierService.GetAllSuppliers()
                                        .FirstOrDefault(x => x.Id == SelectedSupplier.Id);
                if (updatedSupplier != null)
                {
                    SelectedSupplier = updatedSupplier;
                    SupplierBalance = updatedSupplier.CurrentBalance;
                }

                string msg = _editingMasterId > 0
                    ? "✔ Invoice updated successfully!"
                    : newProductsCreated > 0
                        ? $"✔ Purchase recorded!\n📦 {newProductsCreated} new product(s) added."
                        : "✔ Purchase recorded and stock updated!";

                MessageBox.Show(msg, "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                ResetForm();
            }
            else
            {
                MessageBox.Show("Error occurred while saving.");
            }
        }

        private void ResetForm()
        {
            _editingMasterId = 0;   // ← reset edit mode
            InitializeData();
            SelectedSupplier = null;
            SupplierHistory = new ObservableCollection<MPurchaseMaster>();
            IsHistoryOpen = false;
            OnPropertyChanged(nameof(ScanHintText));
            OnPropertyChanged(nameof(ScanHintVisibility));
        }
    }
}
