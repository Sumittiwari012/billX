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
    /// <summary>Read-only view model for Store Stock.</summary>
    public class StoreStockViewModel : BaseViewModel
    {
        private const string AllStoresLabel = "All Stores";

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
                    RefreshFilter();
            }
        }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    RefreshFilter();
            }
        }

        private ObservableCollection<StoreStockRow> _rows = new ObservableCollection<StoreStockRow>();
        private ICollectionView _stockView;
        public ICollectionView StockView
        {
            get => _stockView;
            private set => SetProperty(ref _stockView, value);
        }

        private string _summary;
        public string Summary
        {
            get => _summary;
            set => SetProperty(ref _summary, value);
        }

        public void LoadData()
        {
            var keepStoreId = SelectedStore?.Id ?? 0;

            var stores = _service.GetStores();
            stores.Insert(0, new Store { Id = 0, StoreName = AllStoresLabel });
            Stores = new ObservableCollection<Store>(stores);
            _selectedStore = Stores.FirstOrDefault(s => s.Id == keepStoreId) ?? Stores[0];
            OnPropertyChanged(nameof(SelectedStore));

            _rows = new ObservableCollection<StoreStockRow>(_service.GetStoreStock());
            var view = CollectionViewSource.GetDefaultView(_rows);
            view.Filter = Filter;
            StockView = view;

            UpdateSummary();
        }

        private void RefreshFilter()
        {
            StockView?.Refresh();
            UpdateSummary();
        }

        private bool Filter(object o)
        {
            if (!(o is StoreStockRow row)) return false;

            if (SelectedStore != null && SelectedStore.Id != 0 && row.StoreId != SelectedStore.Id)
                return false;

            var text = SearchText?.Trim();
            if (string.IsNullOrEmpty(text)) return true;

            return Has(row.Barcode, text) || Has(row.ProductCode, text) || Has(row.ProductName, text);
        }

        private static bool Has(string source, string text) =>
            !string.IsNullOrEmpty(source) && source.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;

        private void UpdateSummary()
        {
            if (StockView == null) { Summary = ""; return; }
            var rows = StockView.Cast<StoreStockRow>().ToList();
            Summary = $"{rows.Count} item(s)  |  Total quantity: {rows.Sum(r => r.Quantity):N3}";
        }

        public StoreStockViewModel()
        {
            RefreshCommand = new RelayCommand(_ => LoadData());
            LoadData();
        }
    }
}