using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MyWPFCRUDApp.Services
{
    /// <summary>
    /// Sends a purchase bill (one or more images, or a PDF's pages→images) to
    /// Groq's qwen/qwen3.6-27b vision model and returns a parsed ScannedBillResult.
    /// Supports multi-page / multi-photo bills (up to 5 images per Groq's limit)
    /// so long bills split across several photos or PDF pages can be read as one document.
    /// Key is read from ApiKeyManager — never hardcoded.
    /// </summary>
    public class BillScanService
    {
        private const string GroqEndpoint =
            "https://api.groq.com/openai/v1/chat/completions";

        // qwen3.6-27b: current production Groq vision model (llama-4-scout/maverick
        // are deprecated). Supports up to 5 images/request and native JSON mode.
        private const string Model = "qwen/qwen3.6-27b";

        // Groq's hard limit for this model.
        private const int MaxImagesPerRequest = 5;

        // Bills with many rows produce large JSON. 1000 tokens was truncating
        // mid-array on long bills, and Groq's json_object validator rejects
        // a truncated response outright (json_validate_failed) before our
        // finish_reason=="length" fallback below ever gets a chance to run.
        // Raised to give plenty of headroom for long item lists.
        private const int MaxCompletionTokens = 900;

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(120)
        };

        // ── Smart prompt ──────────────────────────────────────────────────────
        private const string Prompt = @"
You are a purchase bill parser for an Indian retail billing app.
You may be given MULTIPLE images (photos of one bill from different
angles, or consecutive pages of the same bill/PDF). Treat them as ONE
document and merge all line items into a single list, in the order they
appear across the images. Do not skip or summarize rows — extract every
single line item, no matter how many there are.

RULES FOR AMBIGUOUS BILLS (no column headers, just numbers):
- Numbers in range 1–500 are most likely QUANTITY (pieces/units)
- Numbers in range 100–100000 are most likely RATE (price per unit in INR)
- The largest number per row OR a number equal to qty × rate is AMOUNT/TOTAL
- If only 2 numbers per row: first is quantity, second is rate
- Ignore grand total / subtotal rows at the bottom

HSN CODE:
- If the bill already prints an HSN/SAC code for a row, use it exactly.
- If no HSN code is printed, infer the most likely HSN code for that item
  from its description ONLY if you are reasonably confident (common,
  well-known goods, e.g. rice, cement, mobile phones, apparel).
- If you are not confident, leave hsn_code as an empty string. Never
  fabricate a plausible-looking code — an empty string is preferred over
  a wrong guess.

Return ONLY valid JSON, no markdown, no backticks, no explanation:
{
  ""invoice_number"": ""string or empty"",
  ""invoice_date"":   ""DD-MM-YYYY or empty"",
  ""supplier_name"":  ""string or empty"",
  ""items"": [
    { ""description"": ""string"", ""hsn_code"": ""string or empty"", ""quantity"": 48, ""purchase_price"": 600, ""amount"": 28800 },
    { ""description"": ""string"", ""hsn_code"": ""string or empty"", ""quantity"": 44, ""purchase_price"": 380, ""amount"": 16720 }
  ],
  ""grand_total"": 45520
}";

        // ── Main entry: single image (kept for backward compatibility) ────────
        public Task<ScannedBillResult> ScanBillAsync(string filePath)
            => ScanBillAsync(new List<string> { filePath });

        // ── Main entry: one or more images / PDF pages of the SAME bill ───────
        public async Task<ScannedBillResult> ScanBillAsync(IReadOnlyList<string> filePaths)
        {
            if (filePaths == null || filePaths.Count == 0)
                throw new ArgumentException("At least one file must be provided.");

            if (filePaths.Count > MaxImagesPerRequest)
                throw new Exception(
                    $"This bill has {filePaths.Count} images, but the model only " +
                    $"accepts {MaxImagesPerRequest} per request. Please split it into " +
                    $"batches of {MaxImagesPerRequest} or fewer and merge the results.");

            string apiKey = ApiKeyManager.GetKey();
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new Exception("No Groq API key found. Please set it up first.");

            var imageContentBlocks = new List<object>();
            foreach (var path in filePaths)
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                string base64;
                string mimeType;

                if (ext == ".pdf")
                {
                    (base64, mimeType) = await ConvertPdfFirstPageAsync(path);
                }
                else
                {
                    byte[] bytes = await File.ReadAllBytesAsync(path);
                    base64 = Convert.ToBase64String(bytes);
                    mimeType = GetMimeType(ext);
                }

                string imageUrl = $"data:{mimeType};base64,{base64}";
                imageContentBlocks.Add(new
                {
                    type = "image_url",
                    image_url = new { url = imageUrl }
                });
            }

            // Text prompt first, then all image blocks in order.
            var content = new List<object> { new { type = "text", text = Prompt } };
            content.AddRange(imageContentBlocks);

            var body = new
            {
                model = Model,
                max_completion_tokens = MaxCompletionTokens,
                temperature = 0.2,
                response_format = new { type = "json_object" },
                // qwen3.6-27b defaults to "thinking mode" (reasoning_effort=
                // "default"), and its chain-of-thought can leak straight into
                // message.content as <think>...</think> text. Combined with
                // response_format=json_object, that non-JSON content gets
                // rejected outright as json_validate_failed (with an empty
                // failed_generation) — this is NOT the same failure as
                // truncation from too low a token limit. "none" disables
                // thinking mode entirely so the model answers directly with
                // JSON. Groq's Qwen models only accept "none" or "default"
                // here (not "low"/"medium"/"high").
                reasoning_effort = "none",
                messages = new[]
                {
                    new { role = "user", content = content.ToArray() }
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

            var choice = doc.RootElement.GetProperty("choices")[0];
            var message = choice.GetProperty("message");

            // If the model was cut off, finish_reason will say "length" —
            // surface that clearly instead of failing on broken JSON silently.
            string finishReason = choice.TryGetProperty("finish_reason", out var fr)
                ? fr.GetString() ?? ""
                : "";

            string text = message.GetProperty("content").GetString() ?? "";

            // Strip any accidental markdown fences
            text = text.Replace("```json", "").Replace("```", "").Trim();

            // Find the JSON object inside the text (model sometimes adds preamble)
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start >= 0 && end > start)
                text = text.Substring(start, end - start + 1);

            JsonNode billJson;
            try
            {
                billJson = JsonNode.Parse(text)
                    ?? throw new Exception("Model returned empty JSON.");
            }
            catch (Exception ex)
            {
                if (finishReason == "length")
                    throw new Exception(
                        "The bill has more line items than fit in one response. " +
                        "Try scanning it in smaller batches (e.g. split the photo " +
                        "into top-half/bottom-half) and merge the results.", ex);
                throw new Exception("Model returned unparseable JSON: " + ex.Message, ex);
            }

            var result = new ScannedBillResult
            {
                InvoiceNumber = billJson["invoice_number"]?.GetValue<string>() ?? "",
                InvoiceDate = billJson["invoice_date"]?.GetValue<string>() ?? "",
                SupplierName = billJson["supplier_name"]?.GetValue<string>() ?? "",
                GrandTotal = SafeDecimal(billJson["grand_total"])
            };

            var items = billJson["items"]?.AsArray();
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item == null) continue;
                    decimal pp = SafeDecimal(item["purchase_price"]);
                    if (pp == 0) pp = SafeDecimal(item["rate"]);

                    result.Items.Add(new ScannedBillItem
                    {
                        Description = item["description"]?.GetValue<string>() ?? "",
                        HsnCode = item["hsn_code"]?.GetValue<string>() ?? "",
                        Quantity = SafeDouble(item["quantity"]),
                        PurchasePrice = pp,
                        Amount = SafeDecimal(item["amount"])
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
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
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