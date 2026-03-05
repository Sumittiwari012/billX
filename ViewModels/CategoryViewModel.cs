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
namespace MyWPFCRUDApp.ViewModels
{
    public class CategoryViewModel:BaseViewModel
    {
        private readonly CategoryService _categoryService = new();
        public CategoryViewModel()
        {
            AddCommand = new RelayCommand(_ => Add());
            UpdateCommand = new RelayCommand(_ => Update(), _ => SelectedCategory != null);
            DeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedCategory != null);
            RefreshCommand = new RelayCommand(_ => { ClearFields(); LoadData(); });
            LoadData();
        }

        // ── Properties ────────────────────────────────────────────────────────
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
                    CategoryName = value.CategoryName;
                    
                }
            }
        }

        private string _categoryName;
        public string CategoryName
        {
            get => _categoryName;
            set => SetProperty(ref _categoryName, value);
        }

        

        // ── Commands ──────────────────────────────────────────────────────────
        public ICommand AddCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand RefreshCommand { get; }

        // ── Constructor ───────────────────────────────────────────────────────


        // ── Methods ───────────────────────────────────────────────────────────
        public void LoadData()
        {
            var list = _categoryService.GetAllCategory();
            Category = new ObservableCollection<MCategory>(list);
        }

        private void ClearFields()
        {
            CategoryName = string.Empty;
            SelectedCategory = null;
        }

        private bool TryGetCategory(out MCategory c)
        {
            c = new MCategory();
            if (string.IsNullOrWhiteSpace(CategoryName))
            {
                MessageBox.Show("Category name is required.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            c = new MCategory
            {
                Id = SelectedCategory?.Id ?? -1,
                CategoryName = CategoryName.Trim()
            };
            return true;
        }

        private void Add()
        {
            if (!TryGetCategory(out var s)) return;
            if (_categoryService.InsertCategory(s)) { ClearFields(); LoadData(); }
        }

        private void Update()
        {
            if (SelectedCategory == null)
            { MessageBox.Show("Select a row first."); return; }
            if (!TryGetCategory(out var s)) return;
            if (_categoryService.UpdateCategory(s)) { ClearFields(); LoadData(); }
        }

        private void Delete()
        {
            if (SelectedCategory == null)
            { MessageBox.Show("Select a row first."); return; }
            var r = MessageBox.Show("Delete this student?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r == MessageBoxResult.Yes && _categoryService.DeleteCategory(SelectedCategory.Id))
            { ClearFields(); LoadData(); }
        }
    }
}
