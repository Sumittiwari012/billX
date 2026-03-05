using Google.Protobuf.WellKnownTypes;
using Mysqlx.Crud;
using MyWPFCRUDApp.ViewModels;
using Org.BouncyCastle.Math;
using System.ComponentModel;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace MyWPFCRUDApp.Views
{
    public partial class SetupProgressWindow : Window
    {
        private readonly SetupProgressViewModel _vm = new();

        public SetupProgressWindow()
        {
            InitializeComponent();
            DataContext = _vm;
        }

        public void Update(int pct, string msg)
            => _vm.Update(pct, msg);
    }
}
