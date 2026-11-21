using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using MySql.Data.MySqlClient;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace MyWPFCRUDApp.ViewModels
{
    public class StoreViewModel : BaseViewModel
    {
        public ObservableCollection<Store> Stores { get; } = new();
        public ObservableCollection<Godown> GodownOptions { get; } = new();

        private Store? _selected;
        public Store? SelectedStore
        {
            get => _selected;
            set
            {
                if (SetProperty(ref _selected, value) && value != null)
                {
                    EditingId = value.Id;
                    StoreName = value.StoreName;
                    SelectedGodownId = value.GodownId;
                    Address = value.Address ?? "";
                    IsActive = value.IsActive;
                }
            }
        }

        private long _editingId;
        public long EditingId { get => _editingId; set => SetProperty(ref _editingId, value); }

        private string _storeName = "";
        public string StoreName { get => _storeName; set => SetProperty(ref _storeName, value); }

        private long? _selectedGodownId;
        public long? SelectedGodownId { get => _selectedGodownId; set => SetProperty(ref _selectedGodownId, value); }

        private string _address = "";
        public string Address { get => _address; set => SetProperty(ref _address, value); }

        private bool _isActive = true;
        public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }

        public ICommand SaveCommand { get; }
        public ICommand NewCommand { get; }
        public ICommand DeleteCommand { get; }

        public StoreViewModel()
        {
            SaveCommand = new RelayCommand(_ => Save());
            NewCommand = new RelayCommand(_ => ClearForm());
            DeleteCommand = new RelayCommand(_ => Delete());
            LoadGodowns();
            Load();
        }

        private void LoadGodowns()
        {
            try
            {
                GodownOptions.Clear();
                foreach (var g in GodownService.GetAll().Where(g => g.IsActive))
                    GodownOptions.Add(g);
            }
            catch (Exception ex) { Error("Failed to load godowns", ex); }
        }

        private void Load()
        {
            try
            {
                Stores.Clear();
                foreach (var s in StoreService.GetAll()) Stores.Add(s);
            }
            catch (Exception ex) { Error("Failed to load stores", ex); }
        }

        private void ClearForm()
        {
            SelectedStore = null;
            EditingId = 0;
            StoreName = "";
            SelectedGodownId = null;
            Address = "";
            IsActive = true;
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(StoreName))
            {
                MessageBox.Show("Store name is required.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var s = new Store
            {
                Id = EditingId,
                StoreName = StoreName.Trim(),
                GodownId = SelectedGodownId,
                Address = string.IsNullOrWhiteSpace(Address) ? null : Address.Trim(),
                IsActive = IsActive
            };

            try
            {
                if (EditingId == 0) StoreService.Insert(s);
                else StoreService.Update(s);

                Load();
                ClearForm();
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                MessageBox.Show("A store with this name already exists.", "Duplicate",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex) { Error("Failed to save store", ex); }
        }

        private void Delete()
        {
            if (SelectedStore == null)
            {
                MessageBox.Show("Select a store to delete.", "Delete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var res = MessageBox.Show($"Delete store '{SelectedStore.StoreName}'?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            try
            {
                StoreService.Delete(SelectedStore.Id);
                Load();
                ClearForm();
            }
            catch (Exception ex) { Error("Failed to delete store", ex); }
        }

        private static void Error(string msg, Exception ex) =>
            MessageBox.Show($"{msg}:\n\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
