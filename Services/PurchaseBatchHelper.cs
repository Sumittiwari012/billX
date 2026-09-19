using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace MyWPFCRUDApp.Services
{
    public class PurchaseBatch
    {
        public decimal PurchasePrice { get; set; }
        public double Quantity { get; set; }
    }

    public static class PurchaseBatchHelper
    {
        // Same price -> add to that entry's quantity. New price -> new entry.
        public static string AddPurchase(string? json, decimal price, double quantity)
        {
            var batches = string.IsNullOrWhiteSpace(json)
                ? new List<PurchaseBatch>()
                : JsonSerializer.Deserialize<List<PurchaseBatch>>(json) ?? new List<PurchaseBatch>();

            price = Math.Round(price, 2);
            var match = batches.FirstOrDefault(b => Math.Round(b.PurchasePrice, 2) == price);

            if (match != null)
                match.Quantity += quantity;
            else
                batches.Add(new PurchaseBatch { PurchasePrice = price, Quantity = quantity });

            return JsonSerializer.Serialize(batches);
        }
    }
}