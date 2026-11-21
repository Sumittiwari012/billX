using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace MyWPFCRUDApp.ViewModels
{
    public class UserAccountViewModel : BaseViewModel
    {
        public ICommand UserSaveCommand { get; }
        public ICommand UserDeleteCommand { get; }
        public ICommand UserResetCommand { get; }

        private readonly UserAccountService _userAccountService;
        private readonly UserService _userTypeService;

        private ObservableCollection<MUser> _users;
        public ObservableCollection<MUser> Users
        {
            get => _users;
            set => SetProperty(ref _users, value);
        }

        private ObservableCollection<MUserType> _userTypes;
        public ObservableCollection<MUserType> UserTypes
        {
            get => _userTypes;
            set => SetProperty(ref _userTypes, value);
        }

        private ObservableCollection<Store> _stores;
        public ObservableCollection<Store> Stores
        {
            get => _stores;
            set => SetProperty(ref _stores, value);
        }

        private MUser _selectedUser;
        public MUser SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (SetProperty(ref _selectedUser, value) && value != null)
                {
                    CurrentUser = new MUser
                    {
                        Id = value.Id,
                        UserName = value.UserName,
                        UserTypeId = value.UserTypeId,
                        StoreId = value.StoreId,
                        MobileNumber = value.MobileNumber
                    };
                    SelectedUserType = UserTypes?.FirstOrDefault(t => t.Id == value.UserTypeId);
                    SelectedStore = Stores?.FirstOrDefault(s => s.Id == value.StoreId);
                }
            }
        }

        private MUser _currentUser;
        public MUser CurrentUser
        {
            get => _currentUser;
            set => SetProperty(ref _currentUser, value);
        }

        private MUserType _selectedUserType;
        public MUserType SelectedUserType
        {
            get => _selectedUserType;
            set => SetProperty(ref _selectedUserType, value);
        }

        private Store _selectedStore;
        public Store SelectedStore
        {
            get => _selectedStore;
            set => SetProperty(ref _selectedStore, value);
        }

        public void LoadData()
        {
            // Load lookups first so SelectedUser can resolve its type/store
            UserTypes = new ObservableCollection<MUserType>(_userTypeService.GetUserType());
            Stores = new ObservableCollection<Store>(_userAccountService.GetStores());
            Users = new ObservableCollection<MUser>(_userAccountService.GetUsers());
        }

        private void Reset()
        {
            CurrentUser = new MUser();
            SelectedUser = null;
            SelectedUserType = null;
            SelectedStore = null;
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(CurrentUser.UserName))
            {
                System.Windows.MessageBox.Show("User Name is required!");
                return;
            }
            if (SelectedStore == null)
            {
                System.Windows.MessageBox.Show("Please select a Store!");
                return;
            }
            if (SelectedUserType == null)
            {
                System.Windows.MessageBox.Show("Please select a User Type!");
                return;
            }
            if (CurrentUser.MobileNumber <= 0)
            {
                System.Windows.MessageBox.Show("Please enter a valid Mobile Number!");
                return;
            }

            CurrentUser.UserTypeId = SelectedUserType.Id;
            CurrentUser.StoreId = SelectedStore.Id;

            bool success;
            try
            {
                success = CurrentUser.Id <= 0
                    ? _userAccountService.InsertUser(CurrentUser)
                    : _userAccountService.UpdateUser(CurrentUser);
            }
            catch (MySql.Data.MySqlClient.MySqlException ex) when (ex.Number == 1062)
            {
                System.Windows.MessageBox.Show("This user already exists in the selected store.");
                return;
            }

            if (success)
            {
                LoadData();
                Reset();
            }
        }

        private void Delete()
        {
            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to delete this user?", "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo);
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                if (_userAccountService.DeleteUser(SelectedUser.Id))
                {
                    LoadData();
                    Reset();
                }
            }
        }

        public UserAccountViewModel()
        {
            CurrentUser = new MUser();
            _userAccountService = new UserAccountService();
            _userTypeService = new UserService();

            UserSaveCommand = new RelayCommand(_ => Save());
            UserDeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedUser != null);
            UserResetCommand = new RelayCommand(_ => Reset());

            LoadData();
        }
    }
}