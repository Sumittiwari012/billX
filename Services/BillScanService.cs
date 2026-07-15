using MyWPFCRUDApp.Models;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MyWPFCRUDApp.Services
{
    /// <summary>
    /// Sends a purchase bill (image or PDF→image) to Groq's
    /// llama-3.2-11b-vision-preview model and returns a parsed ScannedBillResult.
    /// Key is read from ApiKeyManager — never hardcoded.
    /// </summary>
    public class BillScanService
    {
        private const string GroqEndpoint =
            "https://api.groq.com/openai/v1/chat/completions";

        private const string Model = "meta-llama/llama-4-scout-17b-16e-instruct";

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        // ── Smart prompt ──────────────────────────────────────────────────────
        private const string Prompt = @"
You are a purchase bill parser for an Indian retail billing app.
Extract all line items from this bill image.

RULES FOR AMBIGUOUS BILLS (no column headers, just numbers):
- Numbers in range 1–500 are most likely QUANTITY (pieces/units)
- Numbers in range 100–100000 are most likely RATE (price per unit in INR)
- The largest number per row OR a number equal to qty × rate is AMOUNT/TOTAL
- If only 2 numbers per row: first is quantity, second is rate
- Ignore grand total / subtotal rows at the bottom

Return ONLY valid JSON, no markdown, no backticks, no explanation:
{
  ""invoice_number"": ""string or empty"",
  ""invoice_date"":   ""DD-MM-YYYY or empty"",
  ""supplier_name"":  ""string or empty"",
  ""items"": [
    { ""description"": ""string"", ""quantity"": 48, ""purchase_price"": 600, ""amount"": 28800 },
    { ""description"": ""string"", ""quantity"": 44, ""purchase_price"": 380, ""amount"": 16720 }
  ],
  ""grand_total"": 45520
}";

        // ── Main entry ────────────────────────────────────────────────────────
        public async Task<ScannedBillResult> ScanBillAsync(string filePath)
        {
            string apiKey = ApiKeyManager.GetKey();
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new Exception("No Groq API key found. Please set it up first.");

            string ext      = Path.GetExtension(filePath).ToLowerInvariant();
            byte[] bytes    = await File.ReadAllBytesAsync(filePath);
            string base64   = Convert.ToBase64String(bytes);
            string mimeType = GetMimeType(ext);

            // Groq vision requires an image — convert PDF page 1 to PNG if needed
            if (ext == ".pdf")
            {
                (base64, mimeType) = await ConvertPdfFirstPageAsync(filePath);
            }

            string imageUrl = $"data:{mimeType};base64,{base64}";

            // Build OpenAI-compatible request body
            var body = new
            {
                model = Model,
                max_tokens = 1024,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text",      text      = Prompt },
                            new { type = "image_url", image_url = new { url = imageUrl } }
                        }
                    }
                }
            };

            string json = JsonSerializer.Serialize(body);
            var request = new HttpRequestMessage(HttpMethod.Post, GroqEndpoint);
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _http.SendAsync(request);
            string responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(
                    $"Groq API error {response.StatusCode}:\n{responseText}");

            return ParseResponse(responseText);
        }

        // ── Parse Groq response → ScannedBillResult ───────────────────────────
        private static ScannedBillResult ParseResponse(string raw)
        {
            var doc = JsonDocument.Parse(raw);

            // OpenAI format: choices[0].message.content
            string text = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";

            // Strip any accidental markdown fences
            text = text
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            // Find the JSON object inside the text (model sometimes adds preamble)
            int start = text.IndexOf('{');
            int end   = text.LastIndexOf('}');
            if (start >= 0 && end > start)
                text = text.Substring(start, end - start + 1);

            var billJson = JsonNode.Parse(text)
                ?? throw new Exception("Model returned empty or unparseable JSON.");

            var result = new ScannedBillResult
            {
                InvoiceNumber = billJson["invoice_number"]?.GetValue<string>() ?? "",
                InvoiceDate   = billJson["invoice_date"]?.GetValue<string>()   ?? "",
                SupplierName  = billJson["supplier_name"]?.GetValue<string>()  ?? "",
                GrandTotal    = SafeDecimal(billJson["grand_total"])
            };

            var items = billJson["items"]?.AsArray();
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item == null) continue;
                    // Try "purchase_price" first (our key), then "rate" as fallback
                    // in case the model uses the old key name
                    decimal pp = SafeDecimal(item["purchase_price"]);
                    if (pp == 0) pp = SafeDecimal(item["rate"]);

                    result.Items.Add(new ScannedBillItem
                    {
                        Description   = item["description"]?.GetValue<string>() ?? "",
                        Quantity      = SafeDouble(item["quantity"]),
                        PurchasePrice = pp,
                        Amount        = SafeDecimal(item["amount"])
                    });
                }
            }

            return result;
        }

        // ── PDF → PNG conversion using System.Drawing (no extra NuGet needed) ─
        private static async Task<(string base64, string mime)>
            ConvertPdfFirstPageAsync(string pdfPath)
        {
            // Fallback: if PDF conversion not available, throw helpful message
            // For full PDF support add PdfiumViewer or PDFsharp NuGet
            throw new Exception(
                "PDF scanning requires an additional library.\n\n" +
                "Please convert your bill to an image (JPG/PNG) and try again.\n" +
                "You can take a photo with your phone and upload that instead.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static string GetMimeType(string ext) => ext switch
        {
            ".jpg"  => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png"  => "image/png",
            ".webp" => "image/webp",
            _       => "image/jpeg"
        };

        private static decimal SafeDecimal(JsonNode node)
        {
            try { return node?.GetValue<decimal>() ?? 0m; } catch { }
            try { if (decimal.TryParse(node?.ToString(), out var d)) return d; } catch { }
            return 0m;
        }

        private static double SafeDouble(JsonNode node)
        {
            try { return node?.GetValue<double>() ?? 0; } catch { }
            try { if (double.TryParse(node?.ToString(), out var d)) return d; } catch { }
            return 0;
        }
    }
}
