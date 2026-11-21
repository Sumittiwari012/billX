using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using MySql.Data.MySqlClient;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace MyWPFCRUDApp.ViewModels
{
    public class GodownViewModel : BaseViewModel
    {
        public ObservableCollection<Godown> Godowns { get; } = new();

        private Godown? _selected;
        public Godown? SelectedGodown
        {
            get => _selected;
            set
            {
                if (SetProperty(ref _selected, value) && value != null)
                {
                    EditingId = value.Id;
                    GodownName = value.GodownName;
                    Address = value.Address ?? "";
                    IsActive = value.IsActive;
                }
            }
        }

        private long _editingId;
        public long EditingId { get => _editingId; set => SetProperty(ref _editingId, value); }

        private string _godownName = "";
        public string GodownName { get => _godownName; set => SetProperty(ref _godownName, value); }

        private string _address = "";
        public string Address { get => _address; set => SetProperty(ref _address, value); }

        private bool _isActive = true;
        public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }

        public ICommand SaveCommand { get; }
        public ICommand NewCommand { get; }
        public ICommand DeleteCommand { get; }

        public GodownViewModel()
        {
            SaveCommand = new RelayCommand(_ => Save());
            NewCommand = new RelayCommand(_ => ClearForm());
            DeleteCommand = new RelayCommand(_ => Delete());
            Load();
        }

        private void Load()
        {
            try
            {
                Godowns.Clear();
                foreach (var g in GodownService.GetAll()) Godowns.Add(g);
            }
            catch (Exception ex) { Error("Failed to load godowns", ex); }
        }

        private void ClearForm()
        {
            SelectedGodown = null;
            EditingId = 0;
            GodownName = "";
            Address = "";
            IsActive = true;
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(GodownName))
            {
                MessageBox.Show("Godown name is required.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var g = new Godown
            {
                Id = EditingId,
                GodownName = GodownName.Trim(),
                Address = string.IsNullOrWhiteSpace(Address) ? null : Address.Trim(),
                IsActive = IsActive
            };

            try
            {
                if (EditingId == 0) GodownService.Insert(g);
                else GodownService.Update(g);

                Load();
                ClearForm();
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                MessageBox.Show("A godown with this name already exists.", "Duplicate",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex) { Error("Failed to save godown", ex); }
        }

        private void Delete()
        {
            if (SelectedGodown == null)
            {
                MessageBox.Show("Select a godown to delete.", "Delete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var res = MessageBox.Show(
                $"Delete godown '{SelectedGodown.GodownName}'?\nStores linked to it will be unlinked.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            try
            {
                GodownService.Delete(SelectedGodown.Id);
                Load();
                ClearForm();
            }
            catch (Exception ex) { Error("Failed to delete godown", ex); }
        }

        private static void Error(string msg, Exception ex) =>
            MessageBox.Show($"{msg}:\n\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
