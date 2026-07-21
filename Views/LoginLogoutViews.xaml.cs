using MyWPFCRUDApp.ViewModels;
using System.Windows.Controls;

namespace MyWPFCRUDApp.Views
{
    public partial class LoginLogoutViews : UserControl
    {
        public LoginLogoutViews()
        {
            InitializeComponent();
            this.DataContext = new LoginLogoutViewModel();
        }
    }
}