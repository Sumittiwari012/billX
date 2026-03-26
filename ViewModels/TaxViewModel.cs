using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
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
    public class TaxViewModel: BaseViewModel
    {
        public ICommand TaxSaveCommand { get; }
        public ICommand TaxDeleteCommand { get; }
        public ICommand TaxResetCommand { get; }

        private readonly TaxService _taxService;
        private ObservableCollection<MTaxCategory> _taxcategory;
        public ObservableCollection<MTaxCategory> TaxCategory
        {
            get => _taxcategory;
            set => SetProperty(ref _taxcategory, value);
        }
        private MTaxCategory _selectedTaxCategory;
        public MTaxCategory SelectedTaxCategory
        {
            get => _selectedTaxCategory;
            set
            {
                if (SetProperty(ref _selectedTaxCategory, value) && value != null)
                {
                    MTaxCategory = value;
                }
            }
        }
        private MTaxCategory _mTaxcategory;
        public MTaxCategory MTaxCategory
        {
            get => _mTaxcategory;
            set => SetProperty(ref _mTaxcategory, value);
        }
        public void LoadData()
        {
            var categoryData = _taxService.GetTaxCategory();
            TaxCategory = new ObservableCollection<MTaxCategory>(categoryData);
        }
        private void Reset()
        {
            MTaxCategory = new MTaxCategory();
            SelectedTaxCategory = null;
        }
        private void Save()
        {
            if (string.IsNullOrWhiteSpace(MTaxCategory.CategoryName))
            {
                System.Windows.MessageBox.Show("Category Name is required!");
                return;
            }

            bool success;
            if (MTaxCategory.Id <= 0)
                success = _taxService.InsertTax(MTaxCategory);
            else
                success = _taxService.UpdateTaxCategory(MTaxCategory); // Note: Your service currently names this UpdateStudent

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
                if (_taxService.DeleteTaxCategory((long)SelectedTaxCategory.Id))
                {
                    LoadData();
                    Reset();
                }
            }
        }
        public TaxViewModel()
        {

            MTaxCategory = new MTaxCategory();
            _taxService = new TaxService();
            TaxSaveCommand = new RelayCommand(_ => Save());
            TaxDeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedTaxCategory != null);
            TaxResetCommand = new RelayCommand(_ => Reset());
            LoadData();

        }
    }
}
