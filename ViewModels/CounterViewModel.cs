using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace MyWPFCRUDApp.ViewModels
{
    public class CounterViewModel : BaseViewModel
    {
        public ICommand CounterSaveCommand { get; }
        public ICommand CounterDeleteCommand { get; }
        public ICommand CounterResetCommand { get; }

        private readonly CounterService _counterService;
        private readonly UserAccountService _userAccountService;
        private static readonly Random _random = new Random();

        private ObservableCollection<MCounter> _counters;
        public ObservableCollection<MCounter> Counters
        {
            get => _counters;
            set => SetProperty(ref _counters, value);
        }

        private ObservableCollection<MUser> _users;
        public ObservableCollection<MUser> Users
        {
            get => _users;
            set => SetProperty(ref _users, value);
        }

        private MCounter _selectedCounter;
        public MCounter SelectedCounter
        {
            get => _selectedCounter;
            set
            {
                if (SetProperty(ref _selectedCounter, value) && value != null)
                {
                    CurrentCounter = new MCounter
                    {
                        Id = value.Id,
                        CounterName = value.CounterName,
                        UserId = value.UserId,
                        Password = value.Password,
                        
                    };
                    SelectedUser = Users.FirstOrDefault(u => u.Id == value.UserId);
                }
            }
        }

        private MCounter _currentCounter;
        public MCounter CurrentCounter
        {
            get => _currentCounter;
            set => SetProperty(ref _currentCounter, value);
        }

        private MUser _selectedUser;
        public MUser SelectedUser
        {
            get => _selectedUser;
            set => SetProperty(ref _selectedUser, value);
        }

        public void LoadData()
        {
            var userData = _userAccountService.GetUsers();
            Users = new ObservableCollection<MUser>(userData);

            var counterData = _counterService.GetCounters();
            Counters = new ObservableCollection<MCounter>(counterData);
        }

        // Generates a random 10-digit numeric string, e.g. "4839201765".
        // Always exactly 10 digits — the leading digit is chosen from 1-9
        // so the result never starts with 0 and shrinks below 10 digits.
        

        private void Reset()
        {
            CurrentCounter = new MCounter();
            SelectedCounter = null;
            SelectedUser = null;
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(CurrentCounter.CounterName))
            {
                System.Windows.MessageBox.Show("Counter Name is required!");
                return;
            }
            if (SelectedUser == null)
            {
                System.Windows.MessageBox.Show("Please select a User!");
                return;
            }
            if (string.IsNullOrWhiteSpace(CurrentCounter.Password))
            {
                System.Windows.MessageBox.Show("Password is required!");
                return;
            }

            CurrentCounter.UserId = SelectedUser.Id;

            bool success;
            if (CurrentCounter.Id <= 0)
                success = _counterService.InsertCounter(CurrentCounter);
            else
                success = _counterService.UpdateCounter(CurrentCounter);

            if (success)
            {
                LoadData();
                Reset();
            }
        }

        private void Delete()
        {
            var result = System.Windows.MessageBox.Show("Are you sure you want to delete this counter?", "Confirm Delete", System.Windows.MessageBoxButton.YesNo);
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                if (_counterService.DeleteCounter(SelectedCounter.Id))
                {
                    LoadData();
                    Reset();
                }
            }
        }

        public CounterViewModel()
        {
            _counterService = new CounterService();
            _userAccountService = new UserAccountService();

            CurrentCounter = new MCounter();

            CounterSaveCommand = new RelayCommand(_ => Save());
            CounterDeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedCounter != null);
            CounterResetCommand = new RelayCommand(_ => Reset());

            LoadData();
        }
    }
}