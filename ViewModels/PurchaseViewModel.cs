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

        // NEW — guards against a second click of SAVE INVOICE re-saving the
        // same invoice (and crashing on a duplicate-key DB exception).
        // Reset to false whenever a fresh/loaded invoice becomes save-able
        // again (InitializeData / LoadHistoryInvoice), set to true only after
        // a successful save.
        private bool _invoiceSaved = false;

        // ══════════════════════════════════════════════════════════════════
        // NEW — snapshot of what THIS invoice's total was worth to the
        // supplier's wallet at the moment it was loaded:
        //   • Brand-new invoice (InitializeData)      -> 0
        //   • Invoice reopened via History             -> master.TotalAmount
        //     (its previously-saved total, before any edits made this session)
        //
        // On SAVE, only the DIFFERENCE between this and the freshly
        // recalculated PurchaseMaster.TotalAmount is applied to the
        // supplier's wallet — not a blind full recalculation. If you raise
        // the invoice total, the wallet balance goes up by exactly that much
        // more; if you lower it, the wallet goes down by exactly that much
        // less. This only ever runs inside SavePurchase(), which is only
        // ever triggered by clicking SAVE.
        // ══════════════════════════════════════════════════════════════════
        private decimal _originalInvoiceTotal = 0m;

        // Product-master changes staged by Bulk Edit — nothing here hits the
        // database until SavePurchase() runs (SAVE INVOICE), matching the
        // rule that Bulk Edit itself never writes to the DB.
        private readonly System.Collections.Generic.List<MProducts> _pendingProductInserts = new();
        private readonly System.Collections.Generic.List<MProducts> _pendingProductUpdates = new();
        private readonly System.Collections.Generic.List<MProducts> _pendingProductDeletes = new();
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
            _productService = new ProductService();
            _billScanService = new BillScanService();
            _taxService = new TaxService();

            AddItemCommand = new RelayCommand(_ => AddItemToGrid());
            PurchaseDeleteCommand = new RelayCommand(p => RemoveItemFromGrid(p as MPurchaseDetail));
            PurchaseSaveCommand = new RelayCommand(_ => SavePurchase());
            PurchaseResetCommand = new RelayCommand(_ => ResetForm());
            BarcodeSearchCommand = new RelayCommand(p => HandleBarcodeSearch(p?.ToString()));
            OpenAddSupplierCommand = new RelayCommand(_ => OpenSupplierWindow());
            ScanBillCommand = new RelayCommand(async _ => await ExecuteScanBillAsync());
            OpenApiKeySetupCommand = new RelayCommand(_ => OpenApiKeySetup());
            ToggleHistoryCommand = new RelayCommand(_ => ToggleHistory());
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
            int newProductsCount = 0;

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

                var colMap = new System.Collections.Generic.Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (var cell in headerRow.CellsUsed())
                    colMap[cell.GetString().Trim()] = cell.Address.ColumnNumber;

                if (!colMap.TryGetValue("ProductName", out int nameCol))
                {
                    MessageBox.Show(
                        "The Excel sheet must have a 'ProductName' column.\n\n" +
                        "Optional columns: 'Quantity', 'PurchasePrice', 'RetailPrice', 'MRP', " +
                        "'Size', 'Color', 'HSNCode', 'CGST', 'SGST', 'IGST'.\n\n" +
                        "Barcodes are assigned automatically — no Barcode column needed.",
                        "Invalid Sheet", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int? qtyCol = colMap.TryGetValue("Quantity", out var qc) ? qc : (int?)null;
                int? priceCol = colMap.TryGetValue("PurchasePrice", out var pc) ? pc : (int?)null;
                int? retailCol = colMap.TryGetValue("RetailPrice", out var rc) ? rc : (int?)null;
                int? mrpCol = colMap.TryGetValue("MRP", out var mc) ? mc : (int?)null;
                int? sizeCol = colMap.TryGetValue("Size", out var szc) ? szc : (int?)null;
                int? colourCol = colMap.TryGetValue("Color", out var clc) ? clc : (int?)null;
                int? hsnCol = colMap.TryGetValue("HSNCode", out var hc) ? hc : (int?)null;
                int? cgstCol = colMap.TryGetValue("CGST", out var cgc) ? cgc : (int?)null;
                int? sgstCol = colMap.TryGetValue("SGST", out var sgc) ? sgc : (int?)null;
                int? igstCol = colMap.TryGetValue("IGST", out var igc) ? igc : (int?)null;

                // ── Barcode auto-generation setup ───────────────────────────────
                // Seed the "already used" set from every product barcode currently
                // known (DB) plus everything already sitting on this invoice, so
                // freshly generated barcodes for this import batch never collide
                // with either.
                var usedBarcodes = new System.Collections.Generic.HashSet<string>(
                    Products.Select(p => p.Barcode).Where(b => !string.IsNullOrWhiteSpace(b)),
                    StringComparer.OrdinalIgnoreCase);
                foreach (var pi in PurchaseItems)
                    if (!string.IsNullOrWhiteSpace(pi.Barcode)) usedBarcodes.Add(pi.Barcode);

                string barcodePrefix = "M";
                long nextBarcodeNumber = 1;
                string? lastAuto = _productService.GetLastAutoBarcode();
                if (!string.IsNullOrWhiteSpace(lastAuto))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(lastAuto, @"^([A-Za-z]+)(\d+)$");
                    if (match.Success)
                    {
                        barcodePrefix = match.Groups[1].Value;
                        nextBarcodeNumber = long.Parse(match.Groups[2].Value) + 1;
                    }
                }

                string GenerateBarcode()
                {
                    string candidate;
                    do
                    {
                        candidate = $"{barcodePrefix}{nextBarcodeNumber}";
                        nextBarcodeNumber++;
                    } while (usedBarcodes.Contains(candidate));
                    usedBarcodes.Add(candidate);
                    return candidate;
                }

                int lastRow = ws.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();

                for (int r = headerRow.RowNumber() + 1; r <= lastRow; r++)
                {
                    var row = ws.Row(r);
                    string nameFromSheet = row.Cell(nameCol).GetString().Trim();

                    // First blank ProductName cell = end of the data block.
                    if (string.IsNullOrWhiteSpace(nameFromSheet))
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

                    decimal? ReadDecimal(int? col)
                    {
                        if (!col.HasValue) return null;
                        var cell = row.Cell(col.Value);
                        return !cell.IsEmpty() && decimal.TryParse(cell.GetString(), out var v) ? v : (decimal?)null;
                    }

                    string ReadString(int? col)
                    {
                        if (!col.HasValue) return null;
                        var cell = row.Cell(col.Value);
                        var s = cell.GetString().Trim();
                        return string.IsNullOrWhiteSpace(s) ? null : s;
                    }

                    decimal? priceOverride = ReadDecimal(priceCol);
                    decimal? retailOverride = ReadDecimal(retailCol);
                    decimal? mrpOverride = ReadDecimal(mrpCol);
                    decimal? cgstOverride = ReadDecimal(cgstCol);
                    decimal? sgstOverride = ReadDecimal(sgstCol);
                    decimal? igstOverride = ReadDecimal(igstCol);
                    string sizeFromSheet = ReadString(sizeCol);
                    string colourFromSheet = ReadString(colourCol);
                    string hsnFromSheet = ReadString(hsnCol);

                    // FIX: no more matching/merging by ProductName — against the
                    // product master OR against other rows already on this
                    // invoice (including earlier rows from this same Excel
                    // sheet). Every row in the sheet becomes its own separate
                    // invoice line, taken exactly as the sheet has it, with a
                    // freshly auto-generated barcode. Nothing hits the database
                    // until SAVE INVOICE is clicked (SavePurchase() already
                    // auto-creates a product for ProductId == 0 rows).
                    decimal price = priceOverride ?? 0;
                    decimal retail = retailOverride ?? 0;
                    decimal mrp = mrpOverride ?? 0;
                    string generatedBarcode = GenerateBarcode();

                    PurchaseItems.Add(new MPurchaseDetail
                    {
                        ProductId = 0,
                        ProductName = nameFromSheet,
                        Barcode = generatedBarcode,
                        Quantity = qty,
                        PurchasePrice = price,
                        WholesalePrice = 0,
                        MRP = mrp,
                        Retail = retail,
                        Size = sizeFromSheet,
                        Colour = colourFromSheet,
                        HSNCode = hsnFromSheet,
                        CGST = cgstOverride ?? 0,
                        SGST = sgstOverride ?? 0,
                        IGST = igstOverride ?? 0,
                        AfterTaxation = (decimal)qty * price
                    });
                    added++;
                    newProductsCount++;
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
                $"{added} item(s) added, taken exactly as they appear in the sheet " +
                "(no matching or merging by product name) — barcodes were auto-assigned " +
                "and are only recorded in your product list once you click SAVE INVOICE.";

            MessageBox.Show(summary, "Import Result", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private void LoadHistoryInvoice(MPurchaseMaster master)
        {
            if (master == null) return;

            // NEW — an invoice pulled up from History is fresh to save/update
            // again (until it's actually saved once via this session).
            _invoiceSaved = false;

            // NEW — snapshot this invoice's PREVIOUSLY SAVED total, before any
            // edits in this session. On Save, only the difference between
            // this and the newly recalculated total moves the supplier's
            // wallet — not the invoice's full amount again (that would double
            // count what was already applied when this invoice was first
            // saved).
            _originalInvoiceTotal = master.TotalAmount;

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
                    // FIX: HSNCode/Size/Colour/CGST/SGST/IGST were never
                    // copied here, so reopening a SAVED invoice (via History)
                    // stripped these fields from the in-memory grid even
                    // though they were correctly written to the database at
                    // SAVE INVOICE time. Bulk Edit then reads this stripped
                    // grid and shows everything blank, making it look like
                    // the data was lost on save when it was actually just
                    // lost on reload.
                    HSNCode = d.HSNCode,
                    Size = d.Size,
                    Colour = d.Colour,
                    CGST = d.CGST,
                    SGST = d.SGST,
                    IGST = d.IGST,
                    AfterTaxation = d.AfterTaxation,

                    // FIX: Batch / MfgDate / ExpDate were saved on the
                    // MPurchaseDetail row (GetFilteredPurchases reads them) but
                    // never copied onto the in-memory grid line here, so a
                    // reopened invoice always started with blanks — and those
                    // blanks are what UpdatePurchase then wrote back into the
                    // ProductQuantity.PurchaseQuantity JSON.
                    Batch = d.Batch,
                    MfgDate = d.MfgDate,
                    ExpDate = d.ExpDate,
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
                Title = "Select Purchase Bill (PDF or Image)",
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
                bool looksLikeAuthError =
                    ex.Message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("expired_api_key", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("invalid_api_key", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("invalid_request_error", StringComparison.OrdinalIgnoreCase);

                if (looksLikeAuthError)
                {
                    MessageBox.Show(ex.Message, "Bill Scan Failed",
        MessageBoxButton.OK, MessageBoxImage.Warning);

                    var keyWin = new ApiKeySetupWindow { Owner = Application.Current.MainWindow };
                    keyWin.ShowDialog();
                    return; // let the user click Scan Bill again after fixing the key
                }

                MessageBox.Show(
                    $"AI scan failed:\n\n{ex.Message}\n\n" +
                    "If this keeps happening, click the ⚙ icon next to AI BILL SCAN " +
                    "to check or update your Groq API key.",
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

            var usedBarcodes = new System.Collections.Generic.HashSet<string>(
                Products.Select(p => p.Barcode).Where(b => !string.IsNullOrWhiteSpace(b)),
                StringComparer.OrdinalIgnoreCase);
            foreach (var pi in PurchaseItems)
                if (!string.IsNullOrWhiteSpace(pi.Barcode)) usedBarcodes.Add(pi.Barcode);

            string lastBarcode = _productService.GetLastBarcode();
            string prefix = "GR";
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

            string GenerateBarcode()
            {
                string candidate;
                do
                {
                    candidate = $"{prefix}{nextNumber}";
                    nextNumber++;
                } while (usedBarcodes.Contains(candidate));
                usedBarcodes.Add(candidate);
                return candidate;
            }

            int added = 0;

            foreach (var item in approved.Items)
            {
                double qty = item.Quantity > 0 ? item.Quantity : 1;
                decimal price = item.PurchasePrice;
                decimal netAmt = (decimal)qty * price;
                string barcode = GenerateBarcode();

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
                    HSNCode = item.HsnCode,   // ← now carried through (was dropped before)
                    Size = item.Size,         // ← NEW
                    Colour = item.Colour,     // ← NEW
                    CGST = item.CGST,         // ← NEW
                    SGST = item.SGST,         // ← NEW
                    IGST = item.IGST,         // ← NEW
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
                return;
            }

            // Unknown barcode: add it straight to the invoice as a new line with
            // ProductId == 0 — same pattern already used by Scan Bill, Excel
            // Import and Quick Add. Nothing is written to MProducts here;
            // SavePurchase() auto-creates the product master row for any
            // ProductId == 0 line, but only when SAVE INVOICE is actually clicked.
            var existingLine = PurchaseItems.FirstOrDefault(i =>
                i.ProductId == 0 &&
                string.Equals(i.Barcode, barcode, StringComparison.OrdinalIgnoreCase));

            if (existingLine != null)
            {
                // Same unknown barcode scanned again this session — just bump qty.
                int index = PurchaseItems.IndexOf(existingLine);
                existingLine.Quantity++;
                existingLine.AfterTaxation = (decimal)existingLine.Quantity * existingLine.PurchasePrice;
                PurchaseItems.RemoveAt(index);
                PurchaseItems.Insert(index, existingLine);
                RecalculateTotal();
                return;
            }

            PurchaseItems.Add(new MPurchaseDetail
            {
                ProductId = 0,
                ProductName = barcode,   // placeholder — edit directly in the grid
                Barcode = barcode,
                Quantity = 1,
                PurchasePrice = 0,
                WholesalePrice = 0,
                MRP = 0,
                Retail = 0,
                AfterTaxation = 0
            });

            RecalculateTotal();
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
            // NEW — a brand-new / blank invoice is always save-able.
            _invoiceSaved = false;

            // NEW — a brand-new invoice hasn't contributed anything to any
            // supplier's wallet yet, so the entire computed total (once
            // Save succeeds) counts as the "difference" to apply.
            _originalInvoiceTotal = 0m;

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
            // NEW — block a second click of SAVE INVOICE. Without this, clicking
            // Save twice re-runs the insert/update below with the same invoice
            // data, which can throw an unhandled DB exception (e.g. duplicate
            // invoice number) and crash the app instead of showing a message.
            if (_invoiceSaved)
            {
                MessageBox.Show(
                    "This invoice has already been saved.\n\n" +
                    "Start a new invoice (Reset) if you want to record another purchase.",
                    "Invoice Already Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (SelectedSupplier == null)
            { MessageBox.Show("Please select a supplier."); return; }
            if (!PurchaseItems.Any())
            { MessageBox.Show("Please add at least one item."); return; }

            // ── Commit everything Bulk Edit staged — deletes, then updates,
            //    then inserts. This is the ONLY place any of these actually
            //    hit the database; Bulk Edit itself only ever edited
            //    in-memory MProducts objects. ──
            foreach (var toDelete in _pendingProductDeletes)
            {
                if (!_productService.DeleteProduct(toDelete.Id))
                {
                    MessageBox.Show(
                        $"Failed to delete '{toDelete.ProductName}' (barcode {toDelete.Barcode}) " +
                        "from the product master. Stopping here — nothing else was saved.\n\n" +
                        $"Details: {_productService.LastError}",
                        "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            foreach (var toUpdate in _pendingProductUpdates)
            {
                if (!_productService.UpdateProduct(toUpdate))
                {
                    MessageBox.Show(
                        $"Failed to update product '{toUpdate.ProductName}' (barcode {toUpdate.Barcode}).\n\n" +
                        $"Details: {_productService.LastError}",
                        "Update Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            foreach (var toInsert in _pendingProductInserts)
            {
                var existingByBarcode = _productService.GetByBarcode(toInsert.Barcode);
                if (existingByBarcode != null)
                {
                    toInsert.Id = existingByBarcode.Id; // already there — nothing to do
                    continue;
                }

                if (!_productService.InsertProduct(toInsert))
                {
                    MessageBox.Show(
                        $"Failed to save new product '{toInsert.ProductName}' (barcode {toInsert.Barcode}).\n\n" +
                        $"Details: {_productService.LastError}",
                        "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var insertedProduct = _productService.GetByBarcode(toInsert.Barcode);
                if (insertedProduct != null) toInsert.Id = insertedProduct.Id;
            }

            // Any invoice line still pointing at ProductId == 0 whose product
            // was just inserted above (matched by Barcode) picks up its real
            // Id here, before the generic "auto-create for ProductId==0" loop
            // below runs — that loop is now only a fallback for items Bulk
            // Edit never touched (typed manually, Scan Bill, or Excel import).
            foreach (var item in PurchaseItems)
            {
                if (item.ProductId > 0) continue;
                var match = _pendingProductInserts.FirstOrDefault(p =>
                    string.Equals(p.Barcode, item.Barcode, StringComparison.OrdinalIgnoreCase));
                if (match != null && match.Id > 0)
                    item.ProductId = match.Id;
            }

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
                    HSNCode = item.HSNCode,
                    Size = item.Size,
                    Colour = item.Colour,
                    DiscountPercentage = 0,
                    CGST = (double)item.CGST,
                    SGST = (double)item.SGST,
                    IGST = (double)item.IGST,
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

            // NEW — make sure PurchaseMaster.TotalAmount reflects every edit
            // made in this session (including any new products just created
            // above) before we snapshot it for the wallet-difference
            // calculation and persist it to the DB.
            RecalculateTotal();

            PurchaseMaster.PaymentMode = PaymentMethod;
            PurchaseMaster.AmountPaid = AmountPaid;
            PurchaseMaster.SupplierId = SelectedSupplier.Id;
            PurchaseMaster.MPurchaseDetail = PurchaseItems.ToList();
            PurchaseMaster.VendorInvoiceNumber = VendorInvoiceNumber; // ← new: carry into model


            bool success;

            // NEW — wrap the actual DB save in try/catch so any unexpected
            // error (duplicate key, connection drop, etc.) shows a message
            // instead of taking the whole application down.
            try
            {
                if (_editingMasterId > 0)
                {
                    // UPDATE existing invoice — supplier wallet is adjusted
                    // below by the DIFFERENCE in total, not recalculated
                    // from scratch.
                    success = _purchaseService.UpdatePurchase(_editingMasterId, PurchaseMaster);
                }
                else
                {
                    // INSERT new invoice — supplier wallet is adjusted below
                    // too; since _originalInvoiceTotal is 0 for a new
                    // invoice, the "difference" applied is simply the full
                    // invoice total, same net effect as before.
                    success = _purchaseService.AddPurchase(PurchaseMaster);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to save invoice:\n\n{ex.Message}",
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (success)
            {
                _pendingProductInserts.Clear();
                _pendingProductUpdates.Clear();
                _pendingProductDeletes.Clear();

                // ══════════════════════════════════════════════════════════
                // NEW — supplier wallet adjustment, ONLY on a successful
                // SAVE, and ONLY by the amount this invoice's total actually
                // changed by:
                //   • If the new total is HIGHER than what this invoice was
                //     last saved as (or 0, for a brand-new invoice), the
                //     positive difference is ADDED to the wallet.
                //   • If the new total is LOWER, the (negative) difference
                //     is subtracted — i.e. the wallet goes down.
                //   • No change in total → no wallet call at all.
                // ══════════════════════════════════════════════════════════
                decimal totalDifference = PurchaseMaster.TotalAmount - _originalInvoiceTotal;

                decimal newBalance = SupplierBalance;
                if (totalDifference != 0)
                {
                    newBalance = _supplierService.AdjustSupplierBalance(
                        SelectedSupplier.Id, totalDifference);
                }

                SupplierBalance = newBalance;
                SelectedSupplier.CurrentBalance = newBalance;

                // This invoice's total is now the new baseline — if the user
                // saves again in the same session (edits it further, then
                // clicks SAVE again — normally blocked by _invoiceSaved, but
                // kept correct in case that flow ever changes), future diffs
                // are computed from this point.
                _originalInvoiceTotal = PurchaseMaster.TotalAmount;

                // NEW — lock this invoice against further saves until Reset /
                // a new invoice / a history invoice is loaded.
                _invoiceSaved = true;

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
        //   • newProducts — subset that were brand-new (added via "Add Copies"),
        //     paired with the Quantity typed into the Bulk Edit grid for that row.
        //     These become new invoice line items using THAT quantity — not a
        //     hardcoded 1 — since the whole point of editing Quantity in Bulk
        //     Edit is for it to carry through to the invoice.
        //     FIX: this parameter used to be List<MProducts> with no quantity
        //     attached, so every new row landed on the invoice as Quantity = 1
        //     regardless of what was typed in the grid.
        //   • deletedBarcodes — products removed via "Delete Selected" in the edit
        //     window. Matching lines are removed from THIS invoice too, since a
        //     deleted product can't be purchased.
        // ════════════════════════════════════════════════════════════════════════
        public void RefreshAfterProductEdit(
    List<MProducts> savedProducts,
    List<(MProducts Product, double Quantity)> newProducts,
    List<string> deletedBarcodes,
    List<(MPurchaseDetail Source, MProducts Product)> updatedInvoiceLines,
    List<MProducts> productsToInsert, List<MProducts> productsToUpdate, List<MProducts> productsToDelete,
    List<ProductBulkEditWindow.BulkEditResultLine> resultLines)
        {
            // Stage the product-master changes Bulk Edit produced. They are applied
            // to the database inside SavePurchase(), not here, so closing Bulk Edit
            // stays a purely in-memory step until SAVE INVOICE.
            //
            // Every IsNew row in the grid is re-reported each session, so the staged
            // inserts are replaced wholesale. This also drops new rows that were
            // deleted in a later session, so they aren't inserted at SAVE.
            _pendingProductInserts.Clear();
            _pendingProductInserts.AddRange(productsToInsert);

            // Updates and deletes accumulate across sessions, but replace by Id so
            // a second session never leaves an older copy of the same product staged.
            foreach (var p in productsToUpdate)
            {
                _pendingProductUpdates.RemoveAll(x => x.Id == p.Id);
                _pendingProductUpdates.Add(p);
            }
            foreach (var p in productsToDelete)
            {
                if (!_pendingProductDeletes.Any(x => x.Id == p.Id))
                    _pendingProductDeletes.Add(p);
            }

            // Reflect the staged changes in the local Products list (used by
            // dropdowns / barcode search) without touching the database.
            if (productsToDelete.Any())
            {
                var deleteIds = new System.Collections.Generic.HashSet<long>(productsToDelete.Select(p => p.Id));
                foreach (var p in Products.Where(p => deleteIds.Contains(p.Id)).ToList())
                    Products.Remove(p);
            }
            foreach (var updated in productsToUpdate)
            {
                var match = Products.FirstOrDefault(p => p.Id == updated.Id);
                if (match != null) Products[Products.IndexOf(match)] = updated;
            }
            OnPropertyChanged(nameof(Products));

            // Rebuild PurchaseItems from resultLines, an ordered snapshot of the Bulk
            // Edit grid taken at Save time, so variance rows land directly under the
            // row they were created from instead of at the bottom of the invoice.
            // Deleted rows are already absent from resultLines.
            PurchaseItems.Clear();

            foreach (var line in resultLines)
            {
                if (line.SourceInvoiceItem != null)
                {
                    // Existing invoice line: update via the direct row reference the
                    // edit window handed back, not by matching Barcode.
                    var source = line.SourceInvoiceItem;
                    var product = line.Product;

                    // NEW: carry the full edited product (category, subcategory,
                    // unit, rack, etc.) on the invoice line, so reopening Bulk Edit
                    // rebuilds from it instead of resetting to database/default values.
                    source.Product = product;

                    source.ProductId = product.Id;      // still 0 for a not-yet-inserted product
                    source.ProductName = product.ProductName;
                    source.Barcode = product.Barcode;   // keep in sync if it was renumbered
                    source.PurchasePrice = product.PurchasePrice;
                    source.WholesalePrice = product.WholesalePrice;
                    source.MRP = product.MRP;
                    source.Retail = product.RetailSalePrice;

                    source.HSNCode = product.HSNCode;
                    source.Size = product.Size;
                    source.Colour = product.Colour;
                    source.CGST = (decimal)product.CGST;
                    source.SGST = (decimal)product.SGST;
                    source.IGST = (decimal)product.IGST;

                    // FIX: batch + dates edited in Bulk Edit were never copied
                    // back onto the invoice line, so the line kept its old
                    // (or blank) values and those are what PurchaseService
                    // wrote into the PurchaseQuantity JSON.
                    source.Batch = product.Batch;
                    source.MfgDate = product.MfgDate;
                    source.ExpDate = product.ExpDate;

                    PurchaseItems.Add(source);
                }
                else
                {
                    // Brand-new line (created via Add Variance). Uses the quantity
                    // typed into the Bulk Edit grid, and AfterTaxation = qty * price.
                    double qty = line.Quantity > 0 ? line.Quantity : 1;
                    var newProd = line.Product;

                    PurchaseItems.Add(new MPurchaseDetail
                    {
                        ProductId = newProd.Id,   // 0, created for real in SavePurchase()
                        ProductName = newProd.ProductName,
                        Barcode = newProd.Barcode,
                        Product = newProd,        // already carried on new lines
                        Quantity = qty,
                        PurchasePrice = newProd.PurchasePrice,
                        WholesalePrice = newProd.WholesalePrice,
                        MRP = newProd.MRP,
                        Retail = newProd.RetailSalePrice,
                        HSNCode = newProd.HSNCode,
                        Size = newProd.Size,
                        Colour = newProd.Colour,
                        CGST = (decimal)newProd.CGST,
                        SGST = (decimal)newProd.SGST,
                        IGST = (decimal)newProd.IGST,
                        Batch = newProd.Batch,
                        MfgDate = newProd.MfgDate,
                        ExpDate = newProd.ExpDate,
                        AfterTaxation = (decimal)qty * newProd.PurchasePrice
                    });
                }
            }

            RecalculateTotal();
            OnPropertyChanged(nameof(PurchaseItems));
        }
        private void ResetForm()
        {
            _editingMasterId = 0;   // ← reset edit mode
            _invoiceSaved = false;  // NEW — a reset form is fresh and save-able again
            _pendingProductInserts.Clear();
            _pendingProductUpdates.Clear();
            _pendingProductDeletes.Clear();
            InitializeData();       // also resets _originalInvoiceTotal to 0
            SelectedSupplier = null;
            SupplierHistory = new ObservableCollection<MPurchaseMaster>();
            IsHistoryOpen = false;
            OnPropertyChanged(nameof(ScanHintText));
            OnPropertyChanged(nameof(ScanHintVisibility));
        }
    }
}