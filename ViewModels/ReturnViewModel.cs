
using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using WPFCRUDApp.Models;

namespace MyWPFCRUDApp.ViewModels
{
    // ════════════════════════════════════════════════════════════════════════
    // ReturnLineItem
    // ════════════════════════════════════════════════════════════════════════
    public class ReturnLineItem : BaseViewModel
    {
        private readonly Action? _onChanged;

        public ReturnLineItem(Action? onChanged = null)
        {
            _onChanged = onChanged;
        }

        public long ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? Barcode { get; set; }

        /// <summary>
        /// Quantity originally purchased on the linked purchase invoice.
        /// Used to prevent returning more than was purchased.
        /// </summary>
        public double? PurchasedQuantity { get; set; }

        private double _quantity;

        public double Quantity
        {
            get => _quantity;

            set
            {
                if (SetProperty(ref _quantity, value))
                {
                    OnPropertyChanged(nameof(Amount));
                    _onChanged?.Invoke();
                }
            }
        }

        private decimal _purchasePrice;

        public decimal PurchasePrice
        {
            get => _purchasePrice;

            set
            {
                if (SetProperty(ref _purchasePrice, value))
                {
                    OnPropertyChanged(nameof(Amount));
                    _onChanged?.Invoke();
                }
            }
        }

        public decimal Amount =>
            Math.Round((decimal)Quantity * PurchasePrice, 2);

        private string? _batch;

        public string? Batch
        {
            get => _batch;
            set => SetProperty(ref _batch, value);
        }

        private DateTime? _mfgDate;

        public DateTime? MfgDate
        {
            get => _mfgDate;
            set => SetProperty(ref _mfgDate, value);
        }

        private DateTime? _expDate;

        public DateTime? ExpDate
        {
            get => _expDate;
            set => SetProperty(ref _expDate, value);
        }

        private string? _reason;

        public string? Reason
        {
            get => _reason;
            set => SetProperty(ref _reason, value);
        }

        public MPurchaseReturnDetail ToDetail()
        {
            return new MPurchaseReturnDetail
            {
                ProductId = ProductId,
                ProductName = ProductName,
                Barcode = Barcode,
                Quantity = Quantity,
                PurchasePrice = PurchasePrice,
                Batch = Batch,
                MfgDate = MfgDate,
                ExpDate = ExpDate,
                Reason = Reason
            };
        }

        public static ReturnLineItem From(
            MPurchaseReturnDetail d,
            Action? onChanged)
        {
            return new ReturnLineItem(onChanged)
            {
                ProductId = d.ProductId,
                ProductName = d.ProductName,
                Barcode = d.Barcode,
                Quantity = d.Quantity,
                PurchasePrice = d.PurchasePrice,
                Batch = d.Batch,
                MfgDate = d.MfgDate,
                ExpDate = d.ExpDate,
                Reason = d.Reason
            };
        }
    }


    // ════════════════════════════════════════════════════════════════════════
    // ReturnViewModel
    // ════════════════════════════════════════════════════════════════════════
    public class ReturnViewModel : BaseViewModel
    {
        // ── Services ────────────────────────────────────────────────────────
        private readonly PurchaseReturnService _returnService;
        private readonly SupplierService _supplierService;
        private readonly PurchaseService _purchaseService;


        // ── Edit state ──────────────────────────────────────────────────────
        private long _editingMasterId = 0;
        private bool _returnSaved = false;

        private decimal _originalReturnTotal = 0m;

        private string _linkedInvoiceNumber = string.Empty;


        // ── Commands ────────────────────────────────────────────────────────
        public ICommand RemoveItemCommand { get; }

        public ICommand LoadFromInvoiceCommand { get; }

        public ICommand ClearInvoiceLinkCommand { get; }

        public ICommand SaveReturnCommand { get; }

        public ICommand ResetCommand { get; }

        public ICommand ToggleHistoryCommand { get; }

        public ICommand LoadHistoryReturnCommand { get; }

        public ICommand DeleteReturnCommand { get; }


        // ── Suppliers ───────────────────────────────────────────────────────
        private ObservableCollection<MSupplier> _suppliers = new();

        public ObservableCollection<MSupplier> Suppliers
        {
            get => _suppliers;
            set => SetProperty(ref _suppliers, value);
        }


        // ── Return items ────────────────────────────────────────────────────
        private ObservableCollection<ReturnLineItem> _returnItems = new();

