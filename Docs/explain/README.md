# Giải thích code theo chức năng

Mỗi chức năng có một file, viết theo `_TEMPLATE.md`.

| Mã | Chức năng | File |
|---|---|---|
| M-05 | Đặc tả và chốt baseline | [m05-specification-freeze.md](m05-specification-freeze.md) |
| C-02 | Hợp đồng và đặc tả của khách | [c02-customer-baseline.md](c02-customer-baseline.md) |
| C-09 | Phụ lục hợp đồng | [c09-contract-addendum.md](c09-contract-addendum.md) |
| API-01, API-02 | Lưu và tải file | [api01-02-file-service.md](api01-02-file-service.md) |
| API-03, API-04, JOB-07 | Gửi email | [api03-04-job07-email.md](api03-04-job07-email.md) |
| G-01 | Đăng nhập qua form | [g01-login-form.md](g01-login-form.md) |
| A-01, A-09..A-18 | Màn hình Admin | [a-admin-screens.md](a-admin-screens.md) |
| API | Cấu trúc endpoint (`src/API/Endpoints/`) | [api-endpoints-structure.md](api-endpoints-structure.md) |
| FE-ROUTES | Khung route React, layout theo role, các màn đã chuyển từ Razor | [fe-routes-react-client.md](fe-routes-react-client.md) |
| SDS-GAPS | CR, BEO, ngày cưới, thanh toán, đánh giá/khiếu nại, tài nguyên, thông báo, cấu hình Admin, JOB-01..06 | [sds-gaps.md](sds-gaps.md) |
| ADMIN-UI | Giao diện Admin: thanh điều hướng ngang, theme riêng, A-01/A-02/A-03/A-04/A-06 theo mockup | [admin-ui.md](admin-ui.md) |
| SEED-EXTRA | Dữ liệu mẫu bổ sung: phụ phí CR, sảnh, dịch vụ, tài nguyên, 6 dự án mẫu | [seed-extra-data.md](seed-extra-data.md) |
| A-19 | Admin quản lý tài khoản: danh sách, tạo Manager/Admin, sửa, khóa/mở khóa, cấp lại mật khẩu | [A-19-account-management.md](A-19-account-management.md) |
| EMAIL-SMTP | Gửi email thật qua SMTP (Brevo relay): một đường gửi qua outbox, không lộ mật khẩu khi có SMTP, link trỏ React | [EMAIL-SMTP-real-email.md](EMAIL-SMTP-real-email.md) |
| CLEAN-ARCH | Clean Architecture: entity ở Domain, quy tắc ở `Domain/Rules`, use case cho phiếu điều phối, hợp đồng phát sinh, khóa dịch vụ; bảng đã chuyển / chưa chuyển | [clean-architecture.md](clean-architecture.md) |
| BUG-DISCORD-1125 | Sửa lỗi đợt test 22–24/11: cờ "cần nhà cung cấp", xóa minh chứng, mở lại nhiệm vụ, lịch sử phiếu, PDF bộ phận có nhiệm vụ, quy tắc số điện thoại | [bug-discord-1125.md](bug-discord-1125.md) |
