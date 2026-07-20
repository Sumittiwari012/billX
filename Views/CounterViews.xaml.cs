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
            if (DataContext is CounterViewModel vm)
            {
                vm.CurrentCounter.Password = PasswordInput.Password;
            }
        }
    }
}