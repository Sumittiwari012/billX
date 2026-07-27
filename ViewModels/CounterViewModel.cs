using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace MyWPFCRUDApp.ViewModels
{
    // Master-detail: Counters (MCounterNew) on the master side, and the
    // users assigned to the selected counter (MCounterUser, each with
    // their own password) on the detail side.
    public class CounterViewModel : BaseViewModel
    {
        public ICommand CounterSaveCommand { get; }
        public ICommand CounterDeleteCommand { get; }
        public ICommand CounterResetCommand { get; }

        public ICommand CounterUserSaveCommand { get; }
        public ICommand CounterUserDeleteCommand { get; }
        public ICommand CounterUserResetCommand { get; }

        private readonly CounterService _counterService;
        private readonly CounterUserService _counterUserService;
        private readonly UserAccountService _userAccountService;

        // ---- Counters (master) ----
        private ObservableCollection<MCounterNew> _counters;
        public ObservableCollection<MCounterNew> Counters
        {
            get => _counters;
            set => SetProperty(ref _counters, value);
        }

        private MCounterNew _selectedCounter;
        public MCounterNew SelectedCounter
        {
            get => _selectedCounter;
            set
            {
                if (SetProperty(ref _selectedCounter, value))
                {
                    if (value != null)
                    {
                        CurrentCounter = new MCounterNew
                        {
                            Id = value.Id,
                            CounterName = value.CounterName
                        };
                        LoadCounterUsers(value.Id);
                    }
                    else
                    {
                        CurrentCounter = new MCounterNew();
                        CounterUsers = new ObservableCollection<MCounterUser>();
                    }
                    ResetCounterUserForm();
                    OnPropertyChanged(nameof(IsCounterSelected));
                }
            }
        }

        // Lets the XAML enable/disable the "Assign / Edit Counter User" panel
        // without needing a value converter.
        public bool IsCounterSelected => SelectedCounter != null;

        private MCounterNew _currentCounter;
        public MCounterNew CurrentCounter
        {
            get => _currentCounter;
            set => SetProperty(ref _currentCounter, value);
        }

        // ---- Users (dropdown source) ----
        private ObservableCollection<MUser> _users;
        public ObservableCollection<MUser> Users
        {
            get => _users;
            set => SetProperty(ref _users, value);
        }

        // ---- Counter Users (detail, for SelectedCounter) ----
        private ObservableCollection<MCounterUser> _counterUsers;
        public ObservableCollection<MCounterUser> CounterUsers
        {
            get => _counterUsers;
            set => SetProperty(ref _counterUsers, value);
        }

        private MCounterUser _selectedCounterUser;
        public MCounterUser SelectedCounterUser
        {
            get => _selectedCounterUser;
            set
            {
                if (SetProperty(ref _selectedCounterUser, value) && value != null)
                {
                    CurrentCounterUser = new MCounterUser
                    {
                        Id = value.Id,
                        CounterId = value.CounterId,
                        UserId = value.UserId,
                        Password = value.Password
                    };
                    SelectedUser = Users.FirstOrDefault(u => u.Id == value.UserId);
                }
            }
        }

        private MCounterUser _currentCounterUser;
        public MCounterUser CurrentCounterUser
        {
            get => _currentCounterUser;
            set => SetProperty(ref _currentCounterUser, value);
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
            Counters = new ObservableCollection<MCounterNew>(counterData);

            CounterUsers = new ObservableCollection<MCounterUser>();
        }

        private void LoadCounterUsers(long counterId)
        {
            var data = _counterUserService.GetCounterUsers(counterId);
            CounterUsers = new ObservableCollection<MCounterUser>(data);
        }

        // ---- Counter (master) CRUD ----
        private void ResetCounter()
        {
            CurrentCounter = new MCounterNew();
            SelectedCounter = null;
        }

        private void SaveCounter()
        {
            if (string.IsNullOrWhiteSpace(CurrentCounter.CounterName))
            {
                System.Windows.MessageBox.Show("Counter Name is required!");
                return;
            }

            bool success = CurrentCounter.Id <= 0
                ? _counterService.InsertCounter(CurrentCounter)
                : _counterService.UpdateCounter(CurrentCounter);

            if (success)
            {
                LoadData();
                ResetCounter();
            }
        }

        private void DeleteCounter()
        {
            if (SelectedCounter == null) return;

            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to delete this counter? All users assigned to it will lose access to it too.",
                "Confirm Delete", System.Windows.MessageBoxButton.YesNo);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                if (_counterService.DeleteCounter(SelectedCounter.Id))
                {
                    LoadData();
                    ResetCounter();
                }
            }
        }

        // ---- Counter User (detail) CRUD ----
        private void ResetCounterUserForm()
        {
            CurrentCounterUser = new MCounterUser { CounterId = SelectedCounter?.Id ?? 0 };
            SelectedCounterUser = null;
            SelectedUser = null;
        }

        private void SaveCounterUser()
        {
            if (SelectedCounter == null)
            {
                System.Windows.MessageBox.Show("Please select a Counter first!");
                return;
            }
            if (SelectedUser == null)
            {
                System.Windows.MessageBox.Show("Please select a User!");
                return;
            }
            if (string.IsNullOrWhiteSpace(CurrentCounterUser.Password))
            {
                System.Windows.MessageBox.Show("Password is required!");
                return;
            }

            CurrentCounterUser.CounterId = SelectedCounter.Id;
            CurrentCounterUser.UserId = SelectedUser.Id;

            if (_counterUserService.ExistsForCounterAndUser(
                    CurrentCounterUser.CounterId, CurrentCounterUser.UserId, CurrentCounterUser.Id))
            {
                System.Windows.MessageBox.Show("This user is already assigned to this counter!");
                return;
            }

            bool success = CurrentCounterUser.Id <= 0
                ? _counterUserService.InsertCounterUser(CurrentCounterUser)
                : _counterUserService.UpdateCounterUser(CurrentCounterUser);

            if (success)
            {
                LoadCounterUsers(SelectedCounter.Id);
                ResetCounterUserForm();
            }
        }

        private void DeleteCounterUser()
        {
            if (SelectedCounterUser == null) return;

            var result = System.Windows.MessageBox.Show(
                "Remove this user from the counter?", "Confirm Delete", System.Windows.MessageBoxButton.YesNo);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                if (_counterUserService.DeleteCounterUser(SelectedCounterUser.Id))
                {
                    LoadCounterUsers(SelectedCounter.Id);
                    ResetCounterUserForm();
                }
            }
        }

        public CounterViewModel()
        {
            _counterService = new CounterService();
            _counterUserService = new CounterUserService();
            _userAccountService = new UserAccountService();

            CurrentCounter = new MCounterNew();
            CurrentCounterUser = new MCounterUser();

            CounterSaveCommand = new RelayCommand(_ => SaveCounter());
            CounterDeleteCommand = new RelayCommand(_ => DeleteCounter(), _ => SelectedCounter != null);
            CounterResetCommand = new RelayCommand(_ => ResetCounter());

            CounterUserSaveCommand = new RelayCommand(_ => SaveCounterUser());
            CounterUserDeleteCommand = new RelayCommand(_ => DeleteCounterUser(), _ => SelectedCounterUser != null);
            CounterUserResetCommand = new RelayCommand(_ => ResetCounterUserForm());

            LoadData();
        }
    }
}