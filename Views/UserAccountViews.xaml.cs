using MyWPFCRUDApp.ViewModels;
using System.Windows.Controls;

namespace MyWPFCRUDApp.Views
{
    /// <summary>
    /// Interaction logic for UserAccountViews.xaml
    /// </summary>
    public partial class UserAccountViews : UserControl
    {
        public UserAccountViews()
        {
            InitializeComponent();
            this.DataContext = new UserAccountViewModel();
        }
        private void NumericOnly_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }
    }
}