        public ObservableCollection<ReturnLineItem> ReturnItems
        {
            get => _returnItems;
            set => SetProperty(ref _returnItems, value);
        }


        // ── Purchase invoices ───────────────────────────────────────────────
        private ObservableCollection<MPurchaseMaster> _supplierInvoices = new();

        public ObservableCollection<MPurchaseMaster> SupplierInvoices
        {
            get => _supplierInvoices;
            set => SetProperty(ref _supplierInvoices, value);
        }


        // ── Return history ──────────────────────────────────────────────────
        private ObservableCollection<MPurchaseReturnMaster> _supplierHistory = new();

        public ObservableCollection<MPurchaseReturnMaster> SupplierHistory
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


        // ── Header ──────────────────────────────────────────────────────────
        private string _returnInvoiceNumber = string.Empty;

        public string ReturnInvoiceNumber
        {
            get => _returnInvoiceNumber;

            set
            {
                if (SetProperty(ref _returnInvoiceNumber, value))
                    OnPropertyChanged(nameof(ModeText));
            }
        }


        private DateTime _returnDate = DateTime.Now;

        public DateTime ReturnDate
        {
            get => _returnDate;
            set => SetProperty(ref _returnDate, value);
        }


        private string _remarks = string.Empty;

        public string Remarks
        {
            get => _remarks;
            set => SetProperty(ref _remarks, value);
        }


        public bool IsEditMode =>
            _editingMasterId > 0;


        public string ModeText =>
            IsEditMode
                ? $"Editing {ReturnInvoiceNumber}"
                : "New return";


        // ── Supplier ────────────────────────────────────────────────────────
        private MSupplier? _selectedSupplier;

        public MSupplier? SelectedSupplier
        {
            get => _selectedSupplier;

            set
            {
                if (SetProperty(ref _selectedSupplier, value))
                {
                    // Selecting a different supplier invalidates
                    // the currently selected purchase invoice.
                    ClearInvoiceLink();

                    if (value != null)
                    {
                        decimal fresh =
                            _supplierService
                                .RecalculateAndUpdateSupplierBalance(value.Id);

                        SupplierBalance = fresh;

                        value.CurrentBalance = fresh;

                        LoadSupplierData();
                    }
                    else
                    {
                        SupplierBalance = 0;

                        SupplierHistory =
                            new ObservableCollection<MPurchaseReturnMaster>();

                        SupplierInvoices =
                            new ObservableCollection<MPurchaseMaster>();
                    }

                    // Supplier selection should NOT control whether
                    // history can be opened.
                    OnPropertyChanged(nameof(SupplierHintVisibility));
                }
            }
        }


        public Visibility SupplierHintVisibility =>
            SelectedSupplier == null
                ? Visibility.Visible
                : Visibility.Collapsed;


        // ── Supplier balance ────────────────────────────────────────────────
        private decimal _supplierBalance;

        public decimal SupplierBalance
        {
            get => _supplierBalance;

            set
            {
                if (SetProperty(ref _supplierBalance, value))
                    OnPropertyChanged(nameof(ProjectedBalance));
            }
        }


        /// <summary>
        /// Projected supplier balance after the current return.
        /// </summary>
        public decimal ProjectedBalance =>
            SupplierBalance -
            (TotalAmount - _originalReturnTotal);


        // ── Original purchase invoice ───────────────────────────────────────
        private MPurchaseMaster? _selectedSupplierInvoice;

        public MPurchaseMaster? SelectedSupplierInvoice
        {
            get => _selectedSupplierInvoice;

            set
            {
                if (SetProperty(ref _selectedSupplierInvoice, value))
                {
                    if (value != null)
                    {
                        _linkedInvoiceNumber =
                            value.InvoiceNumber ?? string.Empty;
                    }
                    else
                    {
                        _linkedInvoiceNumber = string.Empty;
                    }

                    OnPropertyChanged(nameof(LinkedInvoiceDisplay));
                    OnPropertyChanged(nameof(HasLinkedInvoice));
                }
            }
        }


        public bool HasLinkedInvoice =>
            !string.IsNullOrWhiteSpace(_linkedInvoiceNumber);


        public string LinkedInvoiceDisplay =>
            HasLinkedInvoice
                ? $"Linked to purchase invoice {_linkedInvoiceNumber}"
                : "No original purchase invoice selected";


