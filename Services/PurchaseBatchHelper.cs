using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace MyWPFCRUDApp.Services
{
    public class PurchaseBatch
    {
        public string? InvoiceNumber { get; set; }
        public string? SupplierName { get; set; }
        public decimal PurchasePrice { get; set; }
        public double Quantity { get; set; }

        // Optional per-batch details. Nullable so JSON saved before these
        // existed still deserializes (missing properties simply stay null).
        public string? Batch { get; set; }
        public DateTime? MfgDate { get; set; }
        public DateTime? ExpDate { get; set; }
    }

    public static class PurchaseBatchHelper
    {
        /// <summary>
        /// Adds (or updates) a purchase-batch entry in the given JSON array.
        ///
        /// Entries are matched by InvoiceNumber, not price: if this invoice
        /// already has an entry for this product (e.g. the invoice is being
        /// edited and re-saved), that entry's price/quantity is replaced
        /// rather than a duplicate being appended or it merging into some
        /// other invoice's entry that happened to share the same price.
        /// A brand-new invoice number always gets its own new entry.
        ///
        /// Passing a null/blank json (nothing recorded yet for this barcode)
        /// simply starts a fresh single-entry array - this is the normal path
        /// for a product's very first purchase, including older/existing
        /// products that never had a PurchaseQuantity value before.
        /// </summary>
        /// <param name="existingQuantity">
        /// The barcode's ProductQuantity.Quantity value as it stood BEFORE this
        /// purchase is applied. Only used when the batch array is still empty
        /// (i.e. this is the first time a batch entry is ever being recorded
        /// for this barcode): if there was already stock on hand at that
        /// point, a baseline entry is seeded for it first - using the same
        /// purchase price passed in, since no earlier price is on record -
        /// with no InvoiceNumber/SupplierName, since it predates invoice-level
        /// tracking. This keeps that pre-existing stock visible in the batch
        /// history instead of it silently vanishing the first time batches
        /// start being recorded. Pass 0 (default) if there's nothing to seed.
        /// </param>
        /// <param name="batch">Batch number for this purchase (optional).</param>
        /// <param name="mfgDate">Manufacturing date for this purchase (optional).</param>
        /// <param name="expDate">Expiry date for this purchase (optional).</param>
        public static string AddPurchase(
            string? json, decimal price, double quantity,
            string? invoiceNumber, string? supplierName,
            double existingQuantity = 0,
            string? batch = null,
            DateTime? mfgDate = null,
            DateTime? expDate = null)
        {
            var batches = string.IsNullOrWhiteSpace(json)
                ? new List<PurchaseBatch>()
                : JsonSerializer.Deserialize<List<PurchaseBatch>>(json) ?? new List<PurchaseBatch>();

            price = Math.Round(price, 2);

            if (batches.Count == 0 && existingQuantity > 0)
            {
                batches.Add(new PurchaseBatch
                {
                    InvoiceNumber = null,
                    SupplierName = null,
                    PurchasePrice = price,
                    Quantity = existingQuantity
                });
            }

            PurchaseBatch? match = null;
            if (!string.IsNullOrWhiteSpace(invoiceNumber))
            {
                match = batches.FirstOrDefault(b =>
                    string.Equals(b.InvoiceNumber, invoiceNumber, StringComparison.OrdinalIgnoreCase));
            }

            if (match != null)
            {
                // Same invoice, saved again (edit) - replace this invoice's
                // figures for this product rather than duplicating/merging.
                match.PurchasePrice = price;
                match.Quantity = quantity;
                match.SupplierName = supplierName;

                // Only overwrite the optional details when a value was actually
                // supplied, so re-saving without them doesn't wipe what's there.
                if (!string.IsNullOrWhiteSpace(batch)) match.Batch = batch;
                if (mfgDate.HasValue) match.MfgDate = mfgDate;
                if (expDate.HasValue) match.ExpDate = expDate;
            }
            else
            {
                batches.Add(new PurchaseBatch
                {
                    InvoiceNumber = invoiceNumber,
                    SupplierName = supplierName,
                    PurchasePrice = price,
                    Quantity = quantity,
                    Batch = string.IsNullOrWhiteSpace(batch) ? null : batch,
                    MfgDate = mfgDate,
                    ExpDate = expDate
                });
            }

            return JsonSerializer.Serialize(batches);
        }

        /// <summary>
        /// Removes the batch entry for a given invoice (used when a purchase
        /// invoice referencing this product is deleted), so the JSON doesn't
        /// keep a stale entry for an invoice that no longer exists.
        /// Returns the updated JSON, or null if the array ends up empty.
        /// </summary>
        public static string? RemoveByInvoice(string? json, string invoiceNumber)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(invoiceNumber))
                return json;

            var batches = JsonSerializer.Deserialize<List<PurchaseBatch>>(json) ?? new List<PurchaseBatch>();
            batches.RemoveAll(b => string.Equals(b.InvoiceNumber, invoiceNumber, StringComparison.OrdinalIgnoreCase));

            return batches.Count == 0 ? null : JsonSerializer.Serialize(batches);
        }
    }
}