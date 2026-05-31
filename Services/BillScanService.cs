using MyWPFCRUDApp.Models;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MyWPFCRUDApp.Services
{
    /// <summary>
    /// Sends a purchase bill (PDF or image) to Google Gemini 2.0 Flash
    /// and returns a structured ScannedBillResult ready for review.
    /// </summary>
    public class BillScanService
    {
        // ── Store your key in db_config.txt folder or app.config ──────────────
        // For now it reads from a sibling file "gemini_key.txt" next to the .exe
        // so it is never hardcoded in source code.
        private static string ApiKey
        {
            get
            {
                string keyFile = Path.Combine(
     AppDomain.CurrentDomain.BaseDirectory,
     "gemini_key.txt");

                if (File.Exists(keyFile))
                    return File.ReadAllText(keyFile).Trim();

                throw new Exception("Gemini API key not found.");
            }
        }

        private const string GeminiEndpoint =
     "https://generativelanguage.googleapis.com/v1/models/gemini-2.5-flash:generateContent";

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        // ── Smart prompt that handles ambiguous bills like your Sana example ──
        private const string SystemPrompt = @"
You are a purchase bill parser for an Indian retail billing app.
Extract all line items from this bill image or PDF.

RULES FOR AMBIGUOUS BILLS (no column headers, just numbers):
- Numbers in range 1–500 are most likely QUANTITY (pieces/units)
- Numbers in range 100–100000 are most likely RATE (price per unit in INR)
- The largest number per row, OR a number that equals qty × rate, is AMOUNT/TOTAL
- If only 2 numbers per row: treat first as quantity, second as rate
- Ignore the grand total / subtotal rows at the bottom
- Description may be blank if not printed on the bill

Return ONLY a valid JSON object with NO markdown, NO backticks, NO explanation:
{
  ""invoice_number"": ""string or empty"",
  ""invoice_date"":   ""DD-MM-YYYY or empty"",
  ""supplier_name"":  ""string or empty"",
  ""items"": [
    { ""description"": ""string"", ""quantity"": 48, ""rate"": 600,  ""amount"": 28800 },
    { ""description"": ""string"", ""quantity"": 44, ""rate"": 380,  ""amount"": 16720 }
  ],
  ""grand_total"": 45520
}";

        // ────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Main entry: accepts any .pdf, .jpg, .jpeg, .png file path.
        /// Returns parsed bill or throws on failure.
        /// </summary>
        public async Task<ScannedBillResult> ScanBillAsync(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
            string base64 = Convert.ToBase64String(fileBytes);
            string mimeType = GetMimeType(ext);

            // Build Gemini request body
            var requestBody = BuildRequestBody(base64, mimeType);
            string json = JsonSerializer.Serialize(requestBody);

            var request = new HttpRequestMessage(HttpMethod.Post,
                $"{GeminiEndpoint}?key={ApiKey}");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _http.SendAsync(request);
            string responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Gemini API error {response.StatusCode}:\n{responseText}");

            return ParseGeminiResponse(responseText);
        }

        // ── Build the multipart request body ─────────────────────────────────
        private static object BuildRequestBody(string base64Data, string mimeType)
        {
            return new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = SystemPrompt },
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = mimeType,
                                    data      = base64Data
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature      = 0.1,   // low = more deterministic / accurate
                    maxOutputTokens  = 2048
                }
            };
        }

        // ── Parse Gemini's JSON response → ScannedBillResult ─────────────────
        private static ScannedBillResult ParseGeminiResponse(string rawResponse)
        {
            var doc = JsonDocument.Parse(rawResponse);

            // Navigate: candidates[0].content.parts[0].text
            string text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "";

            // Strip any accidental markdown fences Gemini sometimes adds
            text = text
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            var billJson = JsonNode.Parse(text)
                ?? throw new Exception("Gemini returned empty or unparseable JSON.");

            var result = new ScannedBillResult
            {
                InvoiceNumber = billJson["invoice_number"]?.GetValue<string>() ?? "",
                InvoiceDate   = billJson["invoice_date"]?.GetValue<string>()   ?? "",
                SupplierName  = billJson["supplier_name"]?.GetValue<string>()  ?? "",
                GrandTotal    = billJson["grand_total"]?.GetValue<decimal>()   ?? 0m,
            };

            var items = billJson["items"]?.AsArray();
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item == null) continue;
                    result.Items.Add(new ScannedBillItem
                    {
                        Description = item["description"]?.GetValue<string>() ?? "",
                        Quantity    = item["quantity"]?.GetValue<double>()    ?? 0,
                        Rate        = item["rate"]?.GetValue<decimal>()       ?? 0m,
                        Amount      = item["amount"]?.GetValue<decimal>()     ?? 0m,
                    });
                }
            }

            return result;
        }

        // ── Mime type helper ──────────────────────────────────────────────────
        private static string GetMimeType(string ext) => ext switch
        {
            ".pdf"  => "application/pdf",
            ".jpg"  => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png"  => "image/png",
            ".webp" => "image/webp",
            _       => "application/octet-stream"
        };
    }
}
