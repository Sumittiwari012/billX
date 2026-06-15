using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WPFCRUDApp.Models;

namespace MyWPFCRUDApp.ViewModels
{
    public class PaymentWindowViewModel : BaseViewModel
    {
        // ── Services ──────────────────────────────────────────────────────────
        private readonly SupplierService _supplierService = new();
        private readonly PaymentMethodService _pmService = new();
        private readonly BankAccountService _bankService = new();
        private readonly PaymentService _paymentService = new();

        // ── Private state ─────────────────────────────────────────────────────
        private readonly MSupplier _supplier;
        private readonly MPurchaseMaster _master;
        private decimal _totalAlreadyPaid;

        // ── Commands ──────────────────────────────────────────────────────────
        public ICommand RecordPaymentCommand { get; }

        // ── Display-only (read-only, Mode=OneWay in XAML) ─────────────────────
        public string SupplierName => _supplier.SupplierName;
        public long SupplierId => _supplier.Id;
        public string InvoiceNumber => _master.InvoiceNumber;
        public decimal InvoiceTotal => _master.TotalAmount;

        // ── Payment Methods ───────────────────────────────────────────────────
        private ObservableCollection<MPaymentMethod> _paymentMethods;
        public ObservableCollection<MPaymentMethod> PaymentMethods
        {
            get => _paymentMethods;
            set => SetProperty(ref _paymentMethods, value);
        }

        private MPaymentMethod _selectedPaymentMethod;
        public MPaymentMethod SelectedPaymentMethod
        {
            get => _selectedPaymentMethod;
            set
            {
                if (SetProperty(ref _selectedPaymentMethod, value))
                    OnPropertyChanged(nameof(BankAccountVisibility));
            }
        }

        public Visibility BankAccountVisibility =>
            _selectedPaymentMethod?.PaymentMethod
                .Contains("Bank Transfer", StringComparison.OrdinalIgnoreCase) == true
            ? Visibility.Visible : Visibility.Collapsed;

        // ── Bank Accounts ─────────────────────────────────────────────────────
        private ObservableCollection<MBankAccountMaster> _bankAccounts;
        public ObservableCollection<MBankAccountMaster> BankAccounts
        {
            get => _bankAccounts;
            set => SetProperty(ref _bankAccounts, value);
        }

        private MBankAccountMaster _selectedBankAccount;
        public MBankAccountMaster SelectedBankAccount
        {
            get => _selectedBankAccount;
            set => SetProperty(ref _selectedBankAccount, value);
        }

        // ── Amount ────────────────────────────────────────────────────────────
        private decimal _amountPaid;
        public decimal AmountPaid
        {
            get => _amountPaid;
            set
            {
                if (SetProperty(ref _amountPaid, value))
                {
                    OnPropertyChanged(nameof(Balance));
                    OnPropertyChanged(nameof(BalanceBrush));
                }
            }
        }

        public decimal Balance => InvoiceTotal - _totalAlreadyPaid - AmountPaid;

        public Brush BalanceBrush => Balance > 0 ? Brushes.Red
                                   : Balance < 0 ? Brushes.Orange
                                   : Brushes.Green;

        // ── Payment History ───────────────────────────────────────────────────
        private ObservableCollection<PaymentHistoryRow> _paymentHistory;
        public ObservableCollection<PaymentHistoryRow> PaymentHistory
        {
            get => _paymentHistory;
            set
            {
                if (SetProperty(ref _paymentHistory, value))
                    OnPropertyChanged(nameof(EmptyHistoryVisibility));
            }
        }

        public Visibility EmptyHistoryVisibility =>
            PaymentHistory == null || PaymentHistory.Count == 0
            ? Visibility.Visible : Visibility.Collapsed;

        // ── Constructor ───────────────────────────────────────────────────────
        public PaymentWindowViewModel(MSupplier supplier, MPurchaseMaster master)
        {
            _supplier = supplier;
            _master = master;

            RecordPaymentCommand = new RelayCommand(_ => RecordPayment(), _ => CanRecordPayment());

            LoadDropdowns();
            LoadPaymentHistory();

            AmountPaid = Math.Max(0, InvoiceTotal - _totalAlreadyPaid);
        }

        // ── Private methods ───────────────────────────────────────────────────
        private void LoadDropdowns()
        {
            PaymentMethods = new ObservableCollection<MPaymentMethod>(_pmService.GetAll());
            SelectedPaymentMethod = PaymentMethods.FirstOrDefault();

            BankAccounts = new ObservableCollection<MBankAccountMaster>(_bankService.GetAccountNumber());
            SelectedBankAccount = BankAccounts.FirstOrDefault();
        }

        private void LoadPaymentHistory()
        {
            var records = _paymentService.GetByInvoice(_master.InvoiceNumber);

            PaymentHistory = new ObservableCollection<PaymentHistoryRow>(
                records.Select(r => new PaymentHistoryRow
                {
                    PaymentDate = r.createdDate,
                    PaymentMethod = r.PaymentMethod,
                    BankAccount = r.BankAccountNumber ?? "—",
                    Amount = r.AmountPaid
                }));

            _totalAlreadyPaid = records.Sum(r => r.AmountPaid);

            OnPropertyChanged(nameof(Balance));
            OnPropertyChanged(nameof(BalanceBrush));
            OnPropertyChanged(nameof(EmptyHistoryVisibility));
        }

        private bool CanRecordPayment()
        {
            if (AmountPaid <= 0) return false;
            if (BankAccountVisibility == Visibility.Visible && SelectedBankAccount == null) return false;
            return true;
        }

        private void RecordPayment()
        {
            if (AmountPaid <= 0)
            {
                System.Windows.MessageBox.Show("Please enter a valid amount.",
                    "Invalid Amount", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (BankAccountVisibility == Visibility.Visible && SelectedBankAccount == null)
            {
                System.Windows.MessageBox.Show("Please select a bank account for bank transfer.",
                    "Bank Account Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var payment = new MPayment
            {
                SupplierId = _supplier.Id,
                InvoiceNumber = _master.InvoiceNumber,
                PaymentMethod = SelectedPaymentMethod?.PaymentMethod ?? "",
                BankAccountNumber = BankAccountVisibility == Visibility.Visible
                                    ? SelectedBankAccount?.AccountNumber
                                    : null,
                AmountPaid = AmountPaid
            };

            if (!_paymentService.InsertPayment(payment))
            {
                System.Windows.MessageBox.Show("Failed to record payment.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Recalculate supplier balance from scratch
            _supplierService.RecalculateAndUpdateSupplierBalance(_supplier.Id);

            System.Windows.MessageBox.Show($"✔ Payment of ₹{AmountPaid:N2} recorded successfully.",
                "Payment Recorded", MessageBoxButton.OK, MessageBoxImage.Information);

            LoadPaymentHistory();
            AmountPaid = 0;
        }
    }

    public class PaymentHistoryRow
    {
        public DateTime PaymentDate { get; set; }
        public string PaymentMethod { get; set; }
        public string BankAccount { get; set; }
        public decimal Amount { get; set; }
    }
}