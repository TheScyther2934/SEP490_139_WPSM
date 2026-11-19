using Domain.Services;
using System.Net;

namespace Infrastructure.Email;

// Links point to the React client (src/Client/src/routes/paths.js), served by the API at Wpsm:PublicBaseUrl.
internal static class EmailTemplates
{
    internal static string AccountCredentials(string fullName, string email, string temporaryPassword, string role, bool reset, string baseUrl) =>
        $"""
        <p>Xin chào {WebUtility.HtmlEncode(fullName)},</p>
        <p>{(reset ? "Mật khẩu tài khoản WPSM của bạn đã được Admin cấp lại." : $"Admin đã tạo cho bạn tài khoản WPSM với vai trò <strong>{WebUtility.HtmlEncode(role)}</strong>.")}</p>
        <p>Email đăng nhập: <strong>{WebUtility.HtmlEncode(email)}</strong><br>Mật khẩu tạm: <strong>{WebUtility.HtmlEncode(temporaryPassword)}</strong></p>
        <p>Vui lòng <a href="{WebUtility.HtmlEncode(baseUrl.TrimEnd('/'))}/login">đăng nhập</a> và đổi mật khẩu ngay trong lần đầu sử dụng.</p>
        """;

    internal static string Credentials(string fullName, string email, string temporaryPassword, string projectCode, string baseUrl) =>
        $"""
        <p>Xin chào {WebUtility.HtmlEncode(fullName)},</p>
        <p>Tài khoản WPSM cho sự kiện <strong>{WebUtility.HtmlEncode(projectCode)}</strong> đã được tạo.</p>
        <p>Email đăng nhập: <strong>{WebUtility.HtmlEncode(email)}</strong><br>Mật khẩu tạm: <strong>{WebUtility.HtmlEncode(temporaryPassword)}</strong></p>
        <p>Vui lòng <a href="{WebUtility.HtmlEncode(baseUrl.TrimEnd('/'))}/login">đăng nhập</a> và đổi mật khẩu ngay trong lần đầu sử dụng.</p>
        """;

    internal static string EventLocked(string fullName, string projectCode, DateOnly eventDate, string contacts, string hotline, string baseUrl) =>
        $"""
        <p>Xin chào <strong>{WebUtility.HtmlEncode(fullName)}</strong>,</p>
        <p>Dịch vụ của sự kiện <strong>{WebUtility.HtmlEncode(projectCode)}</strong> (ngày cưới {eventDate:dd/MM/yyyy}) đã được chốt. Từ bây giờ bạn theo dõi tiến độ trên tài khoản, mọi thay đổi xin liên hệ người phụ trách:</p>
        <p>{contacts}<br>Hotline: <strong>{WebUtility.HtmlEncode(hotline)}</strong></p>
        <p><a href="{WebUtility.HtmlEncode(baseUrl)}/customer/event">Xem dịch vụ đã đặt</a></p>
        <p>Trân trọng,<br>Trung tâm tiệc cưới WPSM</p>
        """;

    internal static string AddonConfirmRequest(string fullName, string projectCode, string addonCode, string title, decimal total, string baseUrl) =>
        $"""
        <p>Xin chào <strong>{WebUtility.HtmlEncode(fullName)}</strong>,</p>
        <p>Sự kiện <strong>{WebUtility.HtmlEncode(projectCode)}</strong> có hợp đồng phát sinh <strong>{WebUtility.HtmlEncode(addonCode)}</strong> ({WebUtility.HtmlEncode(title)}) trị giá <strong>{EventStageRules.Vnd(total)}</strong>.</p>
        <p>Phát sinh chỉ được thực hiện sau khi bạn đối chiếu và bấm xác nhận: <a href="{WebUtility.HtmlEncode(baseUrl)}/customer/event">mở trang dịch vụ đã đặt</a>.</p>
        <p>Trân trọng,<br>Trung tâm tiệc cưới WPSM</p>
        """;

