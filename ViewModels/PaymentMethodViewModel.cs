using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace MyWPFCRUDApp.ViewModels
{
    public class PaymentMethodViewModel : BaseViewModel
    {
        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ResetCommand { get; }

        private readonly PaymentMethodService _service;

        private ObservableCollection<MPaymentMethod> _paymentMethods;
        public ObservableCollection<MPaymentMethod> PaymentMethods
        {
            get => _paymentMethods;
            set => SetProperty(ref _paymentMethods, value);
        }

        private MPaymentMethod _selectedMethod;
        public MPaymentMethod SelectedMethod
        {
            get => _selectedMethod;
            set
            {
                if (SetProperty(ref _selectedMethod, value) && value != null)
                    MMethod = value;
            }
        }

        private MPaymentMethod _mMethod;
        public MPaymentMethod MMethod
        {
            get => _mMethod;
            set => SetProperty(ref _mMethod, value);
        }

        public void LoadData()
        {
            PaymentMethods = new ObservableCollection<MPaymentMethod>(_service.GetAll());
        }

        private void Reset()
        {
            MMethod = new MPaymentMethod();
            SelectedMethod = null;
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(MMethod.PaymentMethod))
            {
                System.Windows.MessageBox.Show("Payment Method is required!");
                return;
            }

            bool success = MMethod.Id <= 0
                ? _service.Insert(MMethod)
                : _service.Update(MMethod);

            if (success) { LoadData(); Reset(); }
        }

        private void Delete()
        {
            if (SelectedMethod == null) return;

            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to delete this payment method?",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo);

            if (result == System.Windows.MessageBoxResult.Yes)
                if (_service.Delete(SelectedMethod.Id))
                { LoadData(); Reset(); }
        }

        public PaymentMethodViewModel()
        {
            MMethod = new MPaymentMethod();
            _service = new PaymentMethodService();

            SaveCommand = new RelayCommand(_ => Save());
            DeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedMethod != null);
            ResetCommand = new RelayCommand(_ => Reset());

            LoadData();
        }
    }
}