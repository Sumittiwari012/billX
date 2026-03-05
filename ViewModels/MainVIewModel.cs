using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using MyWPFCRUDApp.ViewModels;
using MyWPFCRUDApp.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
namespace MyWPFCRUDApp
{
    
    public class MainViewModel : BaseViewModel
    {
        private object _currentView;
        public object CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }
        private readonly StudentService _studentService = new();
        public MainViewModel()
        {
            AddCommand = new RelayCommand(_ => Add());
            UpdateCommand = new RelayCommand(_ => Update(), _ => SelectedStudent != null);
            DeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedStudent != null);
            RefreshCommand = new RelayCommand(_ => { ClearFields(); LoadData(); });
            CategoryCommand = new RelayCommand(_ => CurrentView = new CategoryViewModel());
            LoadData();
        }

        // ── Properties ────────────────────────────────────────────────────────
        private ObservableCollection<Student> _students;
        public ObservableCollection<Student> Students
        {
            get => _students;
            set => SetProperty(ref _students, value);
        }

        private Student _selectedStudent;
        public Student SelectedStudent
        {
            get => _selectedStudent;
            set
            {
                if (SetProperty(ref _selectedStudent, value) && value != null)
                {
                    Name = value.Name;
                    Age = value.Age.ToString();
                    Email = value.Email;
                }
            }
        }

        private string _name;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _age;
        public string Age
        {
            get => _age;
            set => SetProperty(ref _age, value);
        }

        private string _email;
        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        // ── Commands ──────────────────────────────────────────────────────────
        public ICommand AddCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand CategoryCommand { get; }  // ← ADD THIS

        // ── Constructor ───────────────────────────────────────────────────────


        // ── Methods ───────────────────────────────────────────────────────────
        public void LoadData()
        {
            var list = _studentService.GetAllStudents();
            Students = new ObservableCollection<Student>(list);
        }

        private void ClearFields()
        {
            Name = string.Empty;
            Age = string.Empty;
            Email = string.Empty;
            SelectedStudent = null;
        }

        private bool TryGetStudent(out Student s)
        {
            s = new Student();
            if (string.IsNullOrWhiteSpace(Name) ||
                string.IsNullOrWhiteSpace(Age) ||
                string.IsNullOrWhiteSpace(Email))
            {
                MessageBox.Show("Please fill in all fields.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (!int.TryParse(Age, out int age) || age <= 0)
            {
                MessageBox.Show("Age must be a positive number.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            s = new Student
            {
                Id = SelectedStudent?.Id ?? -1,
                Name = Name.Trim(),
                Age = age,
                Email = Email.Trim()
            };
            return true;
        }

        private void Add()
        {
            if (!TryGetStudent(out var s)) return;
            if (_studentService.InsertStudent(s)) { ClearFields(); LoadData(); }
        }

        private void Update()
        {
            if (SelectedStudent == null)
            { MessageBox.Show("Select a row first."); return; }
            if (!TryGetStudent(out var s)) return;
            if (_studentService.UpdateStudent(s)) { ClearFields(); LoadData(); }
        }

        private void Delete()
        {
            if (SelectedStudent == null)
            { MessageBox.Show("Select a row first."); return; }
            var r = MessageBox.Show("Delete this student?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r == MessageBoxResult.Yes && _studentService.DeleteStudent(SelectedStudent.Id))
            { ClearFields(); LoadData(); }
        }
    }
}
