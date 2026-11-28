# Báo cáo tiến độ

Bảng tổng và mỗi task một file, viết theo `_TEMPLATE.md`. Nguồn phân công: Report-2_Project_Tracking.xlsx, sheet WBS.

Tiến độ tính theo 5 mốc, mỗi mốc 20%: đối chiếu tài liệu, code, test tự động, chạy với phụ thuộc thật, file giải thích.

Trạng thái: ⏳ Chưa làm · 🔄 Đang làm · ⚠️ Xong code, còn điều kiện · ✅ Xong

| Mã | Task | Trạng thái | Tiến độ | Ước lượng (PD) | Kết thúc | Báo cáo |
|---|---|---|---:|---:|---|---|
| M-05 | Specification Editor & Freeze Baseline | ✅ Xong | 100% | 4 | 2026-10-30 | [M-05.md](M-05.md) |
| C-02 | Baseline & Detailed Specification - Customer | ✅ Xong | 100% | 1 | 2026-10-31 | [C-02.md](C-02.md) |
| C-09 | Contract Addendum (làm thêm theo yêu cầu) | ⚠️ Xong màn hình | 80% | 2 | 2026-10-31 | [C-09.md](C-09.md) |
| API-01 | Upload file | ⚠️ Xong trên ổ local | 80% | 1 | 2026-10-30 | [API-01.md](API-01.md) |
| API-02 | Get file URL | ✅ Xong | 100% | 1 | 2026-10-30 | [API-02.md](API-02.md) |
| API-03 | Send email | ⚠️ Xong code | 80% | 0.5 | 2026-10-30 | [API-03.md](API-03.md) |
| API-04 | Delivery status | ⚠️ Xong phía WPSM | 80% | 1 | 2026-10-30 | [API-04.md](API-04.md) |
| JOB-07 | Dispatch notification emails | ✅ Xong | 100% | 1 | 2026-10-30 | [JOB-07.md](JOB-07.md) |
| Repo | Dọn cấu trúc dự án | ⚠️ Chờ quyết định | 60% | — | 2026-11-03 | [structure-cleanup.md](structure-cleanup.md) |
| API | Tách endpoint khỏi Program.cs | ✅ Xong | 100% | — | 2026-11-03 | [API-endpoints-refactor.md](API-endpoints-refactor.md) |
| SDS-GAPS | Hoàn thiện chức năng còn thiếu theo Report 4 | ⚠️ Xong code và test, còn 4 suite Razor cũ lỗi | 100% | — | 2026-11-05 | [SDS-GAPS.md](SDS-GAPS.md) |
| ADMIN-UI | Thiết kế lại giao diện Admin: thanh điều hướng ngang, A-01..A-18 | ✅ Xong | 100% | — | 2026-11-06 | [ADMIN-UI.md](ADMIN-UI.md) |
| A-01, A-09..A-18 | Màn hình Admin còn thiếu | ✅ Xong, đã áp WPSM_DB | 100% | — | 2026-11-03 | [A-admin-screens.md](A-admin-screens.md) |
| FE-ROUTES | Khung route React và chuyển các màn đã có API | ⚠️ Xong code, suite Razor cũ lỗi từ trước | 100% | - | 2026-11-04 | [FE-ROUTES.md](FE-ROUTES.md) |
| G-01 | Sửa lỗi đăng nhập qua form sau merge | ⚠️ Xong code, còn 4 bộ test lỗi | 80% | 0 | 2026-11-02 | [G-01-login-fix.md](G-01-login-fix.md) |
| SEED-EXTRA | Thêm dữ liệu mẫu vào WPSM_DB | ✅ Xong | 100% | - | 2026-11-16 | [SEED-EXTRA.md](SEED-EXTRA.md) |
| A-19 | Admin quản lý tài khoản (ngoài SRS) | ✅ Xong | 100% | - | 2026-11-16 | [A-19.md](A-19.md) |
| DB-LOCALHOST | Chuyển API sang SQL Server đầy đủ (localhost) | ✅ Xong | 100% | - | 2026-11-16 | [DB-LOCALHOST.md](DB-LOCALHOST.md) |
| EMAIL-SMTP | Gửi email thật qua SMTP (Brevo relay) | ⚠️ Xong code, chờ SMTP key Brevo | 80% | - | 2026-11-16 | [EMAIL-SMTP.md](EMAIL-SMTP.md) |
| CLEAN-ARCH | Đưa backend về đúng Clean Architecture (mọi luồng ghi qua use case, trừ JOB-04/05) | ⚠️ Xong code, đã commit/push; chưa chạy e2e, 6 cụm chưa chạy thật | 80% | - | 2026-11-23 | [CLEAN-ARCH.md](CLEAN-ARCH.md) |
| BUG-DISCORD-1125 | Sửa 8 lỗi nhóm báo trên Discord #sep-code (22–24/11) | ⚠️ Xong code và test, chưa commit | 80% | - | 2026-11-25 | [BUG-DISCORD-1125.md](BUG-DISCORD-1125.md) |
| EVENT-TERMS | Điều khoản chính sách chỉ áp dụng cho hợp đồng ký sau ngày hiệu lực | ⚠️ Xong code và test, đã chạy migration trên DB chính (28/11), chưa commit | 90% | 2 | 2026-11-26 | [EVENT-TERMS.md](EVENT-TERMS.md) |
| DOC-REPORT3-4-1126 | Report 3: sơ đồ cho tính năng mới; Report 4: 15 class diagram theo feature, activation bar, rà nội dung (v3) | ⚠️ Report 3 v6 và Report 4 v4 khớp code (Word và sơ đồ), Context Diagram tách theo nhóm dữ liệu; chờ mở bằng Word kiểm tra | 90% | 1.5 | 2026-11-27 | [DOC-REPORT3-4-1126.md](DOC-REPORT3-4-1126.md) |
| REPORT5-TEST-1127 | Report 5: kế hoạch kiểm thử, ca kiểm thử (Excel), kết quả vòng 1–2, danh sách lỗi | ⚠️ Xong vòng 1–2 (unit 227/227, integration 30/30, hệ thống 78/85 rồi 22/22), còn vòng 3 và kiểm thử chấp nhận | 70% | 2 | 2026-11-27 | [REPORT5-TEST-1127.md](REPORT5-TEST-1127.md) |

**Tổng ước lượng phần được giao:** 9.5 PD. C-09 làm thêm, không tính vào tổng.

Các task đến 2026-10-31 chưa đo giờ thật. Từ nay Claude ghi giờ bắt đầu và kết thúc vào báo cáo.
