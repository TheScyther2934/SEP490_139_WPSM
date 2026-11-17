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
        <p>Tài khoản WPSM cho dự án <strong>{WebUtility.HtmlEncode(projectCode)}</strong> đã được tạo.</p>
        <p>Email đăng nhập: <strong>{WebUtility.HtmlEncode(email)}</strong><br>Mật khẩu tạm: <strong>{WebUtility.HtmlEncode(temporaryPassword)}</strong></p>
        <p>Vui lòng <a href="{WebUtility.HtmlEncode(baseUrl.TrimEnd('/'))}/login">đăng nhập</a> và đổi mật khẩu ngay trong lần đầu sử dụng.</p>
        """;

    internal static string BaselineFrozen(string fullName, string projectCode, string baseUrl) =>
        $"""
        <p>Xin chào {WebUtility.HtmlEncode(fullName)},</p>
        <p>Hợp đồng gốc <strong>{WebUtility.HtmlEncode(projectCode)}</strong> đã được chốt và lộ trình chuẩn bị đã sẵn sàng.</p>
        <p>Bạn có thể xem <a href="{WebUtility.HtmlEncode(baseUrl.TrimEnd('/'))}/customer/contract">đặc tả</a> và <a href="{WebUtility.HtmlEncode(baseUrl.TrimEnd('/'))}/customer/roadmap">lộ trình</a> trên WPSM.</p>
        """;
}
