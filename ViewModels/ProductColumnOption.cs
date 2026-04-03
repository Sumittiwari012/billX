using MyWPFCRUDApp.Helpers;

namespace MyWPFCRUDApp.ViewModels
{
    public class ProductColumnOption : BaseViewModel
    {
        private bool _isVisible = true;

        public string Header { get; set; }
        public string Key { get; set; }

        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }
    }
}