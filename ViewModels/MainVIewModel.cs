using MyWPFCRUDApp.Helpers;
using MyWPFCRUDApp.Views;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace MyWPFCRUDApp.ViewModels
{
    public class SubOption
    {
        public string Name { get; set; }
        public string Section { get; set; }
        public string Icon { get; set; }
    }

    public class MainViewModel : BaseViewModel
    {
        private bool _sidebarOpen = false;

        private Visibility _sidebarTextVisibility = Visibility.Collapsed;
        public Visibility SidebarTextVisibility
        {
            get => _sidebarTextVisibility;
            set => SetProperty(ref _sidebarTextVisibility, value);
        }

        private GridLength _sidebarWidth = new GridLength(60);
        public GridLength SidebarWidth
        {
            get => _sidebarWidth;
            set => SetProperty(ref _sidebarWidth, value);
        }

        private string _currentSectionTitle = "Master Entry";
        public string CurrentSectionTitle
        {
            get => _currentSectionTitle;
            set => SetProperty(ref _currentSectionTitle, value);
        }

        private ObservableCollection<SubOption> _subOptions;
        public ObservableCollection<SubOption> SubOptions
        {
            get => _subOptions;
            set => SetProperty(ref _subOptions, value);
        }

        private object _currentView;
        public object CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public ICommand ToggleSidebarCommand { get; }
        public ICommand SelectSectionCommand { get; }
        public ICommand SelectOptionCommand { get; }

        // ── Icon Map ──────────────────────────────────────────────
        private readonly Dictionary<string, string> _iconMap = new()
        {
            // Master
            ["Company Info"] = "🏢",
            ["Category"] = "📁",
            ["Sub Category"] = "📂",
            ["Unit Master"] = "📏",
            ["Tax Category"] = "🪙",
            ["Products"] = "📦",
            ["Customer"] = "👥",
            ["Supplier"] = "🚚",
            ["Sales Person"] = "🧑‍💼",
            ["Account Head"] = "💼",
            // Transactions
            ["Receipt Entry"] = "🧾",
            ["Payment"] = "💳",
            ["Income Voucher"] = "💰",
            ["Expense Voucher"] = "💸",
            ["Contra Voucher"] = "🔁",
            ["General Voucher"] = "📝",
            ["Quotation"] = "📄",
            ["Transaction Status"] = "📊",
            ["Purchase Entry"] = "🛒",
            ["Purchase Order"] = "📋",
            // Inventory
            ["Stock Entry"] = "📥",
            ["Stock Adjustment"] = "⚖️",
            ["Branch Stock Inward"] = "📤",
            ["Branch Stock Outward"] = "📦",
            // Banking
            ["Bank Master"] = "🏦",
            ["Branch Master"] = "🏛️",
            ["Bank Account Registration"] = "📑",
            ["Fund Deposit"] = "💵",
            ["Fund Transfer"] = "🔄",
            ["Payment Withdrawal"] = "🏧",
            ["Account Statement"] = "📃",
            // Reports
            ["Supplier Ledger"] = "📒",
            ["Customer Ledger"] = "📓",
            ["Cash Ledger"] = "💹",
            ["Income Report"] = "📈",
            ["Expense Report"] = "📉",
            ["Sales Report"] = "🗃️",
            ["Purchase Report"] = "🗂️",
            ["Balance Sheet"] = "⚖️",
            ["Profit & Loss"] = "💡",
            ["Trial Balance"] = "🔢",
            ["Low Stock Item"] = "⚠️",
            // Settings
            ["User"] = "👤",
            ["Add Company"] = "🏗️",
            ["Terminal Settings"] = "💻",
            ["Sales Invoice Settings"] = "🧾",
            ["Auto Round Off"] = "🔃",
            ["WhatsApp"] = "💬",
            ["Printer Setting"] = "🖨️",
            ["Payment Setting"] = "💰",
            // Tax
            ["Tax Type"] = "🏷️",
            ["GSTR1"] = "📜",
            ["GSTR3B"] = "📜",
        };

        private readonly Dictionary<string, (string Title, string[] Options)> _menuData = new()
        {
            ["master"] = ("Master Entry", new[] { "Company Info", "Category", "Sub Category", "Unit Master", "Tax Category", "Products", "Customer", "Supplier", "Sales Person", "Account Head" }),
            ["transaction"] = ("Transactions", new[] { "Receipt Entry", "Payment", "Income Voucher", "Expense Voucher", "Contra Voucher", "General Voucher", "Quotation", "Transaction Status", "Purchase Entry", "Purchase Order" }),
            ["inventory"] = ("Inventory", new[] { "Stock Entry", "Stock Adjustment", "Branch Stock Inward", "Branch Stock Outward" }),
            ["banking"] = ("Banking", new[] { "Bank Master", "Branch Master", "Bank Account Registration", "Fund Deposit", "Fund Transfer", "Payment Withdrawal", "Account Statement" }),
            ["reports"] = ("Reports", new[] { "Supplier Ledger", "Customer Ledger", "Cash Ledger", "Income Report", "Expense Report", "Sales Report", "Purchase Report", "Balance Sheet", "Profit & Loss", "Trial Balance", "Low Stock Item" }),
            ["settings"] = ("Settings", new[] { "User", "Add Company", "Terminal Settings", "Sales Invoice Settings", "Auto Round Off", "WhatsApp", "Printer Setting", "Payment Setting" }),
            ["tax"] = ("Tax", new[] { "Tax Type", "GSTR1", "GSTR3B" })
        };

        public MainViewModel()
        {
            ToggleSidebarCommand = new RelayCommand(_ => ToggleSidebar());
            SelectSectionCommand = new RelayCommand(section => SelectSection(section?.ToString()));
            SelectOptionCommand = new RelayCommand(option => SelectOption(option as SubOption));
            SelectSection("master");
        }

        private void ToggleSidebar()
        {
            _sidebarOpen = !_sidebarOpen;
            SidebarTextVisibility = _sidebarOpen ? Visibility.Visible : Visibility.Collapsed;
            SidebarWidth = _sidebarOpen ? new GridLength(220) : new GridLength(60);
        }

        private void SelectSection(string sectionKey)
        {
            if (!_menuData.ContainsKey(sectionKey)) return;
            var (title, options) = _menuData[sectionKey];
            CurrentSectionTitle = title;
            SubOptions = new ObservableCollection<SubOption>(
                options.Select(o => new SubOption
                {
                    Name = o,
                    Section = sectionKey,
                    Icon = _iconMap.TryGetValue(o, out var ic) ? ic : "🔹"
                })
            );
            CurrentView = null;
        }

        private void SelectOption(SubOption option)
        {
            if (option == null) return;
            CurrentView = option.Name switch
            {
                "Category" => new CategoryControl(),
                _ => null
            };
        }
    }
}