using MyWPFCRUDApp.ViewModels;
using System.Windows.Controls;

namespace MyWPFCRUDApp.Views
{
    /// <summary>
    /// Interaction logic for CounterViews.xaml
    /// </summary>
    public partial class CounterViews : UserControl
    {
        public CounterViews()
        {
            InitializeComponent();
            this.DataContext = new CounterViewModel();
        }

        private void PasswordInput_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is CounterViewModel vm && vm.CurrentCounterUser != null)
            {
                vm.CurrentCounterUser.Password = PasswordInput.Password;
            }
        }

        // PasswordBox.Password can't be data-bound directly (by design, for
        // security), so when a row is picked from the grid we push its
        // password into the box manually to support editing.
        private void CounterUsersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is CounterViewModel vm && vm.SelectedCounterUser != null)
            {
                PasswordInput.Password = vm.SelectedCounterUser.Password;
            }
            else
            {
                PasswordInput.Clear();
            }
        }
    }
}
