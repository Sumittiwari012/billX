using MySql.Data.MySqlClient;
using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MyWPFCRUDApp.Services
{
    /// <summary>
    /// Transfer lifecycle: Draft -> Dispatched -> Received | PartiallyReceived -> Closed
    ///                    Draft -> Cancelled
    ///                    Dispatched -> Rejected | Cancelled (stock goes back to source)
    /// Stock only moves at Dispatch (source decreases), Receive (destination increases) and the
    /// return paths. Every movement writes a ledger row in the same transaction.
    /// </summary>
    public static class StockTransferService
    {
        /// <summary>The person who dispatched cannot confirm receipt. Set false for single-user setups.</summary>
        public static bool RequireDifferentReceiver = true;
        public const int MaxLinesPerTransfer = 1000;

        const string HeaderCols = "t.Id, t.TransferNo, t.FromType, t.FromId, t.ToType, t.ToId, t.Status, t.Remarks, t.CreatedBy, t.CreatedDate, t.DispatchedBy, t.DispatchedAt, t.ReceivedBy, t.ReceivedAt";

        const string NameCols = @",
            CASE t.FromType WHEN 'Godown' THEN (SELECT GodownName FROM MGodown WHERE Id = t.FromId)
                            ELSE (SELECT StoreName FROM MStore WHERE Id = t.FromId) END AS FromName,
            CASE t.ToType   WHEN 'Godown' THEN (SELECT GodownName FROM MGodown WHERE Id = t.ToId)
                            ELSE (SELECT StoreName FROM MStore WHERE Id = t.ToId) END AS ToName,
            (SELECT COUNT(*) FROM StockTransferItem i WHERE i.TransferId = t.Id) AS ItemCount";

        // ---------------- Reads ----------------
        public static List<StockTransfer> GetTransfers(string status, string search)
        {
            status = (status == null || status == "All") ? "" : status;
            search = search?.Trim() ?? "";
            return Db.Query($@"SELECT {HeaderCols}{NameCols} FROM StockTransfer t
                               WHERE (@st = '' OR t.Status = @st) AND (@s = '' OR t.TransferNo LIKE @like)
                               ORDER BY t.Id DESC LIMIT 500",
                r => ReadHeader(r, true), ("@st", status), ("@s", search), ("@like", "%" + search + "%"));
        }

        public static StockTransfer GetTransfer(long id) =>
            Db.Query($"SELECT {HeaderCols}{NameCols} FROM StockTransfer t WHERE t.Id = @id",
                r => ReadHeader(r, true), ("@id", id)).FirstOrDefault();

        public static List<StockTransferItem> GetItems(long transferId) =>
            Db.Query(@"SELECT i.Id, i.TransferId, i.ProductId, i.Barcode, p.ProductName, i.DispatchedQty, i.ReceivedQty, i.Remarks,
                              COALESCE(CASE t.FromType
                                  WHEN 'Godown' THEN (SELECT Quantity FROM GodownStock WHERE GodownId = t.FromId AND Barcode = i.Barcode)
                                  ELSE (SELECT Quantity FROM StoreStock WHERE StoreId = t.FromId AND Barcode = i.Barcode) END, 0) AS SourceQty
                       FROM StockTransferItem i
                       JOIN StockTransfer t ON t.Id = i.TransferId
                       JOIN MProducts p ON p.Id = i.ProductId
                       WHERE i.TransferId = @id ORDER BY i.Id",
                r =>
                {
                    var it = ReadItem(r);
                    it.ProductName = Rd.Str(r, "ProductName");
                    it.SourceQty = Rd.Dec(r, "SourceQty");
                    return it;
                }, ("@id", transferId));

        // ---------------- Draft ----------------
        public static long SaveDraft(StockTransfer h, IList<StockTransferItem> items, string user)
        {
            if (h == null) throw new ArgumentNullException(nameof(h));
            if (h.FromType == h.ToType && h.FromId == h.ToId)
                throw new InvalidOperationException("Source and destination cannot be the same.");
            if (h.FromId <= 0 || h.ToId <= 0)
                throw new InvalidOperationException("Select both source and destination.");
            if (items == null || items.Count == 0)
                throw new InvalidOperationException("Add at least one barcode.");
            if (items.Count > MaxLinesPerTransfer)
                throw new InvalidOperationException($"A transfer can have at most {MaxLinesPerTransfer} lines. Split it into several transfers.");
            if (items.Any(i => i == null || i.DispatchedQty <= 0 || i.DispatchedQty != decimal.Truncate(i.DispatchedQty)))
                throw new InvalidOperationException("All quantities must be positive whole numbers.");
            if (items.Any(i => string.IsNullOrWhiteSpace(i.Barcode)))
                throw new InvalidOperationException("Every transfer line must have a barcode.");
            if (items.GroupBy(i => i.Barcode.Trim(), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                throw new InvalidOperationException("The same barcode appears twice.");

            return Db.InTransaction((conn, tx) =>
            {
                RequireLocation(conn, tx, h.FromType, h.FromId);
                RequireLocation(conn, tx, h.ToType, h.ToId);

                long id = h.Id;
                if (id == 0)
                {
                    using var ins = Db.Cmd(conn, tx,
                        @"INSERT INTO StockTransfer (TransferNo, FromType, FromId, ToType, ToId, Status, Remarks, CreatedBy, ModifiedBy)
                          VALUES (UUID(), @ft, @fi, @tt, @ti, 'Draft', @r, @u, @u)",
                        ("@ft", h.FromType), ("@fi", h.FromId), ("@tt", h.ToType), ("@ti", h.ToId), ("@r", h.Remarks), ("@u", user));
                    ins.ExecuteNonQuery();
                    id = ins.LastInsertedId;
                    Db.Cmd(conn, tx, "UPDATE StockTransfer SET TransferNo = CONCAT('TRF-', LPAD(Id, 6, '0')) WHERE Id = @id", ("@id", id)).ExecuteNonQuery();
                }
                else
                {
                    var cur = LoadHeader(conn, tx, id);
                    if (cur.Status != TransferStatus.Draft)
                        throw new InvalidOperationException("Only draft transfers can be edited.");

                    Db.Cmd(conn, tx,
                        "UPDATE StockTransfer SET FromType=@ft, FromId=@fi, ToType=@tt, ToId=@ti, Remarks=@r, ModifiedBy=@u WHERE Id=@id",
                        ("@ft", h.FromType), ("@fi", h.FromId), ("@tt", h.ToType), ("@ti", h.ToId), ("@r", h.Remarks), ("@u", user), ("@id", id)).ExecuteNonQuery();
                    Db.Cmd(conn, tx, "DELETE FROM StockTransferItem WHERE TransferId = @id", ("@id", id)).ExecuteNonQuery();
                }

                foreach (var it in items)
                {
                    var bc = it.Barcode.Trim();
                    long productId;
                    using (var p = Db.Cmd(conn, tx, "SELECT Id FROM MProducts WHERE Barcode = @bc", ("@bc", bc)))
                    {
                        var o = p.ExecuteScalar();
                        if (o == null || o == DBNull.Value)
                            throw new InvalidOperationException($"No product found with barcode '{bc}'.");
                        productId = Convert.ToInt64(o);
                    }

                    Db.Cmd(conn, tx,
                        "INSERT INTO StockTransferItem (TransferId, ProductId, Barcode, DispatchedQty, Remarks) VALUES (@t, @p, @bc, @q, @r)",
                        ("@t", id), ("@p", productId), ("@bc", bc), ("@q", it.DispatchedQty), ("@r", it.Remarks)).ExecuteNonQuery();
                }

                return id;
            });
        }

        public static void DeleteDraft(long id) => Db.InTransaction((conn, tx) =>
        {
            var h = LoadHeader(conn, tx, id);
            if (h.Status != TransferStatus.Draft)
                throw new InvalidOperationException("Only draft transfers can be deleted.");
            Db.Cmd(conn, tx, "DELETE FROM StockTransfer WHERE Id = @id", ("@id", id)).ExecuteNonQuery();
        });

        // ---------------- Dispatch ----------------
        public static void Dispatch(long id, string user) => Db.InTransaction((conn, tx) =>
        {
            var h = LoadHeader(conn, tx, id);
            if (h.Status != TransferStatus.Draft)
                throw new InvalidOperationException("Only a draft can be dispatched.");

            var items = LoadItems(conn, tx, id).OrderBy(i => i.Barcode, StringComparer.Ordinal).ToList();
            if (items.Count == 0)
                throw new InvalidOperationException("The transfer has no lines.");

            var m = StockService.Map(h.FromType);
            var problems = new List<string>();

            foreach (var i in items)
            {
                using var q = Db.Cmd(conn, tx,
                    $"SELECT Quantity FROM {m.Table} WHERE {m.Col} = @l AND Barcode = @bc FOR UPDATE",
                    ("@l", h.FromId), ("@bc", i.Barcode));

                var o = q.ExecuteScalar();
                var avail = o == null || o == DBNull.Value ? 0m : Convert.ToDecimal(o);

                // Draft transfers reserve stock logically. This draft's own reservation is
                // excluded here; reservations from every other Draft for this source are held.
                using var reservedCmd = Db.Cmd(conn, tx,
                    @"SELECT COALESCE(SUM(i.DispatchedQty), 0)
                      FROM StockTransferItem i
                      INNER JOIN StockTransfer t ON t.Id = i.TransferId
                      WHERE t.FromType = @ft
                        AND t.FromId = @fid
                        AND t.Status = 'Draft'
                        AND t.Id <> @currentId
                        AND i.Barcode = @bc",
                    ("@ft", h.FromType), ("@fid", h.FromId), ("@currentId", id), ("@bc", i.Barcode));

                var reserved = Convert.ToDecimal(reservedCmd.ExecuteScalar());
                var free = Math.Max(0m, avail - reserved);

                if (free < i.DispatchedQty)
                    problems.Add($"{i.Barcode}: available {avail:0.###}, reserved by other drafts {reserved:0.###}, free {free:0.###}, required {i.DispatchedQty:0.###}");
            }

            if (problems.Count > 0)
                throw new InvalidOperationException("Insufficient stock:\n" + string.Join("\n", problems));

            foreach (var i in items)
                StockService.Apply(conn, tx, h.FromType, h.FromId, i.ProductId, i.Barcode, -i.DispatchedQty,
                    MovementType.TransferOut, "Transfer", id, user, h.TransferNo);

            Db.Cmd(conn, tx,
                "UPDATE StockTransfer SET Status='Dispatched', DispatchedBy=@u, DispatchedAt=NOW(), ModifiedBy=@u WHERE Id=@id",
                ("@u", user), ("@id", id)).ExecuteNonQuery();
        });

        // ---------------- Receive ----------------
        /// <param name="lines">Received quantities keyed by transfer item Id. Missing/null quantities default to sent quantity.</param>
        public static void Receive(long id, IList<StockTransferItem> lines, string user) => Db.InTransaction((conn, tx) =>
        {
            var h = LoadHeader(conn, tx, id);
            if (h.Status != TransferStatus.Dispatched && h.Status != TransferStatus.PartiallyReceived)
                throw new InvalidOperationException("This transfer is not awaiting receipt.");

            if (RequireDifferentReceiver && string.Equals(h.DispatchedBy, user, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The person who dispatched the transfer cannot confirm receipt.");

            var isAdditionalReceipt = h.Status == TransferStatus.PartiallyReceived;
            var items = LoadItems(conn, tx, id).OrderBy(i => i.Barcode, StringComparer.Ordinal).ToList();
            var errors = new List<string>();
            var received = new Dictionary<long, (decimal Qty, decimal Delta, string Remarks)>();

            foreach (var i in items)
            {
                var line = lines?.FirstOrDefault(l => l != null && l.Id == i.Id);
                var previouslyReceived = i.ReceivedQty ?? 0m;

                // The UI submits cumulative received quantities. On an initial receipt, an omitted
                // line retains the legacy default of "all sent". On an additional receipt, an
                // omitted line keeps its already-posted cumulative quantity.
                var q = line?.ReceivedQty ?? (isAdditionalReceipt ? previouslyReceived : i.DispatchedQty);

                if (q < previouslyReceived || q > i.DispatchedQty)
                {
                    errors.Add($"{i.Barcode}: cumulative received must be between {previouslyReceived:0.###} and {i.DispatchedQty:0.###}");
                    continue;
                }

                if (q != decimal.Truncate(q))
                {
                    errors.Add($"{i.Barcode}: received quantity must be a whole number");
                    continue;
                }

                received[i.Id] = (q, q - previouslyReceived, line?.Remarks);
            }

            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join("\n", errors));

            if (isAdditionalReceipt && !received.Values.Any(x => x.Delta > 0))
                throw new InvalidOperationException("No additional received quantity was entered.");

            foreach (var i in items)
            {
                var (q, delta, rem) = received[i.Id];

                Db.Cmd(conn, tx,
                    "UPDATE StockTransferItem SET ReceivedQty=@q, Remarks=COALESCE(@r, Remarks) WHERE Id=@id",
                    ("@q", q), ("@r", rem), ("@id", i.Id)).ExecuteNonQuery();

                // Only post the newly received delta. Never add the cumulative quantity twice.
                if (delta > 0)
                    StockService.Apply(conn, tx, h.ToType, h.ToId, i.ProductId, i.Barcode, delta,
                        MovementType.TransferIn, "Transfer", id, user, h.TransferNo);
            }

            var full = items.All(i => received[i.Id].Qty == i.DispatchedQty);
            Db.Cmd(conn, tx,
                "UPDATE StockTransfer SET Status=@s, ReceivedBy=@u, ReceivedAt=NOW(), ModifiedBy=@u WHERE Id=@id",
                ("@s", full ? TransferStatus.Received : TransferStatus.PartiallyReceived), ("@u", user), ("@id", id)).ExecuteNonQuery();
        });

        // ---------------- Reject / Cancel ----------------
        public static void Reject(long id, string user, string reason) => Db.InTransaction((conn, tx) =>
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("A reason is required to reject a shipment.");

            var h = LoadHeader(conn, tx, id);
            if (h.Status != TransferStatus.Dispatched)
                throw new InvalidOperationException("Only a dispatched transfer can be rejected.");

            ReturnAllToSource(conn, tx, h, user, MovementType.RejectReturn);
            Db.Cmd(conn, tx, "UPDATE StockTransferItem SET ReceivedQty = 0 WHERE TransferId = @id", ("@id", id)).ExecuteNonQuery();
            Db.Cmd(conn, tx,
                "UPDATE StockTransfer SET Status='Rejected', ReceivedBy=@u, ReceivedAt=NOW(), Remarks=CONCAT(COALESCE(Remarks,''), ' | Rejected: ', @r), ModifiedBy=@u WHERE Id=@id",
                ("@u", user), ("@r", reason), ("@id", id)).ExecuteNonQuery();
        });

        public static void CancelDispatch(long id, string user) =>
            CancelTransfer(id, user);

        /// <summary>
        /// Controller-compatible cancellation:
        /// - Draft: mark Cancelled; no stock movement has happened.
        /// - Dispatched: return all dispatched stock to its source, then mark Cancelled.
        /// Received, PartiallyReceived, Closed and Rejected transfers cannot be cancelled.
        /// </summary>
        public static void CancelTransfer(long id, string user, string reason = null) => Db.InTransaction((conn, tx) =>
        {
            var h = LoadHeader(conn, tx, id);

            if (h.Status == TransferStatus.Draft)
            {
                Db.Cmd(conn, tx,
                    @"UPDATE StockTransfer
                      SET Status='Cancelled',
                          Remarks=CONCAT(COALESCE(Remarks,''), @note),
                          ModifiedBy=@u
                      WHERE Id=@id",
                    ("@note", string.IsNullOrWhiteSpace(reason) ? "" : $" | Cancelled: {reason.Trim()}"),
                    ("@u", user), ("@id", id)).ExecuteNonQuery();
                return;
            }

            if (h.Status != TransferStatus.Dispatched)
                throw new InvalidOperationException($"A {h.Status} transfer cannot be cancelled.");

            ReturnAllToSource(conn, tx, h, user, MovementType.CancelReturn);

            Db.Cmd(conn, tx,
                @"UPDATE StockTransfer
                  SET Status='Cancelled',
                      Remarks=CONCAT(COALESCE(Remarks,''), @note),
                      ModifiedBy=@u
                  WHERE Id=@id",
                ("@note", string.IsNullOrWhiteSpace(reason) ? "" : $" | Cancelled: {reason.Trim()}"),
                ("@u", user), ("@id", id)).ExecuteNonQuery();
        });

        // ---------------- Shortage handling ----------------
        /// <summary>Closes a partially received transfer: shortage goes back to source, or is written off.</summary>
        public static void ResolveShortage(long id, string user, bool returnToSource) => Db.InTransaction((conn, tx) =>
        {
            var h = LoadHeader(conn, tx, id);
            if (h.Status != TransferStatus.PartiallyReceived)
                throw new InvalidOperationException("This transfer has no open shortage.");

            if (returnToSource)
            {
                foreach (var i in LoadItems(conn, tx, id).OrderBy(i => i.Barcode, StringComparer.Ordinal))
                {
                    var shortage = i.DispatchedQty - (i.ReceivedQty ?? 0);
                    if (shortage > 0)
                        StockService.Apply(conn, tx, h.FromType, h.FromId, i.ProductId, i.Barcode, shortage,
                            MovementType.ShortageReturn, "Transfer", id, user, h.TransferNo);
                }
            }

            Db.Cmd(conn, tx,
                "UPDATE StockTransfer SET Status='Closed', Remarks=CONCAT(COALESCE(Remarks,''), @note), ModifiedBy=@u WHERE Id=@id",
                ("@note", returnToSource ? $" | Shortage returned to source by {user}" : $" | Shortage written off by {user}"),
                ("@u", user), ("@id", id)).ExecuteNonQuery();
        });

        // ---------------- Helpers ----------------
        static void ReturnAllToSource(MySqlConnection conn, MySqlTransaction tx, StockTransfer h, string user, string movement)
        {
            foreach (var i in LoadItems(conn, tx, h.Id).OrderBy(i => i.Barcode, StringComparer.Ordinal))
                StockService.Apply(conn, tx, h.FromType, h.FromId, i.ProductId, i.Barcode, i.DispatchedQty,
                    movement, "Transfer", h.Id, user, h.TransferNo);
        }

        static void RequireLocation(MySqlConnection conn, MySqlTransaction tx, string type, long id)
        {
            var m = StockService.Map(type);
            using var c = Db.Cmd(conn, tx, $"SELECT COUNT(*) FROM {m.LocTable} WHERE Id = @id", ("@id", id));
            if (Convert.ToInt64(c.ExecuteScalar()) == 0)
                throw new InvalidOperationException($"{type} #{id} does not exist.");
        }

        static StockTransfer LoadHeader(MySqlConnection conn, MySqlTransaction tx, long id)
        {
            using var cmd = Db.Cmd(conn, tx,
                $"SELECT {HeaderCols} FROM StockTransfer t WHERE t.Id = @id FOR UPDATE", ("@id", id));
            using var r = cmd.ExecuteReader();
            if (!r.Read()) throw new InvalidOperationException("Transfer not found.");
            return ReadHeader(r, false);
        }

        static List<StockTransferItem> LoadItems(MySqlConnection conn, MySqlTransaction tx, long id)
        {
            using var cmd = Db.Cmd(conn, tx,
                "SELECT Id, TransferId, ProductId, Barcode, DispatchedQty, ReceivedQty, Remarks FROM StockTransferItem WHERE TransferId = @id",
                ("@id", id));
            using var r = cmd.ExecuteReader();
            var list = new List<StockTransferItem>();
            while (r.Read()) list.Add(ReadItem(r));
            return list;
        }

        static StockTransfer ReadHeader(MySqlDataReader r, bool withNames)
        {
            var h = new StockTransfer
            {
                Id = Rd.Lng(r, "Id"),
                TransferNo = Rd.Str(r, "TransferNo"),
                FromType = Rd.Str(r, "FromType"),
                FromId = Rd.Lng(r, "FromId"),
                ToType = Rd.Str(r, "ToType"),
                ToId = Rd.Lng(r, "ToId"),
                Status = Rd.Str(r, "Status"),
                Remarks = Rd.Str(r, "Remarks"),
                CreatedBy = Rd.Str(r, "CreatedBy"),
                CreatedDate = Rd.Date(r, "CreatedDate"),
                DispatchedBy = Rd.Str(r, "DispatchedBy"),
                DispatchedAt = Rd.NDate(r, "DispatchedAt"),
                ReceivedBy = Rd.Str(r, "ReceivedBy"),
                ReceivedAt = Rd.NDate(r, "ReceivedAt")
            };

            if (withNames)
            {
                h.FromName = Rd.Str(r, "FromName");
                h.ToName = Rd.Str(r, "ToName");
                h.ItemCount = Convert.ToInt32(r["ItemCount"]);
            }

            return h;
        }

        static StockTransferItem ReadItem(MySqlDataReader r) => new StockTransferItem
        {
            Id = Rd.Lng(r, "Id"),
            TransferId = Rd.Lng(r, "TransferId"),
            ProductId = Rd.Lng(r, "ProductId"),
            Barcode = Rd.Str(r, "Barcode"),
            DispatchedQty = Rd.Dec(r, "DispatchedQty"),
            ReceivedQty = Rd.NDec(r, "ReceivedQty"),
            Remarks = Rd.Str(r, "Remarks")
        };
    }
}
