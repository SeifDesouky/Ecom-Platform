using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace EcomPlatform.Infrastructure.Helpers
{
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

            var headerLine = headers ?? props.Select(p => p.Name).ToArray();
            sb.AppendLine(string.Join(",", headerLine.Select(EscapeCsv)));

            foreach (var row in rows)
            {
                string[] values = rowMapper != null
                    ? rowMapper(row)
                    : props.Select(p => FormatValue(p.GetValue(row))).ToArray();

                sb.AppendLine(string.Join(",", values.Select(EscapeCsv)));
            }

            return Encoding.UTF8.GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(sb.ToString()))
                .ToArray();
        }

        // ── Excel (ClosedXML — .xlsx حقيقي) ──────────────────────────────────

        public static byte[] ToExcel<T>(
            IEnumerable<T> rows,
            string[]? headers = null,
            Func<T, string[]>? rowMapper = null,
            string? sheetTitle = null)
        {
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var headerLine = headers ?? props.Select(p => p.Name).ToArray();
            var list = rows.ToList();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Report");

            int startRow = 1;

            if (!string.IsNullOrEmpty(sheetTitle))
            {
                ws.Cell(startRow, 1).Value = sheetTitle;
                ws.Cell(startRow, 1).Style.Font.Bold = true;
                ws.Cell(startRow, 1).Style.Font.FontSize = 14;
                ws.Cell(startRow, 1).Style.Font.FontColor = XLColor.FromHtml("#111827");
                ws.Range(startRow, 1, startRow, headerLine.Length).Merge();
                startRow += 2;
            }

            for (int col = 0; col < headerLine.Length; col++)
            {
                var cell = ws.Cell(startRow, col + 1);
                cell.Value = headerLine[col];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4f46e5");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#e5e7eb");
            }

            startRow++;

            for (int i = 0; i < list.Count; i++)
            {
                string[] values = rowMapper != null
                    ? rowMapper(list[i])
                    : props.Select(p => FormatValue(p.GetValue(list[i]))).ToArray();

                var rowBg = i % 2 == 0 ? XLColor.White : XLColor.FromHtml("#f9fafb");

                for (int col = 0; col < values.Length; col++)
                {
                    var cell = ws.Cell(startRow + i, col + 1);
                    cell.Value = values[col];
                    cell.Style.Fill.BackgroundColor = rowBg;
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#e5e7eb");
                }
            }

            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(startRow - 1);

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }

        // ── PDF (QuestPDF — PDF حقيقي) ────────────────────────────────────────

        public static byte[] ToPdf<T>(
            IEnumerable<T> rows,
            string[]? headers = null,
            Func<T, string[]>? rowMapper = null,
            string title = "Report",
            string subtitle = "")
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var headerLine = headers ?? props.Select(p => p.Name).ToArray();
            var list = rows.ToList();

            var document = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                    page.Header().Column(col =>
                    {
                        col.Item()
                            .Text(title)
                            .FontSize(16)
                            .Bold()
                            .FontColor(QuestPDF.Infrastructure.Color.FromHex("111827"));

                        col.Item()
                            .Text($"{subtitle} — {list.Count} rows — Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC")
                            .FontSize(9)
                            .FontColor(QuestPDF.Infrastructure.Color.FromHex("6b7280"));

                        col.Item()
                            .PaddingTop(6)
                            .PaddingBottom(6)
                            .LineHorizontal(1)
                            .LineColor(QuestPDF.Infrastructure.Color.FromHex("e5e7eb"));
                    });

                    page.Content().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            for (int i = 0; i < headerLine.Length; i++)
                                columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            foreach (var h in headerLine)
                            {
                                header.Cell()
                                    .Background(QuestPDF.Infrastructure.Color.FromHex("4f46e5"))
                                    .Padding(5)
                                    .Text(h)
                                    .FontColor(Colors.White)
                                    .Bold()
                                    .FontSize(8);
                            }
                        });

                        for (int i = 0; i < list.Count; i++)
                        {
                            string[] values = rowMapper != null
                                ? rowMapper(list[i])
                                : props.Select(p => FormatValue(p.GetValue(list[i]))).ToArray();

                            var bg = i % 2 == 0
                                ? QuestPDF.Infrastructure.Color.FromHex("ffffff")
                                : QuestPDF.Infrastructure.Color.FromHex("f9fafb");

                            foreach (var v in values)
                            {
                                table.Cell()
                                    .Background(bg)
                                    .BorderBottom(0.5f)
                                    .BorderColor(QuestPDF.Infrastructure.Color.FromHex("e5e7eb"))
                                    .Padding(4)
                                    .Text(v ?? "")
                                    .FontSize(8);
                            }
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Generated by EcomPlatform  |  Page ")
                            .FontSize(8)
                            .FontColor(QuestPDF.Infrastructure.Color.FromHex("9ca3af"));
                        x.CurrentPageNumber()
                            .FontSize(8)
                            .FontColor(QuestPDF.Infrastructure.Color.FromHex("9ca3af"));
                        x.Span(" of ")
                            .FontSize(8)
                            .FontColor(QuestPDF.Infrastructure.Color.FromHex("9ca3af"));
                        x.TotalPages()
                            .FontSize(8)
                            .FontColor(QuestPDF.Infrastructure.Color.FromHex("9ca3af"));
                    });
                });
            });

            return document.GeneratePdf();
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
    }
}