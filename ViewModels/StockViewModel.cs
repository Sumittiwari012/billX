using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace MyWPFCRUDApp.ViewModels
{
    /// <summary>Current stock per godown/store, manual opening stock / adjustments (godown only), and the ledger.</summary>
    public class StockViewModel : NotifyBase
    {
        public string[] LocationTypes { get; } = { LocationType.Godown, LocationType.Store };
        public string[] PostModes { get; } = { "Opening Stock", "Adjustment" };

        public ObservableCollection<LocationOption> Locations { get; } = new ObservableCollection<LocationOption>();
        public ObservableCollection<StockRow> Stock { get; } = new ObservableCollection<StockRow>();
        public ObservableCollection<LedgerRow> Ledger { get; } = new ObservableCollection<LedgerRow>();

        string _type = LocationType.Godown;
        long? _locId = 0;
        string _search = "", _ledgerBarcode = "";
        bool _busy;

        string _postBarcode = "", _postMode = "Opening Stock", _postReason = "";
        decimal _postQty;

        public string SelectedLocationType
        {
            get => _type;
            set
            {
                if (!Set(ref _type, value)) return;
                OnPropertyChanged(nameof(CanPost));
                LoadLocations();
            }
        }

        public long? SelectedLocationId
        {
            get => _locId;
            set { if (Set(ref _locId, value) && value.HasValue && !_busy) Refresh(); }
        }

        public string Search { get => _search; set => Set(ref _search, value); }
        public string LedgerBarcode { get => _ledgerBarcode; set => Set(ref _ledgerBarcode, value); }

        public bool CanPost => _type == LocationType.Godown;
        public string PostBarcode { get => _postBarcode; set => Set(ref _postBarcode, value); }
        public decimal PostQty { get => _postQty; set => Set(ref _postQty, value); }
        public string PostMode { get => _postMode; set => Set(ref _postMode, value); }
        public string PostReason { get => _postReason; set => Set(ref _postReason, value); }

        public ICommand RefreshCommand { get; }
        public ICommand PostCommand { get; }
        public ICommand LoadLedgerCommand { get; }

        public StockViewModel()
        {
            RefreshCommand = new DelegateCommand(Refresh);
            PostCommand = new DelegateCommand(Post);
            LoadLedgerCommand = new DelegateCommand(LoadLedger);
            LoadLocations();
        }

        long? LocFilter => (_locId ?? 0) == 0 ? (long?)null : _locId;

        void LoadLocations()
        {
            try
            {
                _busy = true;
                Locations.Clear();
                Locations.Add(new LocationOption { Id = 0, Name = "— All —", Type = _type });
                foreach (var o in LocationService.GetOptions(_type, false)) Locations.Add(o);
                SelectedLocationId = 0;
            }
            catch (Exception ex) { Msg.Error(ex); }
            finally { _busy = false; }
            Refresh();
        }

        void Refresh()
        {
            try
            {
                Stock.Clear();
                foreach (var r in StockService.GetStock(_type, LocFilter, _search)) Stock.Add(r);
            }
            catch (Exception ex) { Msg.Error(ex); }
        }

        void LoadLedger()
        {
            try
            {
                Ledger.Clear();
                foreach (var r in StockService.GetLedger(_type, LocFilter, _ledgerBarcode)) Ledger.Add(r);
            }
            catch (Exception ex) { Msg.Error(ex); }
        }

        void Post()
        {
            try
            {
                if (LocFilter == null) throw new InvalidOperationException("Select a specific godown first.");
                var movement = _postMode == "Opening Stock" ? MovementType.OpeningStock : MovementType.Adjustment;
                StockService.PostGodownStock(LocFilter.Value, _postBarcode, _postQty, movement, _postReason, StockContext.CurrentUser);
                PostBarcode = ""; PostQty = 0; PostReason = "";
                Refresh();
            }
            catch (Exception ex) { Msg.Error(ex); }
        }
    }
}
