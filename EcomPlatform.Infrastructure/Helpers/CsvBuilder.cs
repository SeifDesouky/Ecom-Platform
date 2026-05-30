using System.Globalization;
using System.Reflection;
using System.Text;

namespace EcomPlatform.Infrastructure.Helpers
{
    /// <summary>
    /// يحوّل أي List&lt;T&gt; لـ CSV أو Tab-separated Excel
    /// بدون أي dependency خارجية.
    /// يستخدم الـ Property names كـ headers تلقائياً.
    /// </summary>
    public static class CsvBuilder
    {
        // ── CSV ───────────────────────────────────────────────────────────────

        public static byte[] ToCsv<T>(
            IEnumerable<T> rows,
            string[]? headers = null,
            Func<T, string[]>? rowMapper = null)
        {
            var sb = new StringBuilder();
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            // Header
            var headerLine = headers ?? props.Select(p => p.Name).ToArray();
            sb.AppendLine(string.Join(",", headerLine.Select(EscapeCsv)));

            // Rows
            foreach (var row in rows)
            {
                string[] values;
                if (rowMapper != null)
                    values = rowMapper(row);
                else
                    values = props.Select(p =>
                        FormatValue(p.GetValue(row))).ToArray();

                sb.AppendLine(string.Join(",", values.Select(EscapeCsv)));
            }

            // BOM + UTF-8 — عشان Excel يفتحه بالعربي صح
            return Encoding.UTF8.GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(sb.ToString()))
                .ToArray();
        }

        // ── Excel (Tab-separated) ─────────────────────────────────────────────

        public static byte[] ToExcel<T>(
            IEnumerable<T> rows,
            string[]? headers = null,
            Func<T, string[]>? rowMapper = null,
            string? sheetTitle = null)
        {
            var sb = new StringBuilder();
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            if (!string.IsNullOrEmpty(sheetTitle))
            {
                sb.AppendLine(sheetTitle);
                sb.AppendLine();
            }

            // Header
            var headerLine = headers ?? props.Select(p => p.Name).ToArray();
            sb.AppendLine(string.Join("\t", headerLine));

            // Rows
            foreach (var row in rows)
            {
                string[] values;
                if (rowMapper != null)
                    values = rowMapper(row);
                else
                    values = props.Select(p =>
                        FormatValue(p.GetValue(row))).ToArray();

                sb.AppendLine(string.Join("\t", values.Select(EscapeTab)));
            }

            return Encoding.UTF8.GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(sb.ToString()))
                .ToArray();
        }

        // ── PDF (HTML → بيُفتَح في المتصفح ويُطبَع) ─────────────────────────

        public static byte[] ToPdfHtml<T>(
            IEnumerable<T> rows,
            string[]? headers = null,
            Func<T, string[]>? rowMapper = null,
            string title = "Report",
            string subtitle = "")
        {
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var headerLine = headers ?? props.Select(p => p.Name).ToArray();
            var list = rows.ToList();

            var rowsHtml = new StringBuilder();
            foreach (var row in list)
            {
                string[] values;
                if (rowMapper != null)
                    values = rowMapper(row);
                else
                    values = props.Select(p => FormatValue(p.GetValue(p))).ToArray();

                rowsHtml.Append("<tr>");
                foreach (var v in values)
                    rowsHtml.Append($"<td>{System.Net.WebUtility.HtmlEncode(v)}</td>");
                rowsHtml.Append("</tr>");
            }

            var headersHtml = string.Concat(headerLine.Select(h =>
                $"<th>{System.Net.WebUtility.HtmlEncode(h)}</th>"));

            const string css =
                "* { box-sizing: border-box; margin: 0; padding: 0; }" +
                "body { font-family: Arial, sans-serif; font-size: 11px; padding: 20px; color: #1f2937; }" +
                "h1   { font-size: 18px; margin-bottom: 4px; color: #111827; }" +
                "p.sub { font-size: 11px; color: #6b7280; margin-bottom: 16px; }" +
                "table { width: 100%; border-collapse: collapse; }" +
                "th   { background: #4f46e5; color: white; padding: 8px 10px; text-align: left; font-size: 11px; white-space: nowrap; }" +
                "td   { padding: 6px 10px; border-bottom: 1px solid #e5e7eb; vertical-align: top; }" +
                "tr:nth-child(even) td { background: #f9fafb; }" +
                ".footer { margin-top: 20px; font-size: 10px; color: #9ca3af; }" +
                "@media print { body { padding: 0; } .no-print { display: none; } }";

            var titleEncoded = System.Net.WebUtility.HtmlEncode(title);
            var subtitleEncoded = System.Net.WebUtility.HtmlEncode(subtitle);
            var generatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");

            var html = "<!DOCTYPE html><html dir=\"ltr\"><head>" +
                       "<meta charset=\"UTF-8\">" +
                       "<title>" + titleEncoded + "</title>" +
                       "<style>" + css + "</style>" +
                       "</head><body>" +
                       "<h1>" + titleEncoded + "</h1>" +
                       "<p class=\"sub\">" + subtitleEncoded + " \u2014 " + list.Count + " rows \u2014 Generated: " + generatedAt + " UTC</p>" +
                       "<table>" +
                       "<thead><tr>" + headersHtml + "</tr></thead>" +
                       "<tbody>" + rowsHtml + "</tbody>" +
                       "</table>" +
                       "<p class=\"footer\">Generated by EcomPlatform</p>" +
                       "<p class=\"no-print\" style=\"margin-top:16px\">" +
                       "<button onclick=\"window.print()\" style=\"padding:8px 20px;background:#4f46e5;color:white;border:none;border-radius:6px;cursor:pointer;font-size:13px\">" +
                       "\U0001F5A8\uFE0F Print / Save as PDF" +
                       "</button></p>" +
                       "</body></html>";

            return Encoding.UTF8.GetBytes(html);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string FormatValue(object? value) => value switch
        {
            null => string.Empty,
            DateTime dt => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            decimal d => d.ToString("F2", CultureInfo.InvariantCulture),
            double dbl => dbl.ToString("F2", CultureInfo.InvariantCulture),
            bool b => b ? "Yes" : "No",
            _ => value.ToString() ?? string.Empty
        };

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }

        private static string EscapeTab(string value)
            => value?.Replace("\t", " ").Replace("\r\n", " ").Replace("\n", " ") ?? string.Empty;
    }
}