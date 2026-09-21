using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using WPFCRUDApp.Models;

namespace MyWPFCRUDApp.ViewModels
{
    public class ScanPoolItem : BaseViewModel
    {
        private readonly Action? _onChanged;

        public ScanPoolItem(string barcode, string? name, double qty, Action? onChanged)
        {
            Barcode = barcode;
            ProductName = name;
            _quantity = qty;
            _onChanged = onChanged;
        }

        public string Barcode { get; }
        public string? ProductName { get; }

        private double _quantity;
        public double Quantity
        {
            get => _quantity;
            set { if (SetProperty(ref _quantity, value)) _onChanged?.Invoke(); }
        }
    }

    public class UnmatchedItem
    {
        public string Barcode { get; set; } = "";
        public string? ProductName { get; set; }
        public double Quantity { get; set; }
        public string Display => $"{ProductName ?? "Unknown item"} ({Barcode}) ×{Quantity:0.##}";
    }

    public class ReturnScanViewModel : BaseViewModel
    {
        private static readonly StringComparer Cmp = StringComparer.OrdinalIgnoreCase;

        private readonly PurchaseService _purchaseService;
        private readonly PurchaseReturnService _returnService;
        private readonly Func<IReadOnlyDictionary<long, string>> _supplierNames;
        private readonly Action<BillCandidate> _onUseBill;

        private List<MPurchaseMaster>? _purchases;   // cached for the scan session
        private bool _suspend;

        public ObservableCollection<ScanPoolItem> Pool { get; } = new();

        private ObservableCollection<BillCandidate> _candidates = new();
        public ObservableCollection<BillCandidate> Candidates
        {
            get => _candidates;
            private set => SetProperty(ref _candidates, value);
        }

        private ObservableCollection<UnmatchedItem> _unmatched = new();
        public ObservableCollection<UnmatchedItem> Unmatched
        {
            get => _unmatched;
            private set => SetProperty(ref _unmatched, value);
        }

        private string _scanInput = "";
        public string ScanInput
        {
            get => _scanInput;
            set => SetProperty(ref _scanInput, value);
        }

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        private string _poolSummary = "";
        public string PoolSummary
        {
            get => _poolSummary;
            private set => SetProperty(ref _poolSummary, value);
        }

        public bool HasPool => Pool.Count > 0;

        public ICommand AddScanCommand { get; }
        public ICommand RemoveScanCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand UseBillCommand { get; }

        public ReturnScanViewModel(
            PurchaseService purchaseService,
            PurchaseReturnService returnService,
            Func<IReadOnlyDictionary<long, string>> supplierNames,
            Action<BillCandidate> onUseBill)
        {
            _purchaseService = purchaseService;
            _returnService = returnService;
            _supplierNames = supplierNames;
            _onUseBill = onUseBill;

            AddScanCommand = new RelayCommand(_ => AddScan());
            RemoveScanCommand = new RelayCommand(p => RemoveScan(p as ScanPoolItem));
            ClearCommand = new RelayCommand(_ => ClearPool());
            UseBillCommand = new RelayCommand(p => { if (p is BillCandidate c) _onUseBill(c); });
        }

        private void AddScan()
        {
            var code = (ScanInput ?? "").Trim();
            ScanInput = "";
            if (code.Length == 0) return;

            var existing = Pool.FirstOrDefault(p => Cmp.Equals(p.Barcode, code));
            if (existing != null)
            {
                existing.Quantity += 1;          // triggers Recompute
                return;
            }

            string? name = null;
            try { name = _returnService.GetProductNameByBarcode(code); }
            catch { /* name is cosmetic; ignore lookup failure */ }

            Pool.Add(new ScanPoolItem(code, name, 1, Recompute));
            Recompute();

            if (name == null)
                StatusMessage = $"Barcode {code} was not found in your product list.";
        }

        private void RemoveScan(ScanPoolItem? item)
        {
            if (item == null) return;
            Pool.Remove(item);
            Recompute();
        }

        private void ClearPool()
        {
            Pool.Clear();
            Recompute();
        }

        /// Called after a return is saved: takes the returned quantities out of the pool.
        public void Consume(IEnumerable<(string? Barcode, double Quantity)> returned)
        {
            _suspend = true;
            try
            {
                foreach (var (barcode, qty) in returned)
                {
                    var item = Pool.FirstOrDefault(p => Cmp.Equals(p.Barcode, barcode?.Trim()));
                    if (item != null) item.Quantity -= qty;
                }
                foreach (var done in Pool.Where(p => p.Quantity <= 1e-9).ToList())
                    Pool.Remove(done);
            }
            finally { _suspend = false; }

            Recompute();
        }

        private Dictionary<string, double> BuildPool() =>
            Pool.Where(p => p.Quantity > 0)
                .GroupBy(p => p.Barcode, Cmp)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.Quantity), Cmp);

        private void Recompute()
        {
            if (_suspend) return;

            var pool = BuildPool();
            OnPropertyChanged(nameof(HasPool));

            if (pool.Count == 0)
            {
                _purchases = null;               // next session reloads fresh bills
                PoolSummary = "";
                StatusMessage = "";
                Candidates = new();
                Unmatched = new();
                return;
            }

            PoolSummary = $"{pool.Count} item(s), {pool.Values.Sum():0.##} unit(s) left to return";

            try
            {
                _purchases ??= _purchaseService.GetFilteredPurchases().ToList();

                // Read fresh every time so returns saved during this session count.
                var returned = _returnService.GetReturnedQuantities();
                var result = ReturnBillMatcher.Rank(_purchases, pool, returned, _supplierNames());

                Candidates = new ObservableCollection<BillCandidate>(result.Candidates);
                Unmatched = new ObservableCollection<UnmatchedItem>(
                    result.Unmatched.Select(kv => new UnmatchedItem
                    {
                        Barcode = kv.Key,
                        ProductName = Pool.FirstOrDefault(p => Cmp.Equals(p.Barcode, kv.Key))?.ProductName,
                        Quantity = kv.Value
                    }));

                StatusMessage = Candidates.Count == 0
                    ? "No purchase bill has these items available to return."
                    : "";
            }
            catch (Exception ex)
            {
                Candidates = new();
                Unmatched = new();
                StatusMessage = $"Could not match bills: {ex.Message}";
            }
        }
    }
}