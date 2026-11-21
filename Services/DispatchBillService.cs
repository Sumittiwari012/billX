using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace MyWPFCRUDApp.Services
{
    public static class DispatchBillFormat
    {
        public const string Standard = "Standard";
        public const string Custom = "Custom";
    }

    /// <summary>
    /// Builds the dispatch bill for a stock transfer as an HTML page and opens it in the
    /// default browser (Ctrl+P / the Print button there prints it).
    ///
    /// Two formats, both rendered by the same placeholder engine:
    ///   Standard - built-in layout, includes the terms and conditions.
    ///   Custom   - your own HTML file (custom_template.html). Edit it freely; use the
    ///              placeholders listed at the top of that file.
    ///
    /// Files live in  %AppData%\MyWPFCRUDApp\DispatchBill\
    ///   terms.txt             one term per line, used by {{TermsHtml}} (both formats)
    ///   custom_template.html  your own bill layout (created from a sample on first use)
    /// </summary>
    public static class DispatchBillService
    {
        static readonly CultureInfo Ci = CultureInfo.GetCultureInfo("en-IN");
        static readonly HashSet<string> RawKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LogoImg", "SignatureImg", "TermsHtml" };

        // ───────────── files ─────────────
        public static string Folder
        {
            get
            {
                var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyWPFCRUDApp", "DispatchBill");
                Directory.CreateDirectory(d);
                return d;
            }
        }
        public static string TermsPath => Path.Combine(Folder, "terms.txt");
        public static string CustomTemplatePath => Path.Combine(Folder, "custom_template.html");

        public static void EnsureFiles()
        {
            if (!File.Exists(TermsPath)) File.WriteAllText(TermsPath, DefaultTerms, new UTF8Encoding(true));
            if (!File.Exists(CustomTemplatePath)) File.WriteAllText(CustomTemplatePath, CustomSample, new UTF8Encoding(true));
        }

        // ───────────── signature image ─────────────
        static readonly string[] SigExts = { ".png", ".jpg", ".jpeg" };

        static string FindSignature() =>
            SigExts.Select(e => Path.Combine(Folder, "signature" + e)).FirstOrDefault(File.Exists);

        /// <summary>Saves a copy of the chosen PNG/JPG as the signature shown on the bill.</summary>
        public static void SaveSignature(string sourcePath)
        {
            var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (!SigExts.Contains(ext)) throw new InvalidOperationException("Choose a PNG or JPG image.");
            RemoveSignature();
            File.Copy(sourcePath, Path.Combine(Folder, "signature" + ext), true);
        }

        public static void RemoveSignature()
        {
            foreach (var e in SigExts)
            {
                var p = Path.Combine(Folder, "signature" + e);
                if (File.Exists(p)) File.Delete(p);
            }
        }

        // Embedded as a data URI so the saved bill file is self-contained.
        static string SignatureHtml()
        {
            var p = FindSignature();
            if (p == null) return "";
            var mime = p.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
            return $"<img src='data:{mime};base64,{Convert.ToBase64String(File.ReadAllBytes(p))}' style='max-height:60px;max-width:200px'/>";
        }

        public static void EditTerms() { EnsureFiles(); OpenInNotepad(TermsPath); }
        public static void EditCustomTemplate() { EnsureFiles(); OpenInNotepad(CustomTemplatePath); }

        static void OpenInNotepad(string path) =>
            Process.Start(new ProcessStartInfo("notepad.exe", "\"" + path + "\"") { UseShellExecute = true });

        public static void OpenInBrowser(string path) =>
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

        // ───────────── generate ─────────────
        /// <summary>Renders the bill for a saved transfer and returns the HTML file's path.</summary>
        public static string Generate(long transferId, string format)
        {
            EnsureFiles();
            var t = StockTransferService.GetTransfer(transferId)
                    ?? throw new InvalidOperationException("Transfer not found.");

            string template = format == DispatchBillFormat.Custom
                ? File.ReadAllText(CustomTemplatePath, Encoding.UTF8)
                : StandardTemplate;

            var lines = LoadLines(transferId);
            var data = BuildHeader(t, lines);
            string html = Render(template, data, lines);

            var outDir = Path.Combine(Folder, "out");
            Directory.CreateDirectory(outDir);
            var file = Path.Combine(outDir, $"{t.TransferNo}_{format}.html");
            File.WriteAllText(file, html, new UTF8Encoding(true));
            return file;
        }

        // ───────────── data ─────────────
        sealed class Line
        {
            public string Barcode, Product, Category, SubCategory, Hsn, Unit, Size, Colour, Remarks;
            public decimal Qty, Mrp;
            public decimal Amount => Qty * Mrp;      // MRP value (purchase price is never shown on the bill)
        }

        static List<Line> LoadLines(long transferId) =>
            Db.Query(@"SELECT i.Barcode, i.DispatchedQty, i.Remarks,
                              p.ProductName, p.HSNCode, p.Size, p.Colour, p.MRP,
                              u.UnitName, c.CategoryName, sc.SubCategoryName
                       FROM StockTransferItem i
                       JOIN MProducts p ON p.Id = i.ProductId
                       LEFT JOIN MUnit u ON u.Id = p.UnitId
                       LEFT JOIN MCategory c ON c.Id = p.CategoryId
                       LEFT JOIN MSubCategory sc ON sc.Id = p.SubCategoryId
                       WHERE i.TransferId = @id ORDER BY i.Id",
                r => new Line
                {
                    Barcode = Rd.Str(r, "Barcode"),
                    Product = Rd.Str(r, "ProductName"),
                    Hsn = Rd.Str(r, "HSNCode"),
                    Unit = Rd.Str(r, "UnitName"),
                    Category = Rd.Str(r, "CategoryName"),
                    SubCategory = Rd.Str(r, "SubCategoryName"),
                    Size = Rd.Str(r, "Size"),
                    Colour = Rd.Str(r, "Colour"),
                    Remarks = Rd.Str(r, "Remarks"),
                    Qty = Rd.Dec(r, "DispatchedQty"),
                    Mrp = Rd.NDec(r, "MRP") ?? 0m
                }, ("@id", transferId));

        static string LocationAddress(string type, long id)
        {
            try
            {
                var m = StockService.Map(type);
                return Db.Query($"SELECT Address FROM {m.LocTable} WHERE Id = @id",
                    r => Rd.Str(r, "Address"), ("@id", id)).FirstOrDefault() ?? "";
            }
            catch { return ""; }
        }

        static Dictionary<string, string> BuildHeader(Models.StockTransfer t, List<Line> lines)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // company (read straight from MCompanyInfo)
            string[] c = null;
            try
            {
                c = Db.Query(@"SELECT CompanyName, Phone, Mobile, Email, AddressLine1, AddressLine2, City, State, Pincode,
                                      GSTNumber, PANNumber, LogoPath
                               FROM MCompanyInfo ORDER BY Id LIMIT 1",
                    r => new[]
                    {
                        Rd.Str(r,"CompanyName"), Rd.Str(r,"Phone"), Rd.Str(r,"Mobile"), Rd.Str(r,"Email"),
                        Rd.Str(r,"AddressLine1"), Rd.Str(r,"AddressLine2"), Rd.Str(r,"City"), Rd.Str(r,"State"),
                        Rd.Str(r,"Pincode"), Rd.Str(r,"GSTNumber"), Rd.Str(r,"PANNumber"), Rd.Str(r,"LogoPath")
                    }).FirstOrDefault();
            }
            catch { }
            c ??= new string[12];
            string J(params string[] p) => string.Join(", ", p.Where(x => !string.IsNullOrWhiteSpace(x)));

            d["CompanyName"] = c[0] ?? "";
            d["CompanyPhone"] = J(c[1], c[2]);
            d["CompanyEmail"] = c[3] ?? "";
            d["CompanyAddress"] = J(c[4], c[5], c[6], c[7], c[8]);
            d["CompanyGST"] = c[9] ?? "";
            d["CompanyPAN"] = c[10] ?? "";
            d["LogoImg"] = !string.IsNullOrWhiteSpace(c[11]) && File.Exists(c[11])
                ? $"<img src='{new Uri(c[11]).AbsoluteUri}' style='max-height:70px;max-width:160px'/>" : "";

            // transfer
            d["TransferNo"] = t.TransferNo ?? "";
            d["TransferDate"] = (t.DispatchedAt ?? t.CreatedDate).ToString("dd-MMM-yyyy hh:mm tt", Ci);
            d["Status"] = t.Status ?? "";
            d["FromType"] = t.FromType ?? "";
            d["FromName"] = t.FromName ?? "";
            d["FromAddress"] = LocationAddress(t.FromType, t.FromId);
            d["ToType"] = t.ToType ?? "";
            d["ToName"] = t.ToName ?? "";
            d["ToAddress"] = LocationAddress(t.ToType, t.ToId);
            d["DispatchedBy"] = t.DispatchedBy ?? t.CreatedBy ?? "";
            d["SignatureImg"] = SignatureHtml();
            d["Remarks"] = t.Remarks ?? "";
            d["PrintedOn"] = DateTime.Now.ToString("dd-MMM-yyyy hh:mm tt", Ci);

            // totals
            d["TotalLines"] = lines.Count.ToString(Ci);
            d["TotalQty"] = lines.Sum(l => l.Qty).ToString("0.###", Ci);
            d["TotalAmount"] = lines.Sum(l => l.Amount).ToString("N2", Ci);

            // terms & conditions (one per line in terms.txt)
            var terms = File.ReadAllLines(TermsPath, Encoding.UTF8).Where(x => !string.IsNullOrWhiteSpace(x));
            d["TermsHtml"] = "<ol>" + string.Join("", terms.Select(x => "<li>" + WebUtility.HtmlEncode(x.Trim()) + "</li>")) + "</ol>";
            return d;
        }

        static Dictionary<string, string> RowData(Line l, int no) => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SrNo"] = no.ToString(Ci),
            ["Barcode"] = l.Barcode ?? "",
            ["ProductName"] = l.Product ?? "",
            ["HSN"] = l.Hsn ?? "",
            ["Unit"] = l.Unit ?? "",
            ["Category"] = l.Category ?? "",
            ["SubCategory"] = l.SubCategory ?? "",
            ["Size"] = l.Size ?? "",
            ["Colour"] = l.Colour ?? "",
            ["Qty"] = l.Qty.ToString("0.###", Ci),
            ["Amount"] = l.Amount.ToString("N2", Ci),
            ["MRP"] = l.Mrp.ToString("N2", Ci),
            ["LineRemarks"] = l.Remarks ?? ""
        };

        // ───────────── template engine ─────────────
        // {{Name}} is replaced (HTML-encoded). Everything between {{#Items}} and {{/Items}} is
        // repeated once per product line, using the row placeholders.
        static string Render(string template, Dictionary<string, string> data, List<Line> lines)
        {
            const string open = "{{#Items}}", close = "{{/Items}}";
            int a = template.IndexOf(open, StringComparison.OrdinalIgnoreCase);
            int b = template.IndexOf(close, StringComparison.OrdinalIgnoreCase);
            if (a >= 0 && b > a)
            {
                string row = template.Substring(a + open.Length, b - a - open.Length);
                var sb = new StringBuilder();
                int n = 1;
                foreach (var l in lines) sb.Append(Fill(row, RowData(l, n++)));
                template = template.Substring(0, a) + sb + template.Substring(b + close.Length);
            }
            return Fill(template, data);
        }

        static string Fill(string s, Dictionary<string, string> d) =>
            Regex.Replace(s, @"\{\{(\w+)\}\}", m =>
            {
                var key = m.Groups[1].Value;
                if (!d.TryGetValue(key, out var v)) return m.Value;     // unknown token: leave visible so typos are easy to spot
                return RawKeys.Contains(key) ? (v ?? "") : WebUtility.HtmlEncode(v ?? "");
            });

        // ───────────── defaults ─────────────
        // Starter text only - edit terms.txt (Edit terms button) to match your own policy.
        const string DefaultTerms =
