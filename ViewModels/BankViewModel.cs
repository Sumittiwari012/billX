using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace MyWPFCRUDApp.ViewModels
{
    public class BankViewModel : BaseViewModel
    {
        public ICommand BankSaveCommand { get; }
        public ICommand BankDeleteCommand { get; }
        public ICommand BankResetCommand { get; }

        private readonly BankAccountService _bankService;

        private ObservableCollection<MBankAccountMaster> _bankAccounts;
        public ObservableCollection<MBankAccountMaster> BankAccounts
        {
            get => _bankAccounts;
            set => SetProperty(ref _bankAccounts, value);
        }

        private MBankAccountMaster _selectedAccount;
        public MBankAccountMaster SelectedAccount
        {
            get => _selectedAccount;
            set
            {
                if (SetProperty(ref _selectedAccount, value) && value != null)
                    MBankAccount = value;
            }
        }

        private MBankAccountMaster _mBankAccount;
        public MBankAccountMaster MBankAccount
        {
            get => _mBankAccount;
            set => SetProperty(ref _mBankAccount, value);
        }

        public void LoadData()
        {
            BankAccounts = new ObservableCollection<MBankAccountMaster>(
                _bankService.GetAccountNumber());
        }

        private void Reset()
        {
            MBankAccount = new MBankAccountMaster();
            SelectedAccount = null;
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(MBankAccount.AccountNumber))
            {
                System.Windows.MessageBox.Show("Account Number is required!");
                return;
            }

            bool success;
            if (MBankAccount.Id <= 0)
                success = _bankService.InsertAccountNumber(MBankAccount);
            else
                success = _bankService.UpdateUnit(MBankAccount);

            if (success)
            {
                LoadData();
                Reset();
            }
        }

        private void Delete()
        {
            if (SelectedAccount == null) return;

            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to delete this bank account?",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                if (_bankService.DeleteUnit(SelectedAccount.Id))
                {
                    LoadData();
                    Reset();
                }
            }
        }

        public BankViewModel()
        {
            MBankAccount = new MBankAccountMaster();
            _bankService = new BankAccountService();

            BankSaveCommand = new RelayCommand(_ => Save());
            BankDeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedAccount != null);
            BankResetCommand = new RelayCommand(_ => Reset());

            LoadData();
        }
    }
}