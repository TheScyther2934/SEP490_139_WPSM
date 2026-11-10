using Application.Persistence;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Infrastructure.Pdf;

/// <summary>One A4 layout for addenda, BEO department sheets and settlement receipts (SDS I.1, II.2 IPdfGenerator).</summary>
public sealed class QuestPdfGenerator : IPdfGenerator
{
    static QuestPdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Render(PdfDocumentModel model) => Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            // Arial ships with Windows and covers Vietnamese diacritics; QuestPDF falls back to its bundled font elsewhere.
            page.DefaultTextStyle(x => x.FontFamily("Arial", "Segoe UI", "Lato").FontSize(10));
            page.Header().Column(column =>
            {
                column.Item().Text("WPSM – Wedding Project & Service Management").FontSize(8).FontColor(Colors.Grey.Darken1);
                column.Item().Text(model.Title).FontSize(16).Bold();
                if (!string.IsNullOrWhiteSpace(model.Subtitle)) column.Item().Text(model.Subtitle).FontColor(Colors.Grey.Darken2);
            });
            page.Content().PaddingVertical(12).Column(column =>
            {
                column.Spacing(10);
                if (model.Facts.Count > 0)
                {
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.ConstantColumn(150); c.RelativeColumn(); });
                        foreach (var (label, value) in model.Facts)
                        {
                            table.Cell().PaddingVertical(2).Text(label).SemiBold();
                            table.Cell().PaddingVertical(2).Text(value);
                        }
                    });
                }
                foreach (var data in model.Tables)
                {
                    if (!string.IsNullOrWhiteSpace(data.Heading)) column.Item().PaddingTop(6).Text(data.Heading).FontSize(12).Bold();
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { foreach (var _ in data.Columns) c.RelativeColumn(); });
                        table.Header(header =>
                        {
                            foreach (var name in data.Columns)
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(name).SemiBold();
                        });
                        if (data.Rows.Count == 0)
                            table.Cell().ColumnSpan((uint)data.Columns.Count).Padding(4).Text("Không có dữ liệu.").Italic();
                        foreach (var row in data.Rows)
                            foreach (var cell in row)
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(cell);
                    });
                }
                foreach (var note in model.Notes) column.Item().Text(note).FontColor(Colors.Grey.Darken2);
            });
            page.Footer().AlignRight().Text(text =>
            {
                text.Span($"Tạo lúc {DateTime.UtcNow.AddHours(7):dd/MM/yyyy HH:mm} (UTC+7) · Trang ");
                text.CurrentPageNumber();
                text.Span("/");
                text.TotalPages();
            });
        });
    }).GeneratePdf();
}
