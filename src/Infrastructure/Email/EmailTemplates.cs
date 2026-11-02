using System.Net;

namespace WPSM.Infrastructure.Email;

internal static class EmailTemplates
{
    internal static string Credentials(string fullName, string email, string temporaryPassword, string projectCode, string baseUrl) =>
        $"""
        <p>Xin chào {WebUtility.HtmlEncode(fullName)},</p>
        <p>Tài khoản WPSM cho dự án <strong>{WebUtility.HtmlEncode(projectCode)}</strong> đã được tạo.</p>
        <p>Email đăng nhập: <strong>{WebUtility.HtmlEncode(email)}</strong><br>Mật khẩu tạm: <strong>{WebUtility.HtmlEncode(temporaryPassword)}</strong></p>
        <p>Vui lòng <a href="{WebUtility.HtmlEncode(baseUrl.TrimEnd('/'))}/G01">đăng nhập</a> và đổi mật khẩu ngay trong lần đầu sử dụng.</p>
        """;

    internal static string BaselineFrozen(string fullName, string projectCode, string baseUrl) =>
        $"""
        <p>Xin chào {WebUtility.HtmlEncode(fullName)},</p>
        <p>Hợp đồng gốc <strong>{WebUtility.HtmlEncode(projectCode)}</strong> đã được chốt và lộ trình chuẩn bị đã sẵn sàng.</p>
        <p>Bạn có thể xem <a href="{WebUtility.HtmlEncode(baseUrl.TrimEnd('/'))}/C02">đặc tả</a> và <a href="{WebUtility.HtmlEncode(baseUrl.TrimEnd('/'))}/C03">lộ trình</a> trên WPSM.</p>
        """;
}
