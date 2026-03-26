using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using WPFCRUDApp.Models;

namespace MyWPFCRUDApp.ViewModels
{
    public class CustomerViewModel : BaseViewModel
    {
        // Commands
        public ICommand CustomerSaveCommand { get; }
        public ICommand CustomerDeleteCommand { get; }
        public ICommand CustomerResetCommand { get; }

        private readonly CustomerService _customerService;

        // 1. Renamed to CustomerList to avoid conflict with the Class Name 'Customer'
        private ObservableCollection<Customer> _customerList;
        public ObservableCollection<Customer> CustomerList
        {
            get => _customerList;
            set => SetProperty(ref _customerList, value);
        }

        private Customer _selectedCustomer;
        public Customer SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (SetProperty(ref _selectedCustomer, value) && value != null)
                {
                    // When a row is clicked, load that data into the entry form
                    MCustomer = value;
                }
            }
        }

        // 2. This is the object bound to your entry form (TextBoxes)
        private Customer _mCustomer;
        public Customer MCustomer
        {
            get => _mCustomer;
            set => SetProperty(ref _mCustomer, value);
        }

        public CustomerViewModel()
        {
            _customerService = new CustomerService();

            // Initialize instances to prevent NullReferenceExceptions
            MCustomer = new Customer();
            CustomerList = new ObservableCollection<Customer>();

            // Initialize Commands
            CustomerSaveCommand = new RelayCommand(_ => Save());
            CustomerDeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedCustomer != null);
            CustomerResetCommand = new RelayCommand(_ => Reset());

            LoadData();
        }

        public void LoadData()
        {
            // Ensure CustomerService has a method named GetAllCustomers
            var data = _customerService.GetAllCustomers();
            CustomerList = new ObservableCollection<Customer>(data);
        }

        private void Reset()
        {
            MCustomer = new Customer();
            SelectedCustomer = null;
        }

        private void Save()
        {
            // Validation: Ensure we are checking the instance (MCustomer), not the Class (Customer)
            if (string.IsNullOrWhiteSpace(MCustomer.CustomerName))
            {
                MessageBox.Show("Customer Name is required!");
                return;
            }

            bool success;
            if (MCustomer.Id <= 0)
            {
                success = _customerService.InsertCustomer(MCustomer);
            }
            else
            {
                success = _customerService.UpdateCustomer(MCustomer);
            }

            if (success)
            {
                LoadData();
                Reset();
            }
            else
            {
                MessageBox.Show("Database error: Could not save customer.");
            }
        }

        private void Delete()
        {
            if (SelectedCustomer == null) return;

            var result = MessageBox.Show($"Delete {SelectedCustomer.CustomerName}?",
                "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                if (_customerService.DeleteCustomer(SelectedCustomer.Id))
                {
                    LoadData();
                    Reset();
                }
            }
        }
    }
}