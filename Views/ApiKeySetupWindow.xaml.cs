using MyWPFCRUDApp.Services;
using System.Windows;
using System.Windows.Controls;

namespace MyWPFCRUDApp.Views
{
    public partial class ApiKeySetupWindow : Window
    {
        // True = key was saved successfully
        public bool KeySaved { get; private set; }

        public ApiKeySetupWindow()
        {
            InitializeComponent();

            // If a key is already saved, pre-fill masked and show Clear button
            string existing = ApiKeyManager.GetKey();
            if (!string.IsNullOrEmpty(existing))
            {
                PbKey.Password   = existing;
                BtnClear.Visibility = Visibility.Visible;
                TbStatus.Text    = "✔  A key is already saved. You can update or clear it.";
                TbStatus.Foreground = System.Windows.Media.Brushes.Green;
            }
        }

        // ── Show / Hide toggle ────────────────────────────────────────────────
        private void ChkShow_Checked(object sender, RoutedEventArgs e)
        {
            TbKeyVisible.Text      = PbKey.Password;
            TbKeyVisible.Visibility = Visibility.Visible;
            PbKey.Visibility        = Visibility.Collapsed;
        }

        private void ChkShow_Unchecked(object sender, RoutedEventArgs e)
        {
            PbKey.Password          = TbKeyVisible.Text;
            PbKey.Visibility        = Visibility.Visible;
            TbKeyVisible.Visibility = Visibility.Collapsed;
        }

        // Keep both boxes in sync
        private void PbKey_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (TbKeyVisible.Visibility == Visibility.Collapsed)
                return;
            TbKeyVisible.Text = PbKey.Password;
        }

        private void TbKeyVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (PbKey.Visibility == Visibility.Collapsed)
                return;
            PbKey.Password = TbKeyVisible.Text;
        }

        // ── Save ──────────────────────────────────────────────────────────────
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string key = ChkShow.IsChecked == true
                ? TbKeyVisible.Text.Trim()
                : PbKey.Password.Trim();

            // Basic format validation — Groq keys start with gsk_
            if (string.IsNullOrWhiteSpace(key))
            {
                ShowError("Please paste your Groq API key.");
                return;
            }

            if (!key.StartsWith("gsk_"))
            {
                ShowError("That doesn't look like a Groq key.\nGroq keys start with  gsk_  — please check and try again.");
                return;
            }

            if (key.Length < 40)
            {
                ShowError("Key looks too short. Please paste the full key.");
                return;
            }

            ApiKeyManager.SaveKey(key);
            KeySaved     = true;
            DialogResult = true;
            Close();
        }

        // ── Clear ─────────────────────────────────────────────────────────────
        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Clear the saved API key?\nYou will be asked to enter it again next time you scan a bill.",
                "Clear Key", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ApiKeyManager.ClearKey();
                PbKey.Password      = string.Empty;
                TbKeyVisible.Text   = string.Empty;
                BtnClear.Visibility = Visibility.Collapsed;
                ShowError("Key cleared.");
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ShowError(string msg)
        {
            TbStatus.Text       = msg;
            TbStatus.Foreground = msg.StartsWith("✔")
                ? System.Windows.Media.Brushes.Green
                : System.Windows.Media.Brushes.Red;
        }
    }
}
