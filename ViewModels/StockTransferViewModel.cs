using MyWPFCRUDApp.Models;
using MyWPFCRUDApp.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace MyWPFCRUDApp.ViewModels
{
    public class StockTransferViewModel : NotifyBase
    {
        public string[] StatusFilters { get; } =
            { "All", TransferStatus.Draft, TransferStatus.Dispatched, TransferStatus.PartiallyReceived,
              TransferStatus.Received, TransferStatus.Closed, TransferStatus.Rejected, TransferStatus.Cancelled };
        public string[] LocationTypes { get; } = { LocationType.Godown, LocationType.Store };
        public string[] BillFormats { get; } = { DispatchBillFormat.Standard, DispatchBillFormat.Custom };

        public ObservableCollection<StockTransfer> Transfers { get; } = new ObservableCollection<StockTransfer>();
        public ObservableCollection<StockTransferItem> Items { get; } = new ObservableCollection<StockTransferItem>();
        public ObservableCollection<LocationOption> FromLocations { get; } = new ObservableCollection<LocationOption>();
        public ObservableCollection<LocationOption> ToLocations { get; } = new ObservableCollection<LocationOption>();

        StockTransfer _current = new StockTransfer();
        StockTransfer _selected;
        string _statusFilter = "All", _search = "";
        string _fromType = LocationType.Godown, _toType = LocationType.Store;
        string _billFormat = DispatchBillFormat.Standard;
        long? _fromId, _toId;
        string _remarks = "", _scanBarcode = "", _actionRemarks = "";
        decimal _scanQty = 1;

        // ----- list -----
        public string StatusFilter { get => _statusFilter; set { if (Set(ref _statusFilter, value)) Refresh(); } }
        public string Search { get => _search; set => Set(ref _search, value); }

        public StockTransfer Selected
        {
            get => _selected;
            set { if (Set(ref _selected, value) && value != null) LoadTransfer(value); }
        }

        // ----- editor -----
        public string FromType
        {
            get => _fromType;
            set { if (Set(ref _fromType, value)) { LoadLocations(FromLocations, value); FromId = null; } }
        }
        public long? FromId { get => _fromId; set => Set(ref _fromId, value); }

        public string ToType
        {
            get => _toType;
            set { if (Set(ref _toType, value)) { LoadLocations(ToLocations, value); ToId = null; } }
        }
        public long? ToId { get => _toId; set => Set(ref _toId, value); }

        public string Remarks { get => _remarks; set => Set(ref _remarks, value); }
        /// <summary>Standard (built-in, with terms) or Custom (your own template file).</summary>
        public string BillFormat { get => _billFormat; set => Set(ref _billFormat, value); }
        public string ScanBarcode { get => _scanBarcode; set => Set(ref _scanBarcode, value); }
        public decimal ScanQty { get => _scanQty; set => Set(ref _scanQty, value); }
        /// <summary>Reason used by Reject.</summary>
        public string ActionRemarks { get => _actionRemarks; set => Set(ref _actionRemarks, value); }

        // ----- state flags for the view -----
        public string Title => _current.Id == 0 ? "New transfer" : $"{_current.TransferNo}  —  {_current.Status}";
        public bool IsDraft => _current.Status == TransferStatus.Draft;
        public bool IsNotDraft => !IsDraft;
        public bool IsDispatched => _current.Status == TransferStatus.Dispatched;
        public bool IsPartial => _current.Status == TransferStatus.PartiallyReceived;
        public bool ShowScan => IsDraft || IsDispatched;
        public bool ReceiptGridReadOnly => !IsDispatched;
        public bool CanDeleteDraft => IsDraft && _current.Id != 0;
        public bool CanPrintBill => _current.Id != 0;

        void NotifyState()
        {
            foreach (var n in new[] { nameof(Title), nameof(IsDraft), nameof(IsNotDraft), nameof(IsDispatched),
                                      nameof(IsPartial), nameof(ShowScan), nameof(ReceiptGridReadOnly), nameof(CanDeleteDraft), nameof(CanPrintBill) })
                OnPropertyChanged(n);
        }

        // ----- commands -----
        public ICommand NewCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand AddItemCommand { get; }
        public ICommand RemoveItemCommand { get; }
        public ICommand SaveDraftCommand { get; }
        public ICommand DispatchCommand { get; }
        public ICommand DeleteDraftCommand { get; }
        public ICommand ReceiveAllCommand { get; }
        public ICommand ConfirmReceiptCommand { get; }
        public ICommand RejectCommand { get; }
        public ICommand CancelDispatchCommand { get; }
        public ICommand ResolveShortageCommand { get; }   // parameter: "return" or "writeoff"
        public ICommand PrintBillCommand { get; }
        public ICommand EditTermsCommand { get; }
        public ICommand EditTemplateCommand { get; }
        public ICommand SetSignatureCommand { get; }
        public ICommand RemoveSignatureCommand { get; }

        public StockTransferViewModel()
        {
            NewCommand = new DelegateCommand(NewTransfer);
            RefreshCommand = new DelegateCommand(Refresh);
            AddItemCommand = new DelegateCommand(AddItem);
            RemoveItemCommand = new DelegateCommand(p => { if (IsDraft && p is StockTransferItem i) Items.Remove(i); });
            SaveDraftCommand = new DelegateCommand(SaveDraft);
            DispatchCommand = new DelegateCommand(Dispatch);
            DeleteDraftCommand = new DelegateCommand(DeleteDraft);
            ReceiveAllCommand = new DelegateCommand(() => { foreach (var i in Items) i.ReceivedQty = i.DispatchedQty; });
            ConfirmReceiptCommand = new DelegateCommand(ConfirmReceipt);
            RejectCommand = new DelegateCommand(Reject);
            CancelDispatchCommand = new DelegateCommand(CancelDispatch);
            ResolveShortageCommand = new DelegateCommand(p => ResolveShortage(Convert.ToString(p) == "return"));
            PrintBillCommand = new DelegateCommand(PrintBill);
            EditTermsCommand = new DelegateCommand(EditTerms);
            EditTemplateCommand = new DelegateCommand(EditTemplate);
            SetSignatureCommand = new DelegateCommand(SetSignature);
            RemoveSignatureCommand = new DelegateCommand(RemoveSignature);

            LoadLocations(FromLocations, _fromType);
            LoadLocations(ToLocations, _toType);
            Refresh();
        }

        // ----- loading -----
        static void LoadLocations(ObservableCollection<LocationOption> target, string type)
        {
            target.Clear();
            foreach (var o in LocationService.GetOptions(type, true)) target.Add(o);
        }

        void Refresh()
        {
            try
            {
                Transfers.Clear();
                foreach (var t in StockTransferService.GetTransfers(_statusFilter, _search)) Transfers.Add(t);
            }
            catch (Exception ex) { Msg.Error(ex, "Transfers"); }
        }

        void NewTransfer()
        {
            _selected = null; OnPropertyChanged(nameof(Selected));
            _current = new StockTransfer();
            Items.Clear();
            _fromType = LocationType.Godown; _toType = LocationType.Store;
            OnPropertyChanged(nameof(FromType)); OnPropertyChanged(nameof(ToType));
            LoadLocations(FromLocations, _fromType); LoadLocations(ToLocations, _toType);
            FromId = null; ToId = null; Remarks = ""; ScanBarcode = ""; ScanQty = 1; ActionRemarks = "";
            NotifyState();
        }

        void LoadTransfer(StockTransfer t)
        {
            try
            {
                _current = t;
                // Setting the type reloads the location list and clears the id, so set the id afterwards.
                _fromType = t.FromType; LoadLocations(FromLocations, t.FromType); OnPropertyChanged(nameof(FromType));
                _toType = t.ToType; LoadLocations(ToLocations, t.ToType); OnPropertyChanged(nameof(ToType));
                // Inactive locations are not in the lists; make sure the saved one is still shown.
                EnsureOption(FromLocations, t.FromType, t.FromId, t.FromName);
                EnsureOption(ToLocations, t.ToType, t.ToId, t.ToName);
                FromId = t.FromId; ToId = t.ToId;
                Remarks = t.Remarks ?? ""; ActionRemarks = ""; ScanBarcode = ""; ScanQty = 1;

                Items.Clear();
                foreach (var i in StockTransferService.GetItems(t.Id)) Items.Add(i);
                NotifyState();
            }
            catch (Exception ex) { Msg.Error(ex, "Transfers"); }
        }

        static void EnsureOption(ObservableCollection<LocationOption> list, string type, long id, string name)
        {
            if (list.All(o => o.Id != id)) list.Add(new LocationOption { Id = id, Name = name, Type = type });
        }

        /// <summary>Reload a transfer after an action and keep it selected.</summary>
        void Open(long id)
        {
            Refresh();
            var t = StockTransferService.GetTransfer(id);
            _selected = Transfers.FirstOrDefault(x => x.Id == id);
            OnPropertyChanged(nameof(Selected));
            if (t != null) LoadTransfer(t);
        }

        // ----- scanning / items -----
        void AddItem()
        {
            try
            {
                var bc = _scanBarcode?.Trim();
                if (string.IsNullOrEmpty(bc)) return;
                if (_scanQty <= 0) throw new InvalidOperationException("Quantity must be greater than zero.");

                if (IsDispatched)
                {
                    // Receiving: each scan adds to the received quantity of that line.
                    var line = Items.FirstOrDefault(i => i.Barcode == bc);
                    if (line == null) throw new InvalidOperationException($"Barcode '{bc}' is not part of this transfer.");
                    var next = (line.ReceivedQty ?? 0) + _scanQty;
                    if (next > line.DispatchedQty) throw new InvalidOperationException($"Received would exceed dispatched ({line.DispatchedQty:0.###}).");
                    line.ReceivedQty = next;
                }
                else if (IsDraft)
                {
                    var prod = StockService.FindProduct(bc);
                    if (prod == null) throw new InvalidOperationException($"No product found with barcode '{bc}'.");
                    var existing = Items.FirstOrDefault(i => i.Barcode == bc);
                    if (existing != null) existing.DispatchedQty += _scanQty;
                    else Items.Add(new StockTransferItem
                    {
                        Barcode = bc,
                        ProductId = prod.Value.Id,
                        ProductName = prod.Value.Name,
                        DispatchedQty = _scanQty,
                        SourceQty = _fromId.HasValue ? StockService.GetQuantity(_fromType, _fromId.Value, bc) : 0
                    });
                }
                ScanBarcode = ""; ScanQty = 1;
            }
            catch (Exception ex) { Msg.Error(ex, "Transfers"); }
        }

        // ----- actions -----
        long SaveDraftInternal()
        {
            if (!_fromId.HasValue || !_toId.HasValue) throw new InvalidOperationException("Select both source and destination.");
            var header = new StockTransfer
            {
                Id = _current.Id,
                FromType = _fromType,
                FromId = _fromId.Value,
                ToType = _toType,
                ToId = _toId.Value,
                Remarks = _remarks
            };
            return StockTransferService.SaveDraft(header, Items.ToList(), StockContext.CurrentUser);
        }

        void SaveDraft()
        {
            try { Open(SaveDraftInternal()); Msg.Info("Draft saved. Stock is not affected until you dispatch."); }
            catch (Exception ex) { Msg.Error(ex, "Transfers"); }
        }

        void Dispatch()
        {
            try
            {
                if (!Msg.Confirm($"Dispatch {Items.Count} line(s)? Stock will be deducted from the source now.")) return;
                var id = SaveDraftInternal();
                StockTransferService.Dispatch(id, StockContext.CurrentUser);
                Open(id);

                // The goods travel with a dispatch bill - offer it right away.
                if (Msg.Confirm($"Dispatched. Open the dispatch bill ({_billFormat}) now?"))
                    OpenBill(id);
            }
            catch (Exception ex) { Msg.Error(ex, "Dispatch"); }
        }

        // ----- dispatch bill -----
        void OpenBill(long id)
        {
            try { DispatchBillService.OpenInBrowser(DispatchBillService.Generate(id, _billFormat)); }
            catch (Exception ex) { Msg.Error(ex, "Dispatch bill"); }
        }

        void PrintBill()
        {
            if (_current.Id == 0) { Msg.Error(new InvalidOperationException("Save the transfer first."), "Dispatch bill"); return; }
            OpenBill(_current.Id);
        }

        void EditTerms()
        {
            try { DispatchBillService.EditTerms(); }
            catch (Exception ex) { Msg.Error(ex, "Terms & conditions"); }
        }

        void SetSignature()
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select signature image",
                    Filter = "Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
                };
                if (dlg.ShowDialog() != true) return;
                DispatchBillService.SaveSignature(dlg.FileName);
                Msg.Info("Signature saved. It will appear on the Standard dispatch bill.");
            }
            catch (Exception ex) { Msg.Error(ex, "Signature"); }
        }

        void RemoveSignature()
        {
            try
            {
                if (!Msg.Confirm("Remove the saved signature from the dispatch bill?")) return;
                DispatchBillService.RemoveSignature();
            }
            catch (Exception ex) { Msg.Error(ex, "Signature"); }
        }

        void EditTemplate()
        {
            try { DispatchBillService.EditCustomTemplate(); }
            catch (Exception ex) { Msg.Error(ex, "Custom bill template"); }
        }

        void DeleteDraft()
        {
            try
            {
                if (_current.Id == 0 || !Msg.Confirm("Delete this draft?")) return;
                StockTransferService.DeleteDraft(_current.Id);
                NewTransfer();
                Refresh();
            }
            catch (Exception ex) { Msg.Error(ex, "Transfers"); }
        }

        void ConfirmReceipt()
        {
            try
            {
                var shortLines = Items.Count(i => i.Shortage > 0);
                var text = shortLines == 0 ? "Confirm receipt of all lines?" : $"{shortLines} line(s) are short. Confirm receipt anyway?";
                if (!Msg.Confirm(text)) return;
                StockTransferService.Receive(_current.Id, Items.ToList(), StockContext.CurrentUser);
                Open(_current.Id);
            }
            catch (Exception ex) { Msg.Error(ex, "Receive"); }
        }

        void Reject()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_actionRemarks)) throw new InvalidOperationException("Enter a reason in the remarks box first.");
                if (!Msg.Confirm("Reject the whole shipment? Stock returns to the source.")) return;
                StockTransferService.Reject(_current.Id, StockContext.CurrentUser, _actionRemarks);
                Open(_current.Id);
            }
            catch (Exception ex) { Msg.Error(ex, "Reject"); }
        }

        void CancelDispatch()
        {
            try
            {
                if (!Msg.Confirm("Cancel this dispatch? Stock returns to the source.")) return;
                StockTransferService.CancelDispatch(_current.Id, StockContext.CurrentUser);
                Open(_current.Id);
            }
            catch (Exception ex) { Msg.Error(ex, "Cancel"); }
        }

        void ResolveShortage(bool returnToSource)
        {
            try
            {
                var text = returnToSource ? "Return the missing quantity to the source?" : "Write off the missing quantity?";
                if (!Msg.Confirm(text)) return;
                StockTransferService.ResolveShortage(_current.Id, StockContext.CurrentUser, returnToSource);
                Open(_current.Id);
            }
            catch (Exception ex) { Msg.Error(ex, "Shortage"); }
        }
    }
}