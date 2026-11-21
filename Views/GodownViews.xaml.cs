using MyWPFCRUDApp.ViewModels;
using System.Windows.Controls;

namespace MyWPFCRUDApp.Views
{
    public partial class GodownViews : UserControl
    {
        public GodownViews()
        {
            InitializeComponent();
            DataContext = new GodownViewModel();
        }
    }
}
