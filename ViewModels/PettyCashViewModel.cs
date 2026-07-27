using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace MyWPFCRUDApp.ViewModels
{
    public class PettyCashViewModel : BaseViewModel
    {
        public ICommand PettyCashSaveCommand { get; }
        public ICommand PettyCashDeleteCommand { get; }
        public ICommand PettyCashResetCommand { get; }
       

        private readonly PettyCashService _pettyCashService;
        private readonly CounterService _counterService;

        private ObservableCollection<MPettyCash> _pettyCashEntries;
        public ObservableCollection<MPettyCash> PettyCashEntries
        {
            get => _pettyCashEntries;
            set => SetProperty(ref _pettyCashEntries, value);
        }

        private ObservableCollection<MCounterNew> _counters;
        public ObservableCollection<MCounterNew> Counters
        {
            get => _counters;
            set => SetProperty(ref _counters, value);
        }

        private MPettyCash _selectedEntry;
        public MPettyCash SelectedEntry
        {
            get => _selectedEntry;
            set
            {
                if (SetProperty(ref _selectedEntry, value) && value != null)
                {
                    CurrentEntry = new MPettyCash
                    {
                        Id = value.Id,
                        PettyCash = value.PettyCash,
                        CounterId = value.CounterId,
                        Date = value.Date,
                        Accept = value.Accept
                    };
                    SelectedCounter = Counters.FirstOrDefault(c => c.Id == value.CounterId);
                }
            }
        }

        private MPettyCash _currentEntry;
        public MPettyCash CurrentEntry
        {
            get => _currentEntry;
            set => SetProperty(ref _currentEntry, value);
        }

        private MCounterNew _selectedCounter;
        public MCounterNew SelectedCounter
        {
            get => _selectedCounter;
            set => SetProperty(ref _selectedCounter, value);
        }

        public void LoadData()
        {
            var counterData = _counterService.GetCounters();
            Counters = new ObservableCollection<MCounterNew>(counterData);

            var entryData = _pettyCashService.GetPettyCash();
            PettyCashEntries = new ObservableCollection<MPettyCash>(entryData);
        }

        private void Reset()
        {
            CurrentEntry = new MPettyCash
            {
                Date = DateTime.Today,
                Accept = false
            };
            SelectedEntry = null;
            SelectedCounter = null;
        }

        private void Save()
        {
            if (SelectedCounter == null)
            {
                System.Windows.MessageBox.Show("Please select a Counter!");
                return;
            }
            if (CurrentEntry.Date == null)
            {
                System.Windows.MessageBox.Show("Please select a Date!");
                return;
            }
            if (CurrentEntry.PettyCash <= 0)
            {
                System.Windows.MessageBox.Show("Amount must be greater than zero!");
                return;
            }

            CurrentEntry.CounterId = SelectedCounter.Id;

            bool success;
            if (CurrentEntry.Id <= 0)
                success = _pettyCashService.InsertPettyCash(CurrentEntry);
            else
                success = _pettyCashService.UpdatePettyCash(CurrentEntry);

            if (success)
            {
                LoadData();
                Reset();
            }
        }

        private void Delete()
        {
            var result = System.Windows.MessageBox.Show("Are you sure you want to delete this entry?", "Confirm Delete", System.Windows.MessageBoxButton.YesNo);
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                if (_pettyCashService.DeletePettyCash(SelectedEntry.Id))
                {
                    LoadData();
                    Reset();
                }
            }
        }

        // Flips Accept for a specific row directly from the grid,
        // without needing to load it into the form first.
        

        public PettyCashViewModel()
        {
            _pettyCashService = new PettyCashService();
            _counterService = new CounterService();

            CurrentEntry = new MPettyCash
            {
                Date = DateTime.Today,
                Accept = false
            };

            PettyCashSaveCommand = new RelayCommand(_ => Save());
            PettyCashDeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedEntry != null);
            PettyCashResetCommand = new RelayCommand(_ => Reset());
            

            LoadData();
        }
    }
}
