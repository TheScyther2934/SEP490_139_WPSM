using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Domain.Services;

public sealed record FinalSettlementReceiptPdfModel(
    string ReceiptNumber,
    string ProjectCode,
    string CoupleName,
    string CustomerEmail,
    string CustomerPhone,
    DateOnly EventDate,
    string HallName,
    decimal BaseContractValue,
    decimal TotalApprovedCrVariance,
    decimal CurrentProjectTotal,
    decimal TotalAmountPaid,
    decimal FinalBalance,
    string Status,
    string IssuingManagerName,
    DateTime IssueDate,
    IReadOnlyList<(string Type, DateOnly? Date, string? Method, decimal Amount, string? RecordedBy)> Payments);

public static class ReceiptPdfGenerator
{
    public static byte[] GenerateFinalSettlementReceiptPdf(FinalSettlementReceiptPdfModel model)
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.ASCII);

        var objects = new List<string>();

        // 1: Catalog
        objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
        // 2: Pages
        objects.Add("<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        // 3: Page (A4: 595 x 842 pt)
        objects.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R /F2 6 0 R >> >> >>");
        // 5: Font Regular
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        // 6: Font Bold
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>");

        // 4: Stream Contents
        var sb = new StringBuilder();
        sb.AppendLine("BT");

        // Header
        sb.AppendLine("/F2 16 Tf 50 790 Td (WPSM - WEDDING PLANNING AND SERVICE MANAGEMENT) Tj");
        sb.AppendLine("/F1 10 Tf 0 -18 Td (Trung tam To chuc va Quan ly Su kien Tiec cuoi WPSM) Tj");
        sb.AppendLine("/F2 14 Tf 0 -30 Td (BIEN NHAN QUYET TOAN HOP DONG) Tj");
        sb.AppendLine("/F1 10 Tf 0 -15 Td (FINAL SETTLEMENT RECEIPT) Tj");

        // Receipt Meta
        sb.AppendLine($"/F2 10 Tf 0 -25 Td (So bien nhan: ) Tj /F1 10 Tf ({EscapePdf(model.ReceiptNumber)}) Tj");
        sb.AppendLine($"/F2 10 Tf 0 -14 Td (Ma du an: ) Tj /F1 10 Tf ({EscapePdf(model.ProjectCode)}) Tj");
        sb.AppendLine($"/F2 10 Tf 0 -14 Td (Ngay phat hanh: ) Tj /F1 10 Tf ({model.IssueDate:dd/MM/yyyy HH:mm} UTC+7) Tj");

        // Customer Info
        sb.AppendLine("/F2 11 Tf 0 -25 Td (THONG TIN KHACH HANG VA DU AN) Tj");
        sb.AppendLine($"/F2 10 Tf 0 -15 Td (Cap doi: ) Tj /F1 10 Tf ({EscapePdf(StripDiacritics(model.CoupleName))}) Tj");
        sb.AppendLine($"/F2 10 Tf 0 -14 Td (Email: ) Tj /F1 10 Tf ({EscapePdf(model.CustomerEmail)}) Tj /F2 10 Tf 150 0 Td (Dien thoai: ) Tj /F1 10 Tf ({EscapePdf(model.CustomerPhone)}) Tj");
        sb.AppendLine($"/F2 10 Tf -150 -14 Td (Ngay cuoi: ) Tj /F1 10 Tf ({model.EventDate:dd/MM/yyyy}) Tj /F2 10 Tf 150 0 Td (Sanh tiec: ) Tj /F1 10 Tf ({EscapePdf(StripDiacritics(model.HallName))}) Tj");

        // Financial Summary
        sb.AppendLine("/F2 11 Tf -150 -25 Td (TONG KET TAI CHINH (ITEMIZED FINANCIAL SUMMARY)) Tj");
        sb.AppendLine($"/F1 10 Tf 0 -15 Td (- Gia tri hop dong goc (Base Contract): ) Tj /F2 10 Tf ({model.BaseContractValue:N0} VND) Tj");
        sb.AppendLine($"/F1 10 Tf 0 -14 Td (- Chenh lech CR duoc duyet (CR Variance): ) Tj /F2 10 Tf ({model.TotalApprovedCrVariance:+ #,##0;- #,##0;0} VND) Tj");
        sb.AppendLine($"/F1 10 Tf 0 -14 Td (- Tong gia tri du an sau dieu chinh (Project Total): ) Tj /F2 10 Tf ({model.CurrentProjectTotal:N0} VND) Tj");
        sb.AppendLine($"/F1 10 Tf 0 -14 Td (- Tong so tien da thanh toan (Total Paid): ) Tj /F2 10 Tf ({model.TotalAmountPaid:N0} VND) Tj");
        sb.AppendLine($"/F2 10 Tf 0 -14 Td (- Du no con lai (Final Outstanding Balance): ) Tj /F2 10 Tf ({model.FinalBalance:N0} VND - DA QUYET TOAN) Tj");

        // Payment History
        sb.AppendLine("/F2 11 Tf 0 -25 Td (LICH SU THANH TOAN (PAYMENT HISTORY)) Tj");
        foreach (var p in model.Payments)
        {
            var dateStr = p.Date.HasValue ? p.Date.Value.ToString("dd/MM/yyyy") : "---";
            var methodStr = p.Method ?? "---";
            var typeName = p.Type;
            sb.AppendLine($"/F1 9 Tf 0 -14 Td ([{typeName}] Ngay: {dateStr} | PT: {methodStr} | So tien: {p.Amount:N0} VND | Nguoi thu: {EscapePdf(StripDiacritics(p.RecordedBy ?? "Manager"))}) Tj");
        }

        // Settlement Status & Signoff
        sb.AppendLine("/F2 11 Tf 0 -30 Td (XAC NHAN QUYET TOAN HOP DONG) Tj");
        sb.AppendLine("/F1 9 Tf 0 -14 Td (Ban quan ly WPSM xac nhan hop dong tren da hoan tat day du nghia vu thanh toan.) Tj");
        sb.AppendLine("/F1 9 Tf 0 -12 Td (Trang thai du an: SETTLED - Khong con vuong mac tai chinh.) Tj");
        sb.AppendLine($"/F2 10 Tf 0 -25 Td (Dai dien Ban Quan ly: {EscapePdf(StripDiacritics(model.IssuingManagerName))}) Tj");

        sb.AppendLine("ET");

        var streamBytes = Encoding.ASCII.GetBytes(sb.ToString());
        var streamObj = $"<< /Length {streamBytes.Length} >>\nstream\n{sb}endstream";
        objects.Insert(3, streamObj);

        // Write PDF file
        writer.WriteLine("%PDF-1.4");
        writer.Flush();

        var offsets = new List<long>();
        long currentOffset = ms.Position;

        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add(currentOffset);
            var objHeader = $"{i + 1} 0 obj\n";
            var headerBytes = Encoding.ASCII.GetBytes(objHeader);
            ms.Write(headerBytes, 0, headerBytes.Length);

            var bodyBytes = Encoding.ASCII.GetBytes(objects[i]);
            ms.Write(bodyBytes, 0, bodyBytes.Length);

            var objFooter = "\nendobj\n";
            var footerBytes = Encoding.ASCII.GetBytes(objFooter);
            ms.Write(footerBytes, 0, footerBytes.Length);

            currentOffset = ms.Position;
        }

        long xrefOffset = ms.Position;
        writer.WriteLine("xref");
        writer.WriteLine($"0 {objects.Count + 1}");
        writer.WriteLine("0000000000 65535 f ");
        foreach (var off in offsets)
        {
            writer.WriteLine($"{off:D10} 00000 n ");
        }
        writer.WriteLine("trailer");
        writer.WriteLine($"<< /Size {objects.Count + 1} /Root 1 0 R >>");
        writer.WriteLine("startxref");
        writer.WriteLine(xrefOffset);
        writer.WriteLine("%%EOF");
        writer.Flush();

        return ms.ToArray();
    }

    private static string EscapePdf(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }

    private static string StripDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                if (c == 'đ' || c == 'Đ') stringBuilder.Append('d');
                else stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }
}
