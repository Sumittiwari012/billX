using System.Windows;

namespace WpfMySqlCrud
{
    public partial class SetupProgressWindow : Window
    {
        public SetupProgressWindow()
        {
            InitializeComponent();
        }

        public void Update(int percent, string message)
        {
            progressBar.Value = percent;
            txtPercent.Text = $"{percent}%";
            txtStatus.Text = message;
        }
    }
}