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
    public class UserTypeViewModel : BaseViewModel
    {
        public ICommand UserTypeSaveCommand { get; }
        public ICommand UserTypeDeleteCommand { get; }
        public ICommand UserTypeResetCommand { get; }

        private readonly UserService _userService;

        private ObservableCollection<MUserType> _userType;
        public ObservableCollection<MUserType> UserType
        {
            get => _userType;
            set => SetProperty(ref _userType, value);
        }

        private MUserType _selectedUserType;
        public MUserType SelectedUserType
        {
            get => _selectedUserType;
            set
            {
                if (SetProperty(ref _selectedUserType, value) && value != null)
                {
                    MUserType = value;
                }
            }
        }

        private MUserType _muserType;
        public MUserType MUserType
        {
            get => _muserType;
            set => SetProperty(ref _muserType, value);
        }

        public void LoadData()
        {
            var userTypeData = _userService.GetUserType();
            UserType = new ObservableCollection<MUserType>(userTypeData);
        }

        private void Reset()
        {
            MUserType = new MUserType();
            SelectedUserType = null;
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(MUserType.UserTypeName))
            {
                System.Windows.MessageBox.Show("User Type Name is required!");
                return;
            }

            bool success;
            if (MUserType.Id <= 0)
                success = _userService.InsertUser(MUserType);
            else
                success = _userService.UpdateUserType(MUserType);

            if (success)
            {
                LoadData();
                Reset();
            }
        }

        private void Delete()
        {
            var result = System.Windows.MessageBox.Show("Are you sure you want to delete this user type?", "Confirm Delete", System.Windows.MessageBoxButton.YesNo);
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                if (_userService.DeleteUserType((long)SelectedUserType.Id))
                {
                    LoadData();
                    Reset();
                }
            }
        }

        public UserTypeViewModel()
        {
            MUserType = new MUserType();
            _userService = new UserService();
            UserTypeSaveCommand = new RelayCommand(_ => Save());
            UserTypeDeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedUserType != null);
            UserTypeResetCommand = new RelayCommand(_ => Reset());
            LoadData();
        }
    }
}