        private void ClearInvoiceLink()
        {
            _selectedSupplierInvoice = null;

            _linkedInvoiceNumber = string.Empty;

            OnPropertyChanged(nameof(SelectedSupplierInvoice));
            OnPropertyChanged(nameof(LinkedInvoiceDisplay));
            OnPropertyChanged(nameof(HasLinkedInvoice));
        }


        // ── Totals ──────────────────────────────────────────────────────────
        private decimal _totalAmount;

        public decimal TotalAmount
        {
            get => _totalAmount;

            private set
            {
                if (SetProperty(ref _totalAmount, value))
                    OnPropertyChanged(nameof(ProjectedBalance));
            }
        }


        private double _totalQuantity;

        public double TotalQuantity
        {
            get => _totalQuantity;
            private set => SetProperty(ref _totalQuantity, value);
        }


        private int _itemCount;

        public int ItemCount
        {
            get => _itemCount;
            private set => SetProperty(ref _itemCount, value);
        }


        // ════════════════════════════════════════════════════════════════════════
        // Constructor
        // ════════════════════════════════════════════════════════════════════════
        public ReturnViewModel()
        {
            _returnService = new PurchaseReturnService();

            _supplierService = new SupplierService();

            _purchaseService = new PurchaseService();


            RemoveItemCommand =
                new RelayCommand(
                    p => RemoveItem(p as ReturnLineItem));


            LoadFromInvoiceCommand =
                new RelayCommand(
                    _ => LoadItemsFromInvoice());


            ClearInvoiceLinkCommand =
                new RelayCommand(
                    _ => ClearInvoiceLink());


            SaveReturnCommand =
                new RelayCommand(
                    _ => SaveReturn());


            ResetCommand =
                new RelayCommand(
                    _ => ResetForm());


            ToggleHistoryCommand =
                new RelayCommand(
                    _ => ToggleHistory());


            LoadHistoryReturnCommand =
                new RelayCommand(
                    p => LoadHistoryReturn(
                        p as MPurchaseReturnMaster));


            DeleteReturnCommand =
                new RelayCommand(
                    p => DeleteReturn(
                        p as MPurchaseReturnMaster));


            InitializeData();
        }


