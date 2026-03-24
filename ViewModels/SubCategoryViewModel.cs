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
using static MyWPFCRUDApp.Services.SubCategoryService;

namespace MyWPFCRUDApp.ViewModels
{
    public class SubCategoryViewModel:BaseViewModel
    {
        public ICommand SubCategorySaveCommand { get; }
        public ICommand SubCategoryDeleteCommand { get; }
        public ICommand SubCategoryResetCommand { get; }

        private ObservableCollection<MCategory> _categories;
        public ObservableCollection<MCategory> Categories
        {
            get => _categories;
            set => SetProperty(ref _categories, value);
        }

        private readonly CategoryService _categoryService; // Add this
        private readonly SubCategoryService _subCategoryService;
        private ObservableCollection<SubCategoryDisplayModel> _subCategory;
        public ObservableCollection<SubCategoryDisplayModel> SubCategory
        {
            get => _subCategory;
            set => SetProperty(ref _subCategory, value);
        }
        private SubCategoryDisplayModel _selectedSubCategory;
        public SubCategoryDisplayModel SelectedSubCategory
        {
            get => _selectedSubCategory;
            set
            {
                if (SetProperty(ref _selectedSubCategory, value) && value != null)
                {
                    // MANUALLY MAP the data to your MSubCategory for editing/saving
                    MSubCategory = new MSubCategory
                    {
                        Id = value.Id,
                        SubCategoryName = value.SubCategoryName,
                        CategoryId = value.CategoryId
                    };
                }
            }
        }
        private MSubCategory _mSubCategory;
        public MSubCategory MSubCategory
        {
            get => _mSubCategory;
            set => SetProperty(ref _mSubCategory, value);
        }
        public void LoadData()
        {
            var SubcategoryData = _subCategoryService.GetSubCategory();
            SubCategory = new ObservableCollection<SubCategoryDisplayModel>(SubcategoryData);
        }
        private void Reset()
        {
            MSubCategory = new MSubCategory();
            SelectedSubCategory = null;
        }
        private void Save()
        {
            if (string.IsNullOrWhiteSpace(MSubCategory.SubCategoryName))
            {
                System.Windows.MessageBox.Show("SubCategory Name is required!");
                return;
            }

            bool success;
            if (MSubCategory.Id <= 0)
                success = _subCategoryService.InsertSubCategory(MSubCategory);
            else
                success = _subCategoryService.UpdateSubCategory(MSubCategory); // Note: Your service currently names this UpdateStudent

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
                if (_subCategoryService.DeleteSubCategory((long)SelectedSubCategory.Id))
                {
                    LoadData();
                    Reset();
                }
            }
        }
        public SubCategoryViewModel()
        {
            _categoryService = new CategoryService();
            MSubCategory = new MSubCategory();
            _subCategoryService = new SubCategoryService();
            SubCategorySaveCommand = new RelayCommand(_ => Save());
            SubCategoryDeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedSubCategory != null);
            SubCategoryResetCommand = new RelayCommand(_ => Reset());
            LoadDropdownData();
            LoadData();

        }
        private void LoadDropdownData()
        {
            var data = _categoryService.GetCategory();
            Categories = new ObservableCollection<MCategory>(data);
        }
    }
}
