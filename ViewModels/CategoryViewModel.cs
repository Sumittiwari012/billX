using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MyWPFCRUDApp.ViewModels
{
    public class CategoryViewModel:BaseViewModel
    {
        public ICommand CategorySaveCommand { get; }
        public ICommand CategoryDeleteCommand { get; }
        public ICommand CategoryResetCommand { get; }
        private readonly CategoryService _categoryService;
        private ObservableCollection<MCategory> _category;
        public ObservableCollection<MCategory> Category
        {
            get => _category;
            set => SetProperty(ref _category, value);
        }
        private MCategory _selectedCategory;
        public MCategory SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value) && value != null)
                {
                    MCategory = value;
                }
            }
        }
        private MCategory _mcategory;
        public MCategory MCategory
        {
            get => _mcategory;
            set => SetProperty(ref _mcategory, value);
        }
        public void LoadData()
        {
            var categoryData = _categoryService.GetCategory();
            Category = new ObservableCollection<MCategory>(categoryData);
        }
        private void Reset()
        {
            MCategory = new MCategory();
            SelectedCategory = null;
        }
        private void Save()
        {
            if (string.IsNullOrWhiteSpace(MCategory.CategoryName))
            {
                System.Windows.MessageBox.Show("Category Name is required!");
                return;
            }

            bool success;
            if (MCategory.Id <= 0)
                success = _categoryService.InsertCategory(MCategory);
            else
                success = _categoryService.UpdateCategory(MCategory); // Note: Your service currently names this UpdateStudent

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
                if (_categoryService.DeleteCategory((long)SelectedCategory.Id))
                {
                    LoadData();
                    Reset();
                }
            }
        }
        public CategoryViewModel()
        {

            MCategory = new MCategory();
            _categoryService = new CategoryService();
            CategorySaveCommand = new RelayCommand(_ => Save());
            CategoryDeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedCategory != null);
            CategoryResetCommand = new RelayCommand(_ => Reset());
            LoadData();

        }
    }
}
