using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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
                    TaxContext.SelectedTax = value;
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
            TaxCategory = new ObservableCollection<MTaxCategory>(
                _taxService.GetTaxCategory());

            if (TaxContext.SelectedTax != null)
            {
                SelectedTaxCategory = TaxCategory
                    .FirstOrDefault(x => x.Id == TaxContext.SelectedTax.Id);
            }

            if (SelectedTaxCategory == null && TaxCategory.Any())
            {
                SelectedTaxCategory = TaxCategory.First();
            }
        }
        private void Reset()
        {
            MTaxCategory = new MTaxCategory();

            // Do NOT clear SelectedTaxCategory
        }
        private void Save()
        {
            if (MTaxCategory.CGST < 0 ||
                MTaxCategory.SGST < 0 ||
                MTaxCategory.IGST < 0)
            {
                MessageBox.Show("Invalid tax values.");
                return;
            }

            bool success;

            if (MTaxCategory.Id <= 0)
                success = _taxService.InsertTax(MTaxCategory);
            else
                success = _taxService.UpdateTaxCategory(MTaxCategory);

            if (success)
            {
                long selectedId = MTaxCategory.Id;

                LoadData();

                SelectedTaxCategory =
                    TaxCategory.FirstOrDefault(x => x.Id == selectedId);

                MTaxCategory = SelectedTaxCategory;
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