        // ════════════════════════════════════════════════════════════════════════
        // Initialize
        // ════════════════════════════════════════════════════════════════════════
        private void InitializeData()
        {
            _editingMasterId = 0;

            _returnSaved = false;

            _originalReturnTotal = 0m;


            string nextNumber = string.Empty;

            try
            {
                long count =
                    _returnService.GetReturnCount();

                nextNumber =
                    $"MR{count + 1}";
            }
            catch
            {
                // Keep empty if the number could not be generated.
            }


            ReturnInvoiceNumber = nextNumber;

            ReturnDate = DateTime.Now;

            Remarks = string.Empty;


            ReturnItems =
                new ObservableCollection<ReturnLineItem>();


            SupplierHistory =
                new ObservableCollection<MPurchaseReturnMaster>();


            SupplierInvoices =
                new ObservableCollection<MPurchaseMaster>();


            ClearInvoiceLink();


            try
            {
                Suppliers =
                    new ObservableCollection<MSupplier>(
                        _supplierService.GetAllSuppliers());
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not load suppliers:\n\n{ex.Message}",
                    "Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }


            RecalculateTotal();


            OnPropertyChanged(nameof(IsEditMode));

            OnPropertyChanged(nameof(ModeText));

            OnPropertyChanged(nameof(ProjectedBalance));
        }


        private void ResetForm()
        {
            InitializeData();

            _selectedSupplier = null;

            OnPropertyChanged(nameof(SelectedSupplier));

            SupplierBalance = 0;

            IsHistoryOpen = false;

            OnPropertyChanged(nameof(SupplierHintVisibility));
        }


        // ════════════════════════════════════════════════════════════════════════
        // Supplier data
        // ════════════════════════════════════════════════════════════════════════
        private void LoadSupplierData()
        {
            if (SelectedSupplier == null)
                return;


            // Purchase return history for this supplier.
            try
            {
                SupplierHistory =
                    new ObservableCollection<MPurchaseReturnMaster>(
                        _returnService.GetFilteredReturns(
                            supplierId: SelectedSupplier.Id));
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[Return.LoadSupplierData/history] {ex.Message}");

                SupplierHistory =
                    new ObservableCollection<MPurchaseReturnMaster>();
            }


            // Purchase invoices for this supplier.
            try
            {
                var supplierId =
                    SelectedSupplier.Id;

                SupplierInvoices =
                    new ObservableCollection<MPurchaseMaster>(
                        _purchaseService
                            .GetFilteredPurchases()
                            .Where(p => p.SupplierId == supplierId)
                            .OrderByDescending(
                                p => p.PurchaseDate));
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[Return.LoadSupplierData/invoices] {ex.Message}");

                SupplierInvoices =
                    new ObservableCollection<MPurchaseMaster>();
            }
        }


        // ════════════════════════════════════════════════════════════════════════
        // Totals
        // ════════════════════════════════════════════════════════════════════════
        public void RecalculateTotal()
        {
            TotalAmount =
                Math.Round(
                    ReturnItems?.Sum(i => i.Amount) ?? 0m,
                    2);

            TotalQuantity =
                ReturnItems?.Sum(i => i.Quantity) ?? 0;

            ItemCount =
                ReturnItems?.Count ?? 0;

            OnPropertyChanged(nameof(ProjectedBalance));
        }


        // ════════════════════════════════════════════════════════════════════════
        // Remove item
        // ════════════════════════════════════════════════════════════════════════
        private void RemoveItem(ReturnLineItem? item)
        {
            if (item == null)
                return;

            if (!ReturnItems.Contains(item))
                return;

            ReturnItems.Remove(item);

            RecalculateTotal();
        }


        // ════════════════════════════════════════════════════════════════════════
        // Load items from original purchase invoice
        // ════════════════════════════════════════════════════════════════════════
        private void LoadItemsFromInvoice()
        {
            if (SelectedSupplier == null)
            {
                MessageBox.Show(
                    "Please select a supplier first.",
                    "Supplier Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }


            var invoice =
                SelectedSupplierInvoice;

            if (invoice == null)
            {
                MessageBox.Show(
                    "Please select an original purchase invoice first.",
                    "Load Items",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }


            if (ReturnItems.Any())
            {
                var confirm =
                    MessageBox.Show(
                        "This will replace the items currently in the return " +
                        $"with the items from invoice {invoice.InvoiceNumber}.\n\n" +
                        "Continue?",
                        "Replace Items",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                if (confirm != MessageBoxResult.Yes)
                    return;
            }


            ReturnItems.Clear();


            foreach (
                var d in
                invoice.Details ??
                Enumerable.Empty<MPurchaseDetail>())
            {
                ReturnItems.Add(
                    new ReturnLineItem(RecalculateTotal)
                    {
                        ProductId = d.ProductId,

                        ProductName = d.ProductName,

                        Barcode = d.Barcode,

                        PurchasedQuantity = d.Quantity,

                        PurchasePrice = d.PurchasePrice,

                        Batch = d.Batch,

                        MfgDate = d.MfgDate,

                        ExpDate = d.ExpDate,

                        Quantity = d.Quantity
                    });
            }


            RecalculateTotal();


            MessageBox.Show(
                $"✔ {ReturnItems.Count} item(s) loaded from " +
                $"{invoice.InvoiceNumber}.\n\n" +
                "Quantities default to the full purchased amount. " +
                "Lower the quantity for a partial return, or remove " +
                "lines you are not returning.",
                "Items Loaded",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }


        // ════════════════════════════════════════════════════════════════════════
        // HISTORY
        // ════════════════════════════════════════════════════════════════════════
        //
        // IMPORTANT:
        // History no longer requires a supplier.
        //
        // GetFilteredReturns() without supplierId returns ALL returns.
        // ════════════════════════════════════════════════════════════════════════
        private void ToggleHistory()
        {
            if (IsHistoryOpen)
            {
                IsHistoryOpen = false;
                return;
            }


            try
            {
                // No supplier filter.
                // This loads return history for ALL suppliers.
                SupplierHistory =
                    new ObservableCollection<MPurchaseReturnMaster>(
                        _returnService.GetFilteredReturns());


                IsHistoryOpen = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not load return history:\n\n{ex.Message}",
                    "History Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                SupplierHistory =
                    new ObservableCollection<MPurchaseReturnMaster>();

                IsHistoryOpen = true;
            }
        }


        // ════════════════════════════════════════════════════════════════════════
        // Open history return
        // ════════════════════════════════════════════════════════════════════════
        private void LoadHistoryReturn(
            MPurchaseReturnMaster? master)
        {
            if (master == null)
                return;


            _returnSaved = false;

            _originalReturnTotal =
                master.TotalAmount;

            _editingMasterId =
                master.Id;


            ReturnInvoiceNumber =
                master.ReturnInvoiceNumber;

            ReturnDate =
                master.ReturnDate;

            Remarks =
                master.Remarks ?? string.Empty;


            // Find supplier.
            var matchedSupplier =
                Suppliers.FirstOrDefault(
                    s => s.Id == master.SupplierId);


            if (matchedSupplier != null)
            {
                // Set backing field directly so changing supplier
                // does not clear the invoice while loading history.
                _selectedSupplier =
                    matchedSupplier;

                OnPropertyChanged(
                    nameof(SelectedSupplier));

                OnPropertyChanged(
                    nameof(SupplierHintVisibility));


                SupplierBalance =
                    _supplierService
                        .RecalculateAndUpdateSupplierBalance(
                            matchedSupplier.Id);


                LoadSupplierData();
            }


            // Restore original invoice link.
            _linkedInvoiceNumber =
                master.InvoiceNumber ?? string.Empty;


            _selectedSupplierInvoice =
                SupplierInvoices.FirstOrDefault(
                    i => string.Equals(
                        i.InvoiceNumber,
                        _linkedInvoiceNumber,
                        StringComparison.OrdinalIgnoreCase));


            OnPropertyChanged(
                nameof(SelectedSupplierInvoice));

            OnPropertyChanged(
                nameof(LinkedInvoiceDisplay));

            OnPropertyChanged(
                nameof(HasLinkedInvoice));


            // Restore return details.
            ReturnItems =
                new ObservableCollection<ReturnLineItem>(
                    master.MPurchaseReturnDetail
                        .Select(
                            d => ReturnLineItem.From(
                                d,
                                RecalculateTotal)));


            RecalculateTotal();


            IsHistoryOpen = false;


            OnPropertyChanged(
                nameof(IsEditMode));

            OnPropertyChanged(
                nameof(ModeText));

            OnPropertyChanged(
                nameof(ProjectedBalance));
        }


        // ════════════════════════════════════════════════════════════════════════
        // SAVE
        // ════════════════════════════════════════════════════════════════════════
        private void SaveReturn()
        {
            if (_returnSaved)
            {
                MessageBox.Show(
                    "This return has already been saved.\n\n" +
                    "Click Reset to start a new return.",
                    "Return Already Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }


            if (SelectedSupplier == null)
            {
                MessageBox.Show(
                    "Please select a supplier.",
                    "Supplier Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }


            if (SelectedSupplierInvoice == null ||
                string.IsNullOrWhiteSpace(_linkedInvoiceNumber))
            {
                MessageBox.Show(
                    "Please select an original purchase invoice " +
                    "and load its items.",
                    "Original Invoice Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }


            if (!ReturnItems.Any())
            {
                MessageBox.Show(
                    "Please load at least one item from the " +
                    "original purchase invoice.",
                    "No Items",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }


            // Validate quantities.
            var zeroQty =
                ReturnItems.FirstOrDefault(
                    i => i.Quantity <= 0);

            if (zeroQty != null)
            {
                MessageBox.Show(
                    $"'{zeroQty.ProductName}' has a quantity " +
                    "of 0 or less.\n\n" +
                    "Enter a valid quantity or remove the line.",
                    "Invalid Quantity",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }


            // Prevent returning more than the invoice quantity.
            var overReturn =
                ReturnItems.FirstOrDefault(
                    i =>
                        i.PurchasedQuantity.HasValue &&
                        i.Quantity >
                        i.PurchasedQuantity.Value);


            if (overReturn != null)
            {
                MessageBox.Show(
                    $"'{overReturn.ProductName}': you're returning " +
                    $"{overReturn.Quantity}, but only " +
                    $"{overReturn.PurchasedQuantity} were purchased " +
                    $"on invoice {_linkedInvoiceNumber}.",
                    "Quantity Too High",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }


            RecalculateTotal();


            var master =
                new MPurchaseReturnMaster
                {
                    ReturnInvoiceNumber =
                        ReturnInvoiceNumber,

                    InvoiceNumber =
                        _linkedInvoiceNumber,

                    SupplierId =
                        SelectedSupplier.Id,

                    ReturnDate =
                        ReturnDate,

                    TotalAmount =
                        TotalAmount,

                    Remarks =
                        string.IsNullOrWhiteSpace(Remarks)
                            ? null
                            : Remarks.Trim(),

                    MPurchaseReturnDetail =
                        ReturnItems
                            .Select(i => i.ToDetail())
                            .ToList()
                };


            bool success;

            try
            {
                if (_editingMasterId > 0)
                {
                    success =
                        _returnService.UpdateReturn(
                            _editingMasterId,
                            master);
                }
                else
                {
                    success =
                        _returnService.AddReturn(master);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to save return:\n\n{ex.Message}",
                    "Save Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }


            if (!success)
            {
                MessageBox.Show(
                    "Error occurred while saving the return.",
                    "Save Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }


            // Supplier balance adjustment.
            //
            // A return reduces the amount owed to the supplier.
            //
            // New return:
            //     difference = total - 0
            //
            // Edited return:
            //     difference = new total - old total
            //
            // Therefore supplier balance changes by -difference.
            decimal difference =
                master.TotalAmount -
                _originalReturnTotal;


            if (difference != 0)
            {
                decimal newBalance =
                    _supplierService
                        .AdjustSupplierBalance(
                            SelectedSupplier.Id,
                            -difference);

                SupplierBalance =
                    newBalance;

                SelectedSupplier.CurrentBalance =
                    newBalance;
            }


            _originalReturnTotal =
                master.TotalAmount;

            _returnSaved = true;


            OnPropertyChanged(
                nameof(ProjectedBalance));


            // Refresh history.
            //
            // Since history is now global, this does not depend
            // on supplier selection.
            try
            {
                SupplierHistory =
                    new ObservableCollection<MPurchaseReturnMaster>(
                        _returnService.GetFilteredReturns());
            }
            catch
            {
                // Do not fail a successful save just because
                // history refresh failed.
            }


            MessageBox.Show(
                _editingMasterId > 0
                    ? "✔ Return updated successfully!"
                    : "✔ Return recorded and stock updated!",
                "Saved",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }


        // ════════════════════════════════════════════════════════════════════════
        // DELETE
        // ════════════════════════════════════════════════════════════════════════
        private void DeleteReturn(
            MPurchaseReturnMaster? target)
        {
            long id;

            decimal total;

            long supplierId;

            string number;

            bool isCurrentlyLoaded;


            if (target != null)
            {
                id =
                    target.Id;

                total =
                    target.TotalAmount;

                supplierId =
                    target.SupplierId;

                number =
                    target.ReturnInvoiceNumber;

                isCurrentlyLoaded =
                    _editingMasterId ==
                    target.Id;
            }
            else if (
                _editingMasterId > 0 &&
                SelectedSupplier != null)
            {
                id =
                    _editingMasterId;

                total =
                    _originalReturnTotal;

                supplierId =
                    SelectedSupplier.Id;

                number =
                    ReturnInvoiceNumber;

                isCurrentlyLoaded =
                    true;
            }
            else
            {
                return;
            }


            var confirm =
                MessageBox.Show(
                    $"Delete return {number}?\n\n" +
                    "The returned stock will be added back and " +
                    "the supplier's balance restored.\n\n" +
                    "This cannot be undone.",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);


            if (confirm != MessageBoxResult.Yes)
                return;


            bool deleted;

            try
            {
                deleted =
                    _returnService.DeleteReturn(id);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to delete return:\n\n{ex.Message}",
                    "Delete Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }


            if (!deleted)
            {
                MessageBox.Show(
                    "The return could not be deleted.",
                    "Delete Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }


            // Restore supplier balance.
            decimal newBalance =
                _supplierService
                    .AdjustSupplierBalance(
                        supplierId,
                        total);


            if (
                SelectedSupplier != null &&
                SelectedSupplier.Id == supplierId)
            {
                SupplierBalance =
                    newBalance;

                SelectedSupplier.CurrentBalance =
                    newBalance;
            }


            if (isCurrentlyLoaded)
            {
                ResetForm();
            }
            else
            {
                // Refresh ALL history because history is global.
                try
                {
                    SupplierHistory =
                        new ObservableCollection<MPurchaseReturnMaster>(
                            _returnService.GetFilteredReturns());
                }
                catch
                {
                    SupplierHistory =
                        new ObservableCollection<MPurchaseReturnMaster>();
                }

                OnPropertyChanged(
                    nameof(ProjectedBalance));
            }
        }
    }
}

