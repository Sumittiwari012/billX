using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace MyWPFCRUDApp.ViewModels
{
    /// <summary>Set this from your login so ledger/transfer rows record who did what.</summary>
    public static class StockContext
    {
        public static string CurrentUser { get; set; } = "System";
    }

    public abstract class NotifyBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }

    // Swap for your own RelayCommand if you already have one.
    public class DelegateCommand : ICommand
    {
        readonly Action<object> _execute;
        readonly Predicate<object> _canExecute;

        public DelegateCommand(Action execute, Func<bool> canExecute = null)
            : this(_ => execute(), canExecute == null ? (Predicate<object>)null : _ => canExecute()) { }

        public DelegateCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object parameter) => _execute(parameter);
    }

    internal static class Msg
    {
        public static void Info(string text, string title = "Stock") =>
            MessageBox.Show(text, title, MessageBoxButton.OK, MessageBoxImage.Information);

        public static void Error(Exception ex, string title = "Stock") =>
            MessageBox.Show(ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

        public static bool Confirm(string text, string title = "Confirm") =>
            MessageBox.Show(text, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }
}
