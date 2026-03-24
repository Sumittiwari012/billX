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
    public class UnitViewModel:BaseViewModel
    {
        public ICommand UnitSaveCommand { get; }
        public ICommand UnitDeleteCommand { get; }
        public ICommand UnitResetCommand { get; }
        private readonly UnitService _UnitService;
        private ObservableCollection<MUnit> _Unit;
        public ObservableCollection<MUnit> Unit
        {
            get => _Unit;
            set => SetProperty(ref _Unit, value);
        }
        private MUnit _selectedUnit;
        public MUnit SelectedUnit
        {
            get => _selectedUnit;
            set
            {
                if (SetProperty(ref _selectedUnit, value) && value != null)
                {
                    MUnit = value;
                }
            }
        }
        private MUnit _mUnit;
        public MUnit MUnit
        {
            get => _mUnit;
            set => SetProperty(ref _mUnit, value);
        }
        public void LoadData()
        {
            var UnitData = _UnitService.GetUnit();
            Unit = new ObservableCollection<MUnit>(UnitData);
        }
        private void Reset()
        {
            MUnit = new MUnit();
            SelectedUnit = null;
        }
        private void Save()
        {
            if (string.IsNullOrWhiteSpace(MUnit.UnitName))
            {
                System.Windows.MessageBox.Show("Unit Name is required!");
                return;
            }

            bool success;
            if (MUnit.Id <= 0)
                success = _UnitService.InsertUnit(MUnit);
            else
                success = _UnitService.UpdateUnit(MUnit); // Note: Your service currently names this UpdateStudent

            if (success)
            {
                LoadData();
                Reset();
            }
        }
        private void Delete()
        {
            var result = System.Windows.MessageBox.Show("Are you sure you want to delete this Unit?", "Confirm Delete", System.Windows.MessageBoxButton.YesNo);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                if (_UnitService.DeleteUnit((long)SelectedUnit.Id))
                {
                    LoadData();
                    Reset();
                }
            }
        }
        public UnitViewModel()
        {

            MUnit = new MUnit();
            _UnitService = new UnitService();
            UnitSaveCommand = new RelayCommand(_ => Save());
            UnitDeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedUnit != null);
            UnitResetCommand = new RelayCommand(_ => Reset());
            LoadData();

        }
    }
}
