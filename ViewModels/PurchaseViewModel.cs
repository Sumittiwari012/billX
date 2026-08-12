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
       
        public ICommand ImportExcelCommand { get; }   // ← ADD THIS

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
        private string _vendorInvoiceNumber = string.Empty;
        public string VendorInvoiceNumber
        {
            get => _vendorInvoiceNumber;
            set => SetProperty(ref _vendorInvoiceNumber, value);
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

                        // Recalculate from DB instead of trusting the cached
                        // CurrentBalance from the Suppliers list, which may be
                        // stale if the balance changed since it was last loaded.
                        decimal freshBalance = _supplierService.RecalculateAndUpdateSupplierBalance(value.Id);
                        SupplierBalance = freshBalance;
                        value.CurrentBalance = freshBalance;   // keep the cached list in sync too

                        LoadSupplierHistory();
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
            
            ImportExcelCommand = new RelayCommand(_ => ImportItemsFromExcel());   // ← ADD THIS

            
            InitializeData();
        }
        // ════════════════════════════════════════════════════════════════════════
        // EXCEL IMPORT
        //
        // Expected sheet: header row + one row per item. Required column:
        //   "Barcode"
        // Optional columns:
        //   "ProductName"   — only needed for NEW barcodes (see below). Ignored
        //                     for barcodes that already exist — the product
        //                     master's name is always used for those.
        //   "Quantity"      — defaults to 1 if missing/blank/invalid.
        //   "PurchasePrice" — overrides the product master's price for known
        //                     barcodes; REQUIRED (defaults to 0) for new barcodes.
        //   "RetailPrice"   — same idea, for retail price.
        //
        // Barcode matching, same as HandleBarcodeSearch/AddToCart:
        //   • FOUND in Products    → existing product's name/wholesale/MRP used,
        //                            PurchasePrice/RetailPrice overridden if given.
        //   • NOT FOUND in Products → added as a NEW item with ProductId = 0,
        //                            using the row's ProductName/PurchasePrice/
        //                            RetailPrice. Exactly like a scanned bill's
        //                            unmatched item — SavePurchase() already knows
        //                            how to auto-create a product for ProductId==0
        //                            rows when you click SAVE INVOICE, so nothing
        //                            else needs to change for this to work.
        //
        // Reading stops at the first row with a blank Barcode cell, so anything
        // below the data block (notes, legends, blank rows) is never parsed as
        // an item.
        // ════════════════════════════════════════════════════════════════════════
        private void ImportItemsFromExcel()
        {
            if (SelectedSupplier == null)
            {
                var result = MessageBox.Show(
                    "Please select a supplier before importing items.\n\n" +
                    "Click Yes to open the Add Supplier window, or No to select an existing one.",
                    "Supplier Required", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (result == MessageBoxResult.Yes) OpenSupplierWindow();
                return;
            }

            var ofd = new OpenFileDialog
            {
                Title = "Select Excel Sheet",
                Filter = "Excel Files|*.xlsx;*.xls"
            };
            if (ofd.ShowDialog() != true) return;

            int added = 0;
            int updated = 0;
            int newBarcodesAdded = 0;
            var skippedNoName = new System.Collections.Generic.List<string>();

            try
            {
                using var workbook = new ClosedXML.Excel.XLWorkbook(ofd.FileName);
                var ws = workbook.Worksheets.First();
                var headerRow = ws.FirstRowUsed();

                if (headerRow == null)
                {
                    MessageBox.Show("The selected sheet appears to be empty.", "Empty Sheet",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Map header name -> column index (case-insensitive, trims whitespace)
                var colMap = new System.Collections.Generic.Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (var cell in headerRow.CellsUsed())
                    colMap[cell.GetString().Trim()] = cell.Address.ColumnNumber;

                if (!colMap.TryGetValue("Barcode", out int barcodeCol))
                {
                    MessageBox.Show(
                        "The Excel sheet must have a 'Barcode' column.\n\n" +
                        "Optional columns: 'ProductName' (for new barcodes), 'Quantity', " +
                        "'PurchasePrice', 'RetailPrice'.",
                        "Invalid Sheet", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int? nameCol = colMap.TryGetValue("ProductName", out var nc) ? nc : (int?)null;
                int? qtyCol = colMap.TryGetValue("Quantity", out var qc) ? qc : (int?)null;
                int? priceCol = colMap.TryGetValue("PurchasePrice", out var pc) ? pc : (int?)null;
                int? retailCol = colMap.TryGetValue("RetailPrice", out var rc) ? rc : (int?)null;

                int lastRow = ws.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();

                for (int r = headerRow.RowNumber() + 1; r <= lastRow; r++)
                {
                    var row = ws.Row(r);
                    string barcode = row.Cell(barcodeCol).GetString().Trim();

                    // First blank Barcode cell = end of the data block. Everything
                    // below (notes, legends, blank spacer rows) is ignored.
                    if (string.IsNullOrWhiteSpace(barcode))
                        break;

                    double qty = 1;
                    if (qtyCol.HasValue)
                    {
                        var qtyCell = row.Cell(qtyCol.Value);
                        if (!qtyCell.IsEmpty() &&
                            double.TryParse(qtyCell.GetString(), out var parsedQty) &&
                            parsedQty > 0)
                        {
                            qty = parsedQty;
                        }
                    }

                    decimal? priceOverride = null;
                    if (priceCol.HasValue)
                    {
                        var priceCell = row.Cell(priceCol.Value);
                        if (!priceCell.IsEmpty() &&
                            decimal.TryParse(priceCell.GetString(), out var parsedPrice))
                        {
                            priceOverride = parsedPrice;
                        }
                    }

                    decimal? retailOverride = null;
                    if (retailCol.HasValue)
                    {
                        var retailCell = row.Cell(retailCol.Value);
                        if (!retailCell.IsEmpty() &&
                            decimal.TryParse(retailCell.GetString(), out var parsedRetail))
                        {
                            retailOverride = parsedRetail;
                        }
                    }

                    string nameFromSheet = nameCol.HasValue
                        ? row.Cell(nameCol.Value).GetString().Trim()
                        : string.Empty;

                    var product = Products.FirstOrDefault(
                        p => p.Barcode == barcode || p.ProductCode == barcode);

                    if (product != null)
                    {
                        // ── Known barcode: same as before ──
                        var existing = PurchaseItems.FirstOrDefault(i => i.ProductId == product.Id);
                        if (existing != null)
                        {
                            int index = PurchaseItems.IndexOf(existing);
                            existing.Quantity += qty;
                            if (priceOverride.HasValue) existing.PurchasePrice = priceOverride.Value;
                            if (retailOverride.HasValue) existing.Retail = retailOverride.Value;
                            existing.AfterTaxation = (decimal)existing.Quantity * existing.PurchasePrice;
                            PurchaseItems.RemoveAt(index);
                            PurchaseItems.Insert(index, existing);
                            updated++;
                        }
                        else
                        {
                            decimal price = priceOverride ?? product.PurchasePrice;
                            decimal retail = retailOverride ?? product.RetailSalePrice;
                            PurchaseItems.Add(new MPurchaseDetail
                            {
                                ProductId = product.Id,
                                ProductName = product.ProductName,
                                Barcode = product.Barcode,
                                Product = product,
                                Quantity = qty,
                                PurchasePrice = price,
                                WholesalePrice = product.WholesalePrice,
                                MRP = product.MRP,
                                Retail = retail,
                                AfterTaxation = (decimal)qty * price
                            });
                            added++;
                        }
                    }
                    else
                    {
                        // ── New barcode: needs a name to be worth anything.
                        //    Skip (with a reason) only if no name was given at all —
                        //    otherwise add as a brand-new item, ProductId = 0, same
                        //    as TransferScannedItems does for unmatched scanned lines.
                        //    SavePurchase() auto-creates the product for these when
                        //    the invoice is saved. ──
                        if (string.IsNullOrWhiteSpace(nameFromSheet))
                        {
                            skippedNoName.Add(barcode);
                            continue;
                        }

                        decimal price = priceOverride ?? 0;
                        decimal retail = retailOverride ?? 0;

                        var existingNew = PurchaseItems.FirstOrDefault(
                            i => i.ProductId == 0 && i.Barcode == barcode);

                        if (existingNew != null)
                        {
                            int index = PurchaseItems.IndexOf(existingNew);
                            existingNew.Quantity += qty;
                            if (priceOverride.HasValue) existingNew.PurchasePrice = priceOverride.Value;
                            if (retailOverride.HasValue) existingNew.Retail = retailOverride.Value;
                            existingNew.AfterTaxation = (decimal)existingNew.Quantity * existingNew.PurchasePrice;
                            PurchaseItems.RemoveAt(index);
                            PurchaseItems.Insert(index, existingNew);
                            updated++;
                        }
                        else
                        {
                            PurchaseItems.Add(new MPurchaseDetail
                            {
                                ProductId = 0,
                                ProductName = nameFromSheet,
                                Barcode = barcode,
                                Quantity = qty,
                                PurchasePrice = price,
                                WholesalePrice = 0,
                                MRP = 0,
                                Retail = retail,
                                AfterTaxation = (decimal)qty * price
                            });
                            added++;
                            newBarcodesAdded++;
                        }
                    }
                }

                RecalculateTotal();
                OnPropertyChanged(nameof(PurchaseItems));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to read Excel file:\n\n{ex.Message}",
                    "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string summary = $"✔ Import complete.\n\n" +
                $"{added} new item(s) added" +
                (newBarcodesAdded > 0 ? $" ({newBarcodesAdded} of these are brand-new products — " +
                    "they'll be created automatically when you click SAVE INVOICE)." : ".") +
                $"\n{updated} existing item(s) updated (quantity increased).";

            if (skippedNoName.Any())
            {
                summary += $"\n\n⚠ {skippedNoName.Count} unrecognized barcode(s) skipped " +
                    "(no 'ProductName' given, so a new product couldn't be created):\n" +
                    string.Join(", ", skippedNoName.Take(20)) +
                    (skippedNoName.Count > 20 ? $" ...and {skippedNoName.Count - 20} more" : "");
            }

            MessageBox.Show(summary, "Import Result", MessageBoxButton.OK,
                skippedNoName.Any() ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
        private void LoadHistoryInvoice(MPurchaseMaster master)
        {
            if (master == null) return;

            _editingMasterId = master.Id;
            PurchaseMaster.InvoiceNumber = master.InvoiceNumber;
            PurchaseMaster.VendorInvoiceNumber = master.VendorInvoiceNumber;
            PurchaseMaster.PurchaseDate = master.PurchaseDate;
            PurchaseMaster.Discount = master.Discount;
            Discount = master.Discount;
            VendorInvoiceNumber = master.VendorInvoiceNumber ?? string.Empty;

            // ── Set supplier from the invoice's SupplierId ────────────────────────
            var matchedSupplier = Suppliers.FirstOrDefault(s => s.Id == master.SupplierId);
            if (matchedSupplier != null)
            {
                // Set backing field directly to avoid triggering ResetInvoice in the setter
                _selectedSupplier = matchedSupplier;
                PurchaseMaster.SupplierId = matchedSupplier.Id;
                SupplierBalance = _supplierService.RecalculateAndUpdateSupplierBalance(matchedSupplier.Id);
                OnPropertyChanged(nameof(SelectedSupplier));
                OnPropertyChanged(nameof(ScanHintText));
                OnPropertyChanged(nameof(ScanHintVisibility));
            }

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

            // ── Determine next barcode by reading the LAST product's barcode and
            //    incrementing its numeric suffix — not by counting total products,
            //    which breaks if any product was ever deleted or barcodes don't
            //    start at 1. e.g. last barcode "M10" → next items get M11, M12, ...
            string lastBarcode = _productService.GetLastBarcode();
            string prefix = "M";
            long nextNumber = 1;

            if (!string.IsNullOrWhiteSpace(lastBarcode))
            {
                var match = System.Text.RegularExpressions.Regex.Match(lastBarcode, @"^(.*?)(\d+)$");
                if (match.Success)
                {
                    prefix = match.Groups[1].Value;
                    nextNumber = long.Parse(match.Groups[2].Value) + 1;
                }
            }

            int added = 0;

            foreach (var item in approved.Items)
            {
                double qty = item.Quantity > 0 ? item.Quantity : 1;
                decimal price = item.PurchasePrice;
                decimal netAmt = (decimal)qty * price;
                string barcode = $"{prefix}{nextNumber + added}";

                PurchaseItems.Add(new MPurchaseDetail
                {
                    ProductId = 0,
                    ProductName = item.Description,
                    Barcode = barcode,
                    Quantity = qty,
                    PurchasePrice = price,
                    WholesalePrice = item.WholesalePrice,
                    MRP = item.MRP,
                    Retail = item.RetailPrice,
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
            LoadSupplierHistory();

            IsHistoryOpen = !IsHistoryOpen;
            Console.WriteLine($"[ToggleHistory] IsHistoryOpen is now {IsHistoryOpen}. " +
                              $"SupplierHistory count={SupplierHistory?.Count ?? -1}");
        }

        private void LoadSupplierHistory()
        {
            try
            {
                var records = _purchaseService.GetFilteredPurchases();
                SupplierHistory = new ObservableCollection<MPurchaseMaster>(
                    records.OrderByDescending(r => r.PurchaseDate));
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
            string nextVendorInvoice = "";          // ← NEW
            try
            {
                long count = _purchaseService.GetPurchaseCount();
                nextInvoice = $"MS{count + 1}";   // ← CHANGED (was just count+1)
                nextVendorInvoice = $"M{count + 1}";    // ← NEW
            }
            catch { }

            PurchaseMaster = new MPurchaseMaster
            {
                PurchaseDate = DateTime.Now,
                InvoiceNumber = nextInvoice,
                VendorInvoiceNumber = nextVendorInvoice, // ← NEW
                Discount = 0
            };

            VendorInvoiceNumber = nextVendorInvoice;     // ← NEW (updates VM property for binding)

            // unchanged lines below ──────────────────────────────────────────────
            SupplierHistory = new ObservableCollection<MPurchaseMaster>();
            Discount = PurchaseMaster.Discount;
            NewItem = new MPurchaseDetail();
            PurchaseItems = new ObservableCollection<MPurchaseDetail>();
            Suppliers = new ObservableCollection<MSupplier>(_supplierService.GetAllSuppliers());
            Products = new ObservableCollection<MProducts>(_productService.GetProducts());

            try
            {
                var companyService = new CompanyService();
                var companies = companyService.GetCompanyInfo();
                _myGSTNumber = companies.FirstOrDefault()?.GSTNumber ?? string.Empty;
            }
            catch { _myGSTNumber = string.Empty; }

            try
            {
                TaxCategories = new ObservableCollection<MTaxCategory>(
                    _taxService.GetTaxCategory());

                SelectedTaxCategory =
                    TaxContext.SelectedTax ??
                    TaxCategories.FirstOrDefault();

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
            PurchaseMaster.VendorInvoiceNumber = VendorInvoiceNumber; // ← new: carry into model


            bool success;

            if (_editingMasterId > 0)
            {
                // UPDATE existing invoice — no supplier balance change
                success = _purchaseService.UpdatePurchase(_editingMasterId, PurchaseMaster);
            }
            else
            {
                // INSERT new invoice + adjust supplier balance
                

                success = _purchaseService.AddPurchase(PurchaseMaster);
            }

            if (success)
            {
                decimal newBalance = _supplierService.RecalculateAndUpdateSupplierBalance(SelectedSupplier.Id);
                SupplierBalance = newBalance;
                SelectedSupplier.CurrentBalance = newBalance;

                string msg = _editingMasterId > 0
                    ? "✔ Invoice updated successfully!"
                    : newProductsCreated > 0
                        ? $"✔ Purchase recorded!\n📦 {newProductsCreated} new product(s) added."
                        : "✔ Purchase recorded and stock updated!";

                MessageBox.Show(msg, "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                //ResetForm();
            }
            else
            {
                MessageBox.Show("Error occurred while saving.");
            }
        }
        // ════════════════════════════════════════════════════════════════════════
        // Called after ProductBulkEditWindow closes successfully.
        //   • savedProducts — every product left in the edit window after Save
        //     (existing + newly-inserted), used to refresh matching PurchaseItems
        //     rows with their latest name/wholesale/MRP/retail. NOTE: the invoice
        //     line's PurchasePrice is intentionally left as whatever the user
        //     entered on THIS invoice — a bulk edit changing the product master's
        //     PurchasePrice doesn't silently override what you're actually paying
        //     on this purchase.
        //   • newProducts — subset that were brand-new (added via "Add Copies").
        //     These become new invoice line items with Quantity = 1, since they
        //     weren't part of the invoice before the edit window created them.
        //   • deletedBarcodes — products removed via "Delete Selected" in the edit
        //     window. Matching lines are removed from THIS invoice too, since a
        //     deleted product can't be purchased.
        // ════════════════════════════════════════════════════════════════════════
        public void RefreshAfterProductEdit(
            List<MProducts> savedProducts, List<MProducts> newProducts, List<string> deletedBarcodes)
        {
            Products = new ObservableCollection<MProducts>(_productService.GetProducts());
            OnPropertyChanged(nameof(Products));

            if (deletedBarcodes.Any())
            {
                var toRemove = PurchaseItems.Where(i => deletedBarcodes.Contains(i.Barcode)).ToList();
                foreach (var item in toRemove)
                    PurchaseItems.Remove(item);
            }

            foreach (var item in PurchaseItems)
            {
                var match = savedProducts.FirstOrDefault(p => p.Barcode == item.Barcode);
                if (match == null) continue;

                item.ProductId = match.Id;
                item.ProductName = match.ProductName;
                item.WholesalePrice = match.WholesalePrice;
                item.MRP = match.MRP;
                item.Retail = match.RetailSalePrice;
            }

            foreach (var newProd in newProducts)
            {
                PurchaseItems.Add(new MPurchaseDetail
                {
                    ProductId = newProd.Id,
                    ProductName = newProd.ProductName,
                    Barcode = newProd.Barcode,
                    Product = newProd,
                    Quantity = 1,
                    PurchasePrice = newProd.PurchasePrice,
                    WholesalePrice = newProd.WholesalePrice,
                    MRP = newProd.MRP,
                    Retail = newProd.RetailSalePrice,
                    AfterTaxation = newProd.PurchasePrice
                });
            }

            RecalculateTotal();
            OnPropertyChanged(nameof(PurchaseItems));
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
