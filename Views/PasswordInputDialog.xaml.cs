using System.Windows;

namespace WpfMySqlCrud
{
    public partial class PasswordInputDialog : Window
    {
        public bool Confirmed { get; private set; }
        public string EnteredPassword { get; private set; } = string.Empty;

        public PasswordInputDialog()
        {
            InitializeComponent();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            string pwd = pwdBox.Password;
            string confirm = pwdConfirmBox.Password;

            if (pwd.Length < 4)
            {
                txtHint.Text = "⚠ Password must be at least 4 characters.";
                txtHint.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            if (pwd != confirm)
            {
                txtHint.Text = "⚠ Passwords do not match.";
                txtHint.Foreground = System.Windows.Media.Brushes.Red;
                pwdConfirmBox.Clear();
                return;
            }

            EnteredPassword = pwd;
            Confirmed = true;
            Close();
        }
    }
}