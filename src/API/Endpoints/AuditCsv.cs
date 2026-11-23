using System.Text;
using Application.Persistence;

namespace API.Endpoints;

// CSV of the audit log for Excel: UTF-8 with BOM, Vietnamese headers and labels, times in Vietnam time.
public static class AuditCsv
{
    private static readonly Dictionary<string, string> Actions = new() { ["Create"] = "Thêm", ["Update"] = "Sửa", ["Delete"] = "Xóa", ["Login"] = "Đăng nhập" };
    private static readonly Dictionary<string, string> Impacts = new()
    {
        ["Project"] = "Sự kiện", ["Service"] = "Dịch vụ", ["Upload"] = "Upload", ["Account"] = "Tài khoản", ["Payment"] = "Thanh toán",
        ["Addon"] = "Hợp đồng phát sinh", ["Policy"] = "Chính sách", ["Complaint"] = "Khiếu nại & đánh giá", ["Exception"] = "Yêu cầu đặc biệt", ["Other"] = "Khác"
    };

    public static byte[] Build(IReadOnlyList<AuditLogRow> rows)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Thời gian,Người thực hiện,Email,Vai trò,Hoạt động,Ảnh hưởng,Đối tượng,Mã,Sự kiện,Tóm tắt,Thay đổi,IP");
        foreach (var r in rows)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(r.OccurredAt, Domain.Services.VietnamTime.VietnamTimeZone);
            csv.AppendLine(string.Join(",", new[]
            {
                local.ToString("dd/MM/yyyy HH:mm:ss"), r.AccountName, r.Email, r.Role, Actions.GetValueOrDefault(r.Action, r.Action),
                Impacts.GetValueOrDefault(r.Impact, r.Impact), r.EntityType, r.EntityId, r.ProjectCode, r.Summary, r.Changes, r.IpAddress
            }.Select(Cell)));
        }
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }

    // Quotes every cell; a leading = + - @ is neutralised so a value can never run as a spreadsheet formula.
    private static string Cell(string? value)
    {
        var text = value ?? "";
        if (text.Length > 0 && "=+-@".Contains(text[0])) text = "'" + text;
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }
}
