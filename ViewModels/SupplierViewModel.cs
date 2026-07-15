using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using WPFCRUDApp.Models;

namespace MyWPFCRUDApp.ViewModels
{
    public class SupplierViewModel: BaseViewModel
    {
        public ICommand SupplierSaveCommand { get; }
        public ICommand SupplierDeleteCommand { get; }
        public ICommand SupplierResetCommand { get; }

        private readonly SupplierService _supplierService;
        private ObservableCollection<MSupplier> _supplier;
        public ObservableCollection<MSupplier> Supplier
        {
            get => _supplier;
            set => SetProperty(ref _supplier, value);
        }
        private MSupplier _selectedSupplier;
        public MSupplier SelectedSupplier
        {
            get => _selectedSupplier;
            set
            {
                if (SetProperty(ref _selectedSupplier, value) && value != null)
                {
                    MSupplier = value;
                }
            }
        }
        private MSupplier _mSupplier;
        public MSupplier MSupplier
        {
            get => _mSupplier;
            set => SetProperty(ref _mSupplier, value);
        }
        public void LoadData()
        {
            var categoryData = _supplierService.GetAllSuppliers();
            Supplier = new ObservableCollection<MSupplier>(categoryData);
        }
        private void Reset()
        {
            MSupplier = new MSupplier();
            SelectedSupplier = null;
        }
        private void Save()
        {
            if (string.IsNullOrWhiteSpace(MSupplier.SupplierName))
            {
                System.Windows.MessageBox.Show("Category Name is required!");
                return;
            }

            bool success;
            if (MSupplier.Id <= 0)
                success = _supplierService.InsertSupplier(MSupplier);
            else
                success = _supplierService.UpdateSupplier(MSupplier); // Note: Your service currently names this UpdateStudent

            if (success)
            {
                LoadData();
                Reset();
            }
        }
        private void Delete()
        {
            var result = System.Windows.MessageBox.Show("Are you sure you want to delete this category?", "Confirm Delete", System.Windows.MessageBoxButton.YesNo);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                if (_supplierService.DeleteSupplier((long)SelectedSupplier.Id))
                {
                    LoadData();
                    Reset();
                }
            }
        }
        public SupplierViewModel()
        {

            MSupplier = new MSupplier();
            _supplierService = new SupplierService();
            SupplierSaveCommand = new RelayCommand(_ => Save());
            SupplierDeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedSupplier != null);
            SupplierResetCommand = new RelayCommand(_ => Reset());
            LoadData();

        }
    }
}