    internal static string SettlementInvoice(string fullName, string projectCode, decimal contractTotal, decimal addonTotal, decimal depositPaid,
        decimal amountDue, DateOnly dueDate, string link) =>
        $"""
        <p>Xin chào <strong>{WebUtility.HtmlEncode(fullName)}</strong>,</p>
        <p>Hóa đơn tất toán của sự kiện <strong>{WebUtility.HtmlEncode(projectCode)}</strong>:</p>
        <table style="border-collapse: collapse; margin: 15px 0;">
            <tr><td style="padding: 6px 12px;">Hợp đồng sau điều chỉnh:</td><td style="padding: 6px 12px;">{EventStageRules.Vnd(contractTotal)}</td></tr>
            <tr><td style="padding: 6px 12px;">Hợp đồng phát sinh đã xác nhận:</td><td style="padding: 6px 12px;">{EventStageRules.Vnd(addonTotal)}</td></tr>
            <tr><td style="padding: 6px 12px;">Trừ tiền đặt cọc:</td><td style="padding: 6px 12px;">-{EventStageRules.Vnd(depositPaid)}</td></tr>
            <tr><td style="padding: 6px 12px; font-weight: bold;">Cần thanh toán:</td><td style="padding: 6px 12px; color: #d32f2f; font-weight: bold;">{EventStageRules.Vnd(amountDue)}</td></tr>
            <tr><td style="padding: 6px 12px;">Hạn thanh toán:</td><td style="padding: 6px 12px;">{dueDate:dd/MM/yyyy}</td></tr>
        </table>
        <p><a href="{WebUtility.HtmlEncode(link)}">Xem chi tiết</a></p>
        <p>Trân trọng,<br>Trung tâm tiệc cưới WPSM</p>
        """;

    internal static string OverdueAlert(
        string recipientName,
        string role,
        string projectCode,
        string taskName,
        DateTime deadline,
        string hallName,
        DateOnly eventDate,
        string alertUrl)
    {
        var roleGreeting = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "Quản lý";

        return $"""
        <p>Xin chào {roleGreeting} <strong>{WebUtility.HtmlEncode(recipientName)}</strong>,</p>
        <p style="color: #d32f2f; font-weight: bold; font-size: 1.1em;">
            ⚠️ CẢNH BÁO: Nhiệm vụ ngày sự kiện đã quá hạn (Overdue Alert)!
        </p>
        <p>Hệ thống WPSM phát hiện nhiệm vụ ngày sự kiện sau đây đã quá hạn thực hiện:</p>
        <table style="border-collapse: collapse; margin: 15px 0;">
            <tr><td style="padding: 6px 12px; font-weight: bold;">Mã sự kiện:</td><td style="padding: 6px 12px;">{WebUtility.HtmlEncode(projectCode)}</td></tr>
            <tr><td style="padding: 6px 12px; font-weight: bold;">Nhiệm vụ:</td><td style="padding: 6px 12px; color: #d32f2f; font-weight: bold;">{WebUtility.HtmlEncode(taskName)}</td></tr>
            <tr><td style="padding: 6px 12px; font-weight: bold;">Hạn chót:</td><td style="padding: 6px 12px;">{deadline:HH:mm dd/MM/yyyy}</td></tr>
            <tr><td style="padding: 6px 12px; font-weight: bold;">Sảnh tiệc:</td><td style="padding: 6px 12px;">{WebUtility.HtmlEncode(hallName)}</td></tr>
            <tr><td style="padding: 6px 12px; font-weight: bold;">Ngày tổ chức:</td><td style="padding: 6px 12px;">{eventDate:dd/MM/yyyy}</td></tr>
        </table>
        <p>Vui lòng <a href="{WebUtility.HtmlEncode(alertUrl)}">truy cập hệ thống</a> để kiểm tra tình trạng và điều phối xử lý ngay lập tức.</p>
        <p>Trân trọng,<br>Hệ thống Quản lý Tiệc cưới WPSM</p>
        """;
    }
}
