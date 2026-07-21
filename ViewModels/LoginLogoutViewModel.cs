using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace MyWPFCRUDApp.ViewModels
{
    public class LoginLogoutViewModel : BaseViewModel
    {
        public ICommand LoginLogoutSaveCommand { get; }
        public ICommand LoginLogoutDeleteCommand { get; }
        public ICommand LoginLogoutResetCommand { get; }

        private readonly LoginLogoutService _loginLogoutService;

        private ObservableCollection<MLoginLogout> _loginLogouts;
        public ObservableCollection<MLoginLogout> LoginLogouts
        {
            get => _loginLogouts;
            set => SetProperty(ref _loginLogouts, value);
        }

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

        private MLoginLogout _selectedLoginLogout;
        public MLoginLogout SelectedLoginLogout
        {
            get => _selectedLoginLogout;
            set
            {
                if (SetProperty(ref _selectedLoginLogout, value) && value != null)
                {
                    CurrentLoginLogout = new MLoginLogout
                    {
                        Id = value.Id,
                        CounterId = value.CounterId,
                        UserId = value.UserId,
                        LoginTime = value.LoginTime,
                        LogoutTime = value.LogoutTime,
                        Settlement = value.Settlement
                    };
                    SelectedCounter = Counters.FirstOrDefault(c => c.Id == value.CounterId);
                    SelectedUser = Users.FirstOrDefault(u => u.Id == value.UserId);
                }
            }
        }

        private MLoginLogout _currentLoginLogout;
        public MLoginLogout CurrentLoginLogout
        {
            get => _currentLoginLogout;
            set => SetProperty(ref _currentLoginLogout, value);
        }

        private MCounter _selectedCounter;
        public MCounter SelectedCounter
        {
            get => _selectedCounter;
            set => SetProperty(ref _selectedCounter, value);
        }

        private MUser _selectedUser;
        public MUser SelectedUser
        {
            get => _selectedUser;
            set => SetProperty(ref _selectedUser, value);
        }

        public void LoadData()
        {
            Counters = new ObservableCollection<MCounter>(_loginLogoutService.GetCounters());
            Users = new ObservableCollection<MUser>(_loginLogoutService.GetUsers());
            LoginLogouts = new ObservableCollection<MLoginLogout>(_loginLogoutService.GetLoginLogouts());
        }

        private void Reset()
        {
            CurrentLoginLogout = new MLoginLogout { LoginTime = DateTime.Now };
            SelectedLoginLogout = null;
            SelectedCounter = null;
            SelectedUser = null;
        }

        private void Save()
        {
            if (SelectedCounter == null)
            {
                System.Windows.MessageBox.Show("Please select a Counter!");
                return;
            }
            if (SelectedUser == null)
            {
                System.Windows.MessageBox.Show("Please select a User!");
                return;
            }

            CurrentLoginLogout.CounterId = SelectedCounter.Id;
            CurrentLoginLogout.UserId = SelectedUser.Id;

            bool success;
            if (CurrentLoginLogout.Id <= 0)
            {
                if (CurrentLoginLogout.LoginTime == default)
                    CurrentLoginLogout.LoginTime = DateTime.Now;
                success = _loginLogoutService.InsertLoginLogout(CurrentLoginLogout);
            }
            else
                success = _loginLogoutService.UpdateLoginLogout(CurrentLoginLogout);

            if (success)
            {
                LoadData();
                Reset();
            }
        }

        private void Delete()
        {
            var result = System.Windows.MessageBox.Show("Are you sure you want to delete this record?", "Confirm Delete", System.Windows.MessageBoxButton.YesNo);
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                if (_loginLogoutService.DeleteLoginLogout(SelectedLoginLogout.Id))
                {
                    LoadData();
                    Reset();
                }
            }
        }

        public LoginLogoutViewModel()
        {
            CurrentLoginLogout = new MLoginLogout { LoginTime = DateTime.Now };
            _loginLogoutService = new LoginLogoutService();

            LoginLogoutSaveCommand = new RelayCommand(_ => Save());
            LoginLogoutDeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedLoginLogout != null);
            LoginLogoutResetCommand = new RelayCommand(_ => Reset());

            LoadData();
        }
    }
}