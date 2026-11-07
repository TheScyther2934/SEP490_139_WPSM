using System.Net;

namespace Infrastructure.Email;

internal static class EmailTemplates
{
    internal static string Credentials(string fullName, string email, string temporaryPassword, string projectCode, string baseUrl) =>
        $"""
        <p>Xin chào {WebUtility.HtmlEncode(fullName)},</p>
        <p>Tài khoản WPSM cho dự án <strong>{WebUtility.HtmlEncode(projectCode)}</strong> đã được tạo.</p>
        <p>Email đăng nhập: <strong>{WebUtility.HtmlEncode(email)}</strong><br>Mật khẩu tạm: <strong>{WebUtility.HtmlEncode(temporaryPassword)}</strong></p>
        <p>Vui lòng <a href="{WebUtility.HtmlEncode(baseUrl.TrimEnd('/'))}/login">đăng nhập</a> và đổi mật khẩu ngay trong lần đầu sử dụng.</p>
        """;

    internal static string BaselineFrozen(string fullName, string projectCode, string baseUrl) =>
        $"""
        <p>Xin chào {WebUtility.HtmlEncode(fullName)},</p>
        <p>Hợp đồng gốc <strong>{WebUtility.HtmlEncode(projectCode)}</strong> đã được chốt và lộ trình chuẩn bị đã sẵn sàng.</p>
        <p>Bạn có thể xem <a href="{WebUtility.HtmlEncode(baseUrl.TrimEnd('/'))}/C02">đặc tả</a> và <a href="{WebUtility.HtmlEncode(baseUrl.TrimEnd('/'))}/C03">lộ trình</a> trên WPSM.</p>
        """;

    internal static string RoadmapMilestoneReminder(
        string fullName,
        string role,
        string projectCode,
        string taskTitle,
        string milestone,
        DateOnly dueDate,
        int remainingDays,
        string roadmapUrl)
    {
        var roleGreeting = string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase) ? "Quản lý" : "Quý khách";
        var remainingText = remainingDays == 0 ? "hôm nay" : $"còn {remainingDays} ngày";

