using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;

namespace MyWPFCRUDApp.ViewModels
{
    /// <summary>Read-only view model for Store Stock Transfers.</summary>
    public class StoreStockTransferViewModel : BaseViewModel
    {
        private const string AllStoresLabel = "All Stores";
        private const string AllStatusLabel = "All";

        private readonly StoreStockService _service = new StoreStockService();

        public ICommand RefreshCommand { get; }

        private ObservableCollection<Store> _stores;
        public ObservableCollection<Store> Stores
        {
            get => _stores;
            set => SetProperty(ref _stores, value);
        }

        private Store _selectedStore;
        public Store SelectedStore
        {
            get => _selectedStore;
            set
            {
                if (SetProperty(ref _selectedStore, value))
                    TransferView?.Refresh();
            }
        }

        private ObservableCollection<string> _statuses;
        public ObservableCollection<string> Statuses
        {
            get => _statuses;
            set => SetProperty(ref _statuses, value);
        }

        private string _selectedStatus = AllStatusLabel;
        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (SetProperty(ref _selectedStatus, value))
                    TransferView?.Refresh();
            }
        }

        private ObservableCollection<StoreTransferRow> _transfers = new ObservableCollection<StoreTransferRow>();
        private ICollectionView _transferView;
        public ICollectionView TransferView
        {
            get => _transferView;
            private set => SetProperty(ref _transferView, value);
        }

        private StoreTransferRow _selectedTransfer;
        public StoreTransferRow SelectedTransfer
        {
            get => _selectedTransfer;
            set
            {
                if (SetProperty(ref _selectedTransfer, value))
                    LoadItems();
            }
        }

        private ObservableCollection<StoreTransferItemRow> _items = new ObservableCollection<StoreTransferItemRow>();
        public ObservableCollection<StoreTransferItemRow> TransferItems
        {
            get => _items;
            set => SetProperty(ref _items, value);
        }

        public void LoadData()
        {
            var keepStoreId = SelectedStore?.Id ?? 0;
            var keepTransferId = SelectedTransfer?.Id;

            var stores = _service.GetStores();
            stores.Insert(0, new Store { Id = 0, StoreName = AllStoresLabel });
            Stores = new ObservableCollection<Store>(stores);
            _selectedStore = Stores.FirstOrDefault(s => s.Id == keepStoreId) ?? Stores[0];
            OnPropertyChanged(nameof(SelectedStore));

            var data = _service.GetStoreTransfers();
            _transfers = new ObservableCollection<StoreTransferRow>(data);

            var statuses = data.Select(t => t.Status)
                               .Where(s => !string.IsNullOrWhiteSpace(s))
                               .Distinct().OrderBy(s => s).ToList();
            statuses.Insert(0, AllStatusLabel);
            Statuses = new ObservableCollection<string>(statuses);
            _selectedStatus = Statuses.Contains(_selectedStatus) ? _selectedStatus : AllStatusLabel;
            OnPropertyChanged(nameof(SelectedStatus));

            var view = CollectionViewSource.GetDefaultView(_transfers);
            view.Filter = Filter;
            TransferView = view;

            SelectedTransfer = keepTransferId.HasValue
                ? _transfers.FirstOrDefault(t => t.Id == keepTransferId.Value)
                : null;
        }

        private void LoadItems()
        {
            TransferItems = SelectedTransfer == null
                ? new ObservableCollection<StoreTransferItemRow>()
                : new ObservableCollection<StoreTransferItemRow>(_service.GetTransferItems(SelectedTransfer.Id));
        }

        private bool Filter(object o)
        {
            if (!(o is StoreTransferRow t)) return false;

            if (SelectedStore != null && SelectedStore.Id != 0)
            {
                bool involved =
                    (t.FromType == "Store" && t.FromId == SelectedStore.Id) ||
                    (t.ToType == "Store" && t.ToId == SelectedStore.Id);
                if (!involved) return false;
            }

            if (!string.IsNullOrEmpty(SelectedStatus) && SelectedStatus != AllStatusLabel &&
                !string.Equals(t.Status, SelectedStatus, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        public StoreStockTransferViewModel()
        {
            RefreshCommand = new RelayCommand(_ => LoadData());
            LoadData();
        }
    }
}