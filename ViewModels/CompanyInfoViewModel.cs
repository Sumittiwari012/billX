using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MyWPFCRUDApp.ViewModels
{
    public class CompanyInfoViewModel: BaseViewModel
    {
        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ResetCommand { get; }
        private readonly CompanyService _companyInfoService;
        private ObservableCollection<MCompanyInfo> _companies;
        public ObservableCollection<MCompanyInfo> Companies
        {
            get => _companies;
            set => SetProperty(ref _companies, value);
        }
        private MCompanyInfo _selectedCompany;
        public MCompanyInfo SelectedCompany { 
            get => _selectedCompany;
            set
            {
                if (SetProperty(ref _selectedCompany, value) && value != null)
                {
                    CompanyInfo = value;
                }
            }
        }
        private MCompanyInfo _companyInfo;
        public MCompanyInfo CompanyInfo
        {
            get => _companyInfo;
            set => SetProperty(ref _companyInfo, value);
        }
        public void LoadData()
        {
            var companies = _companyInfoService.GetCompanyInfo();
            Companies = new ObservableCollection<MCompanyInfo>(companies);
        }
        private void Reset()
        {
            CompanyInfo = new MCompanyInfo();
            SelectedCompany = null;
        }
        private void Save()
        {
            if (string.IsNullOrWhiteSpace(CompanyInfo.CompanyName))
            {
                System.Windows.MessageBox.Show("Company Name is required!");
                return;
            }

            bool success;
            if (CompanyInfo.Id <= 0)
                success = _companyInfoService.InsertCompany(CompanyInfo);
            else
                success = _companyInfoService.UpdateCompanyInfo(CompanyInfo); // Note: Your service currently names this UpdateStudent

            if (success)
            {
                LoadData();
                Reset();
            }
        }
        private void Delete()
        {
            var result = System.Windows.MessageBox.Show("Are you sure you want to delete this company?", "Confirm Delete", System.Windows.MessageBoxButton.YesNo);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                if (_companyInfoService.DeleteCompany((long)SelectedCompany.Id))
                {
                    LoadData();
                    Reset();
                }
            }
        }
        public CompanyInfoViewModel()
        {
            
            CompanyInfo = new MCompanyInfo();
            _companyInfoService = new CompanyService();
            SaveCommand = new RelayCommand(_ => Save());
            DeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedCompany != null);
            ResetCommand = new RelayCommand(_ => Reset());
            LoadData();

        }
    }
}