        return $"""
        <p>Xin chào {roleGreeting} <strong>{WebUtility.HtmlEncode(fullName)}</strong>,</p>
        <p>Hệ thống WPSM xin gửi thông báo nhắc việc cho dự án <strong>{WebUtility.HtmlEncode(projectCode)}</strong>:</p>
        <table style="border-collapse: collapse; margin: 15px 0;">
            <tr><td style="padding: 6px 12px; font-weight: bold;">Giai đoạn (Milestone):</td><td style="padding: 6px 12px;">{WebUtility.HtmlEncode(milestone)}</td></tr>
            <tr><td style="padding: 6px 12px; font-weight: bold;">Nhiệm vụ:</td><td style="padding: 6px 12px;">{WebUtility.HtmlEncode(taskTitle)}</td></tr>
            <tr><td style="padding: 6px 12px; font-weight: bold;">Hạn chót:</td><td style="padding: 6px 12px;">{dueDate:dd/MM/yyyy}</td></tr>
            <tr><td style="padding: 6px 12px; font-weight: bold;">Thời hạn còn lại:</td><td style="padding: 6px 12px;">{WebUtility.HtmlEncode(remainingText)}</td></tr>
        </table>
        <p>Vui lòng <a href="{WebUtility.HtmlEncode(roadmapUrl)}">truy cập lộ trình công việc</a> để xem chi tiết và cập nhật tiến độ.</p>
        <p>Trân trọng,<br>Hệ thống Quản lý Tiệc cưới WPSM</p>
        """;
    }

    internal static string T30PaymentRequest(
        string fullName,
        string role,
        string projectCode,
        decimal amount,
        DateOnly dueDate,
        decimal currentTotal,
        decimal totalPaid,
        decimal outstandingBalance,
        string paymentUrl,
        bool hasNegotiatingCr)
    {
        var isManager = string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);
        var roleGreeting = isManager ? "Quản lý" : "Quý khách";
        var negotiatingNote = isManager && hasNegotiatingCr
            ? "<p style=\"color: #b78103; font-weight: bold;\">⚠️ Lưu ý: Dự án đang có yêu cầu thay đổi (CR) ở trạng thái đàm phán (Negotiating). Vui lòng kiểm tra và xử lý trên hệ thống.</p>"
            : "";

        return $"""
        <p>Xin chào {roleGreeting} <strong>{WebUtility.HtmlEncode(fullName)}</strong>,</p>
        <p>Hệ thống WPSM xin thông báo phát hành yêu cầu thanh toán đợt hai (Mốc T-30) cho dự án tiệc cưới <strong>{WebUtility.HtmlEncode(projectCode)}</strong>:</p>
        <table style="border-collapse: collapse; margin: 15px 0;">
            <tr><td style="padding: 6px 12px; font-weight: bold;">Đợt thanh toán:</td><td style="padding: 6px 12px;">Thanh toán đợt hai (Mốc T-30)</td></tr>
            <tr><td style="padding: 6px 12px; font-weight: bold;">Số tiền cần thanh toán:</td><td style="padding: 6px 12px; color: #d32f2f; font-weight: bold; font-size: 1.1em;">{amount:N0} VND</td></tr>
            <tr><td style="padding: 6px 12px; font-weight: bold;">Hạn thanh toán (Due Date):</td><td style="padding: 6px 12px;">{dueDate:dd/MM/yyyy}</td></tr>
            <tr><td style="padding: 6px 12px; font-weight: bold;">Tổng giá trị hợp đồng hiện tại:</td><td style="padding: 6px 12px;">{currentTotal:N0} VND</td></tr>
            <tr><td style="padding: 6px 12px; font-weight: bold;">Số tiền đã thanh toán:</td><td style="padding: 6px 12px;">{totalPaid:N0} VND</td></tr>
            <tr><td style="padding: 6px 12px; font-weight: bold;">Dư nợ còn lại (Outstanding):</td><td style="padding: 6px 12px;">{outstandingBalance:N0} VND</td></tr>
        </table>
        {negotiatingNote}
        <p>Vui lòng <a href="{WebUtility.HtmlEncode(paymentUrl)}">nhấn vào đây để xem chi tiết đợt thanh toán</a> trên hệ thống WPSM.</p>
        <p>Trân trọng,<br>Hệ thống Quản lý Tiệc cưới WPSM</p>
        """;
    }

    internal static string FinalSettlementPreparation(
        string managerName,
        string projectCode,
        string customerName,
        DateOnly eventDate,
        decimal baseContractValue,
        decimal approvedCrVariance,
        decimal currentTotal,
        decimal totalPaid,
        decimal finalAmountDue,
        string settlementUrl) =>
        $"""
        <p>Xin chào Quản lý <strong>{WebUtility.HtmlEncode(managerName)}</strong>,</p>
        <p>Sự kiện tiệc cưới của dự án <strong>{WebUtility.HtmlEncode(projectCode)}</strong> (Khách hàng: <strong>{WebUtility.HtmlEncode(customerName)}</strong>) đã diễn ra vào ngày <strong>{eventDate:dd/MM/yyyy}</strong>.</p>
        <p>Hệ thống đã tự động tính toán số liệu quyết toán cuối cùng (Final Settlement) như sau:</p>
        <table style="border-collapse: collapse; margin: 15px 0;">
            <tr><td style="padding: 6px 12px; font-weight: bold;">Giá trị hợp đồng gốc:</td><td style="padding: 6px 12px;">{baseContractValue:N0} VND</td></tr>
            <tr><td style="padding: 6px 12px; font-weight: bold;">Chênh lệch CR đã duyệt:</td><td style="padding: 6px 12px;">{approvedCrVariance:N0} VND</td></tr>
            <tr><td style="padding: 6px 12px; font-weight: bold;">Tổng giá trị dự án hiện tại:</td><td style="padding: 6px 12px;">{currentTotal:N0} VND</td></tr>
            <tr><td style="padding: 6px 12px; font-weight: bold;">Tổng số tiền đã ghi nhận:</td><td style="padding: 6px 12px;">{totalPaid:N0} VND</td></tr>
            <tr><td style="padding: 6px 12px; font-weight: bold;">Số tiền còn phải thanh toán (Final Due):</td><td style="padding: 6px 12px; color: #d32f2f; font-weight: bold; font-size: 1.1em;">{finalAmountDue:N0} VND</td></tr>
        </table>
        <p>Vui lòng truy cập <a href="{WebUtility.HtmlEncode(settlementUrl)}">Màn hình thanh toán &amp; quyết toán (M-13)</a> để đối soát các khoản thanh toán thực tế, ghi nhận thanh toán đợt cuối và hoàn tất quyết toán hợp đồng.</p>
        <p>Trân trọng,<br>Hệ thống Quản lý Tiệc cưới WPSM</p>
        """;
}
