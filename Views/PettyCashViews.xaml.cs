using MyWPFCRUDApp.ViewModels;
using System.Windows.Controls;

namespace MyWPFCRUDApp.Views
{
    public partial class PettyCashViews : UserControl
    {
        public PettyCashViews()
        {
            InitializeComponent();
            this.DataContext = new PettyCashViewModel();
        }
    }
}