@"Goods are dispatched only for the items and quantities listed in this bill.
The receiving party must check quantity and condition of the goods at the time of receipt.
Any shortage, damage or discrepancy must be reported to the dispatching party in writing within 24 hours of receipt.
Goods remain the property of the dispatching party until they are received and accepted.
This bill is a stock dispatch document and is not a tax invoice.
The receiving party must sign and return the acknowledgement copy of this bill.";

        const string StandardTemplate = @"<!DOCTYPE html>
<html><head><meta charset='utf-8'><title>Dispatch Bill {{TransferNo}}</title>
<style>
 @page { size: A4; margin: 12mm; }
 body { font-family: 'Segoe UI', Arial, sans-serif; font-size: 12px; color: #222; margin: 0; }
 .wrap { max-width: 800px; margin: 0 auto; padding: 16px; }
 table { width: 100%; border-collapse: collapse; }
 .head td { vertical-align: top; }
 .co-name { font-size: 20px; font-weight: 700; }
 .title { font-size: 18px; font-weight: 700; text-align: right; letter-spacing: 1px; }
 .box { border: 1px solid #999; padding: 8px; vertical-align: top; width: 50%; }
 .lbl { font-size: 10px; color: #666; text-transform: uppercase; letter-spacing: .5px; }
 .items { margin-top: 12px; }
 .items { font-size: 11px; }
 .items th, .items td { border: 1px solid #999; padding: 4px 5px; }
 .items th { background: #f0f0f0; text-align: left; }
 .r { text-align: right; } .c { text-align: center; }
 .sign td { padding-top: 10px; height: 70px; vertical-align: bottom; border-top: 0; width: 50%; font-size: 11px; }
 .terms { margin-top: 16px; font-size: 11px; }
 .terms ol { margin: 4px 0 0 18px; padding: 0; }
 .no-print { margin-bottom: 8px; text-align: right; }
 @media print { .no-print { display: none; } }
</style></head><body><div class='wrap'>
<div class='no-print'><button onclick='window.print()'>Print</button></div>

<table class='head'><tr>
 <td style='width:170px'>{{LogoImg}}</td>
 <td><div class='co-name'>{{CompanyName}}</div>
     <div>{{CompanyAddress}}</div>
     <div>Ph: {{CompanyPhone}} &nbsp; {{CompanyEmail}}</div>
     <div>GSTIN: {{CompanyGST}} &nbsp; PAN: {{CompanyPAN}}</div></td>
 <td style='width:230px'><div class='title'>DISPATCH BILL</div>
     <div class='r'>No: <b>{{TransferNo}}</b></div>
     <div class='r'>Date: {{TransferDate}}</div>
     <div class='r'>Status: {{Status}}</div></td>
</tr></table>

<table style='margin-top:10px'><tr>
 <td class='box'><div class='lbl'>Dispatch from ({{FromType}})</div><b>{{FromName}}</b><div>{{FromAddress}}</div></td>
 <td class='box'><div class='lbl'>Dispatch to ({{ToType}})</div><b>{{ToName}}</b><div>{{ToAddress}}</div></td>
</tr></table>

<table class='items'>
 <thead><tr>
  <th class='c' style='width:26px'>#</th><th>Barcode</th><th>Product</th><th>Category / Sub-category</th>
  <th>HSN</th><th>Size</th><th>Colour</th><th>Unit</th>
  <th class='r'>Qty</th><th class='r'>MRP</th><th class='r'>MRP value</th>
 </tr></thead>
 <tbody>
{{#Items}}
  <tr><td class='c'>{{SrNo}}</td><td>{{Barcode}}</td><td>{{ProductName}}</td><td>{{Category}} / {{SubCategory}}</td>
      <td>{{HSN}}</td><td>{{Size}}</td><td>{{Colour}}</td><td>{{Unit}}</td>
      <td class='r'>{{Qty}}</td><td class='r'>{{MRP}}</td><td class='r'>{{Amount}}</td></tr>
{{/Items}}
 </tbody>
 <tfoot><tr>
  <td colspan='8' class='r'><b>Total ({{TotalLines}} lines)</b></td>
  <td class='r'><b>{{TotalQty}}</b></td><td></td><td class='r'><b>{{TotalAmount}}</b></td>
 </tr></tfoot>
</table>

<p><span class='lbl'>Remarks</span><br/>{{Remarks}}</p>

<table class='sign'><tr>
 <td>{{SignatureImg}}<br/>Dispatched by: <b>{{DispatchedBy}}</b><br/>Signature &amp; stamp</td>
 <td class='r'>Received by (name, signature &amp; date)</td>
</tr></table>

<div class='terms'><b>Terms &amp; Conditions</b>{{TermsHtml}}</div>
<div class='lbl' style='margin-top:10px'>Printed on {{PrintedOn}}</div>
</div></body></html>";

        const string CustomSample = @"<!DOCTYPE html>
<!--
  YOUR OWN DISPATCH BILL TEMPLATE
  Edit this file (layout, colours, wording, add/remove anything) and save it.
  In the app choose Dispatch bill > Custom, then Print / Preview.

  HEADER PLACEHOLDERS
    {{CompanyName}} {{CompanyAddress}} {{CompanyPhone}} {{CompanyEmail}} {{CompanyGST}} {{CompanyPAN}} {{LogoImg}} {{SignatureImg}}
    {{TransferNo}} {{TransferDate}} {{Status}} {{DispatchedBy}} {{Remarks}} {{PrintedOn}}
    {{FromType}} {{FromName}} {{FromAddress}}   {{ToType}} {{ToName}} {{ToAddress}}
    {{TotalLines}} {{TotalQty}} {{TotalAmount}}
    {{TermsHtml}}  = the terms and conditions from terms.txt (optional - delete it to leave them out)

  PRODUCT ROWS - put one row between {{#Items}} and {{/Items}}; it repeats for every product:
    {{SrNo}} {{Barcode}} {{ProductName}} {{Category}} {{SubCategory}} {{HSN}} {{Unit}} {{Size}} {{Colour}}
    {{Qty}} {{MRP}} {{Amount}} (= Qty x MRP) {{LineRemarks}}
  Purchase price is deliberately not available on the bill.
-->
<html><head><meta charset='utf-8'><title>{{TransferNo}}</title>
<style>
 body { font-family: Arial, sans-serif; font-size: 13px; margin: 24px; }
 table { width: 100%; border-collapse: collapse; }
 th, td { border: 1px solid #888; padding: 5px; }
 .no-print { text-align: right; } @media print { .no-print { display: none; } }
</style></head><body>
<div class='no-print'><button onclick='window.print()'>Print</button></div>
{{LogoImg}}
<h2>{{CompanyName}} - Dispatch Bill {{TransferNo}}</h2>
<p>Date: {{TransferDate}}<br/>From: <b>{{FromName}}</b> &nbsp;&rarr;&nbsp; To: <b>{{ToName}}</b></p>
<table>
 <tr><th>#</th><th>Barcode</th><th>Product</th><th>Category</th><th>Size</th><th>Colour</th><th>Qty</th><th>MRP</th><th>MRP value</th></tr>
{{#Items}}
 <tr><td>{{SrNo}}</td><td>{{Barcode}}</td><td>{{ProductName}}</td><td>{{Category}} / {{SubCategory}}</td><td>{{Size}}</td><td>{{Colour}}</td><td>{{Qty}}</td><td>{{MRP}}</td><td>{{Amount}}</td></tr>
{{/Items}}
 <tr><td colspan='6'><b>Total</b></td><td><b>{{TotalQty}}</b></td><td></td><td><b>{{TotalAmount}}</b></td></tr>
</table>
<p>Remarks: {{Remarks}}</p>
<p>Dispatched by: {{DispatchedBy}}</p>
{{TermsHtml}}
</body></html>";
    }
}