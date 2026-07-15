using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyWPFCRUDApp.ViewModels;

namespace MyWPFCRUDApp
{
    
    public class SetupProgressViewModel : BaseViewModel
    {
        private int _progress;
        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        private string _message;
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        public void Update(int progress, string message)
        {
            Progress = progress;
            Message = message;
        }
    }
}
