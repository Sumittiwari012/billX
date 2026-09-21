using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using WPFCRUDApp.Models;

namespace MyWPFCRUDApp.Services
{
    public class ReturnableLine
    {
        public MPurchaseDetail Line { get; set; } = null!;
        public double Quantity { get; set; }          // still returnable on this line
    }

    public class BillMatchLine
    {
        public long ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? Barcode { get; set; }
        public string? Batch { get; set; }
        public DateTime? MfgDate { get; set; }
        public DateTime? ExpDate { get; set; }
        public decimal PurchasePrice { get; set; }
        public double Quantity { get; set; }          // units proposed for return
        public double MaxQuantity { get; set; }       // returnable cap on this line
    }

    public class BillCandidate
    {
        public MPurchaseMaster Invoice { get; set; } = null!;
        public long SupplierId { get; set; }
        public string SupplierName { get; set; } = "";
        public string InvoiceNumber { get; set; } = "";
        public DateTime? PurchaseDate { get; set; }
        public double ScannedUnits { get; set; }
        public List<BillMatchLine> Lines { get; } = new();

        public double MatchedUnits => Lines.Sum(l => l.Quantity);

        public int MatchedBarcodes =>
            Lines.Select(l => l.Barcode ?? "").Distinct(StringComparer.OrdinalIgnoreCase).Count();

        public decimal MatchedAmount =>
            Lines.Sum(l => Math.Round((decimal)l.Quantity * l.PurchasePrice, 2));

        public string MatchText => $"{MatchedUnits:0.##} of {ScannedUnits:0.##}";

        public string Summary => string.Join(", ",
            Lines.GroupBy(l => l.ProductName ?? l.Barcode ?? "?")
                 .Select(g => $"{g.Key} ×{g.Sum(x => x.Quantity):0.##}"));
    }

    public class MatchResult
    {
        public List<BillCandidate> Candidates { get; set; } = new();
        public Dictionary<string, double> Unmatched { get; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    public static class ReturnBillMatcher
    {
        private const double Eps = 1e-9;

        public static string ReturnedKey(long supplierId, string? invoiceNumber, long productId) =>
            $"{supplierId}|{(invoiceNumber ?? "").Trim().ToUpperInvariant()}|{productId}";

        /// Lines of one invoice with how much can still be returned on each.
        /// Earlier returns are assumed to have consumed the soonest-expiring lines first.
        public static List<ReturnableLine> GetReturnableLines(
            MPurchaseMaster invoice, IReadOnlyDictionary<string, double> returned)
        {
            var result = new List<ReturnableLine>();
            var details = invoice.Details ?? Enumerable.Empty<MPurchaseDetail>();

            foreach (var grp in details.GroupBy(d => d.ProductId))
            {
                returned.TryGetValue(
                    ReturnedKey(invoice.SupplierId, invoice.InvoiceNumber, grp.Key),
                    out double toConsume);

                foreach (var d in grp.OrderBy(d => d.ExpDate ?? DateTime.MaxValue))
                {
                    double used = Math.Min(d.Quantity, toConsume);
                    toConsume -= used;
                    double left = d.Quantity - used;
                    if (left > Eps)
                        result.Add(new ReturnableLine { Line = d, Quantity = left });
                }
            }
            return result;
        }

        /// pool: barcode -> units still to be returned.
        public static MatchResult Rank(
            IEnumerable<MPurchaseMaster> purchases,
            Dictionary<string, double> pool,
            IReadOnlyDictionary<string, double> returned,
            IReadOnlyDictionary<long, string> supplierNames)
        {
            var result = new MatchResult();
            double scanned = pool.Values.Sum();
            var capacity = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var inv in purchases)
            {
                var cand = new BillCandidate
                {
                    Invoice = inv,
                    SupplierId = inv.SupplierId,
                    SupplierName = supplierNames.TryGetValue(inv.SupplierId, out var sn)
                        ? sn : $"Supplier #{inv.SupplierId}",
                    InvoiceNumber = inv.InvoiceNumber ?? "",
                    PurchaseDate = inv.PurchaseDate,
                    ScannedUnits = scanned
                };

                // A bill can't take back more of a barcode than was scanned.
                var need = new Dictionary<string, double>(pool, StringComparer.OrdinalIgnoreCase);

                foreach (var rl in GetReturnableLines(inv, returned))
                {
                    var bc = rl.Line.Barcode?.Trim();
                    if (string.IsNullOrEmpty(bc) || !need.ContainsKey(bc)) continue;

                    capacity[bc] = (capacity.TryGetValue(bc, out var c) ? c : 0) + rl.Quantity;

                    double take = Math.Min(need[bc], rl.Quantity);
                    if (take <= Eps) continue;
                    need[bc] -= take;

                    cand.Lines.Add(new BillMatchLine
                    {
                        ProductId = rl.Line.ProductId,
                        ProductName = rl.Line.ProductName,
                        Barcode = bc,
                        Batch = rl.Line.Batch,
                        MfgDate = rl.Line.MfgDate,
                        ExpDate = rl.Line.ExpDate,
                        PurchasePrice = rl.Line.PurchasePrice,
                        Quantity = take,
                        MaxQuantity = rl.Quantity
                    });
                }

                if (cand.Lines.Count > 0) result.Candidates.Add(cand);
            }

            // Ranking rule: most units covered, then most distinct items,
            // then newest bill. Change the tie-breakers here if you prefer another rule.
            result.Candidates = result.Candidates
                .OrderByDescending(c => c.MatchedUnits)
                .ThenByDescending(c => c.MatchedBarcodes)
                .ThenByDescending(c => c.PurchaseDate ?? DateTime.MinValue)
                .ThenBy(c => c.InvoiceNumber)
                .ToList();

            foreach (var kv in pool)
            {
                capacity.TryGetValue(kv.Key, out double cap);
                if (kv.Value - cap > Eps)
                    result.Unmatched[kv.Key] = kv.Value - cap;
            }
            return result;
        }
    }
}