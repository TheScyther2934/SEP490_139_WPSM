# [BUG-DISCORD-1125] Sửa các lỗi nhóm báo trên Discord #sep-code (22–24/11/2026)

| Mục | Nội dung |
|---|---|
| Phụ trách | cuongnqhe173086 (Claude thực hiện) |
| Iteration / Độ khó | Kiểm thử trước hội đồng / Trung bình |
| Trạng thái | ⚠️ Xong code, còn điều kiện (chưa commit; `run.ps1` cũ chưa viết lại cho PostgreSQL) |
| Tiến độ | 80% |
| Ước lượng WBS | Ngoài WBS |
| Bắt đầu / Kết thúc | 2026-11-25 18:26 / 2026-11-25 18:50 |
| Thời gian thực tế | Bắt đầu tính từ lúc bot gửi tin tiếp nhận (18:26) đến lúc xong tài liệu |
| Git | Chưa commit, chưa push |

## Tiến độ theo mốc
- [x] Đối chiếu tài liệu: SRS M-03 (Phone 9–11 digits), UC-37, GBR-10, nhiệm vụ điều phối (quyết định 20/11)
- [x] Code xong, build 0 lỗi (.NET 8)
- [x] Test tự động đạt: 216/216 unit test, 22/22 kiểm tra e2e `tools/e2e/bug-reports.js`
- [x] Chạy với phụ thuộc thật: API bản Release trên database PostgreSQL tạm `wpsm_verify` (Supabase), React build thật, Chrome
- [x] Có file giải thích: [bug-discord-1125.md](../explain/bug-discord-1125.md)

## Danh sách lỗi và kết luận
| # | Lỗi (người báo, ngày) | Có thật? | Cách xử lý |
|---|---|---|---|
| 1 | Admin sửa dịch vụ không chọn được "cần nhà cung cấp" (emaien, 22/11) | Có (I-024) | Thêm ô "Cần nhà cung cấp bên ngoài" vào A-03, nhãn trong bảng A-02, API nhận `requiresSupplier` |
| 2 | Tải minh chứng nhà cung cấp lên rồi không hủy/xóa được (emaien, 22–23/11) | Có (thiếu chức năng) | `DELETE /projects/{code}/suppliers/{id}`, use case `RemoveSupplierEvidence`; chỉ khi đã khóa dịch vụ và chưa tất toán; nút Xóa có hộp thoại xác nhận. Tệp gốc vẫn giữ trong kho tệp |
| 3a | Phiếu bộ phận: nhiệm vụ đã hoàn thành không hủy được (emaien, 22/11) | Có (thiếu chức năng) | Dùng chung cách sửa với 5b |
| 3b | PDF từng bộ phận chỉ có dịch vụ, không có nhiệm vụ (emaien, 22/11) | Có | PDF thêm mục "Nhiệm vụ cần hoàn thành" (hạn, tên, trạng thái, dấu * nếu cần minh chứng), tự sang trang |
| 4 | Khung "Lịch sử các phiên bản phiếu điều phối" trống dù đã chốt v2 (emaien, 22/11) | Có (I-023) | Trang đọc sai tên trường; sửa thành `history` |
| 5a | Nhiệm vụ ngày cưới bấm hoàn thành được trước ngày cưới (emaien, 22/11) | Không | Đúng thiết kế nhóm chốt 20/11: nhiệm vụ mở từ lúc khóa dịch vụ vì rạp, sân khấu dựng trước |
| 5b | Không hủy hoàn thành nhiệm vụ được (emaien, 22/11) | Có (thiếu chức năng) | `POST /projects/{code}/day-of/{taskId}/reopen`, use case `ReopenCoordinationTask`, `DayOfTask.Reopen()`; mục "Hủy hoàn thành (mở lại)" trong menu nhiệm vụ; minh chứng giữ nguyên |
| 5c | Không có nhiệm vụ theo dịch vụ khách đặt (emaien, 22/11) | Không | Dự án WPSM-2026-002 có 16 nhiệm vụ, có nhiệm vụ riêng cho áo dài, cổng hoa, ban nhạc...; tab Ngày cưới chỉ hiện 6 việc trong ngày, nhiệm vụ chuẩn bị ở "Điều phối theo bộ phận" |
| 6 | Tạo hợp đồng nhận số `1185028924` (tb_long, 24/11) | Có (I-026) | `PhonePolicy`: bắt đầu bằng 0, 10–11 số, đổi +84 thành 0; áp dụng tạo hợp đồng, tài khoản, hồ sơ |
| 7 | Khách đánh giá khi sự kiện chưa diễn ra (tb_long, 24/11) | Có trên code cũ (I-025) | Đã sửa sẵn trong commit `3fabe42` (24/11): chỉ đánh giá khi Đã tất toán / Đã đóng. Người test cần pull code mới |
| 8 | Chữ trong chi tiết khiếu nại hiện ký tự lạ (tb_long, 24/11) | Có | `ComplaintStatus/index.jsx:93` gõ nhầm "Thời점" (chữ Hàn) → "Thời gian gửi"; đã quét cả client không còn ký tự CJK |

## Nhật ký thực hiện
| Thời gian | Việc | Kết quả |
|---|---|---|
| 2026-11-25 18:26 | Bot "auto fix" gửi tin tiếp nhận 8 lỗi vào #sep-code | Tin `1575110342638309460` |
| 2026-11-25 | Tra cứu lịch sử dự án WPSM-2026-015 trong AuditLogs (chỉ đọc) | Đánh giá gửi 13:12 khi dự án còn điều chỉnh; code lúc đó chưa chặn |
| 2026-11-25 | Sửa 8 lỗi, thêm 16 unit test | Build 0 lỗi, 216/216 đạt |
| 2026-11-25 | Tạo database tạm `wpsm_verify`, chạy migration, API cổng 5310, `bug-reports.js` | Lần 1: 3 lỗi do test bấm nút trong lúc hộp thoại còn mở dần; sửa test, lần 2: 22/22 đạt |
| 2026-11-25 18:44 | Tắt API test, xóa `wpsm_verify` | Database chính `postgres` không bị sửa |

## Hướng giải quyết
| Vấn đề | Hướng đã cân nhắc | Hướng chọn và lý do |
|---|---|---|
| Xóa minh chứng nhà cung cấp | Xóa cả tệp trên Cloudinary / chỉ xóa dòng minh chứng | Chỉ xóa dòng; tệp giữ theo quy ước "tệp giữ mãi", nhật ký vẫn ghi ai xóa |
| Sửa dịch vụ không gửi `requiresSupplier` | Mặc định `false` / `null` = giữ nguyên | `null` giữ nguyên để client cũ hoặc API khác không vô tình tắt cờ |
| Mở lại nhiệm vụ quá hạn | Đặt ngay `Overdue` / đặt `Pending` | `Pending`, để job quá hạn đánh dấu lại và gửi cảnh báo như bình thường |
| Quy tắc số điện thoại | Giữ 9–11 số theo SRS / bắt buộc số 0 | Bắt buộc số 0 vì người test báo lỗi thật; DB CHECK 9–11 vẫn thỏa nên không cần migration. SRS M-03 cần sửa lại |
| Kiểm tra e2e | Sửa `run.ps1` cho PostgreSQL ngay / suite riêng | Suite riêng `bug-reports.js` trên database tạm; viết lại `run.ps1` là việc lớn, tách riêng |

## Vấn đề gặp phải
I-023, I-024, I-025, I-026, I-027.

## Kết quả kiểm tra
- `dotnet build WPSM.sln`: 0 lỗi. `dotnet test`: 216/216 (16 test mới ở `tests/Tests/BugReportFixTests.cs`).
- `tools/e2e/bug-reports.js` trên API Release + database `wpsm_verify`: 22/22, gồm click thật trong Chrome cho A-03, xóa minh chứng, mở lại nhiệm vụ, lịch sử phiếu, form tạo hợp đồng.
- Chưa chạy: `tools/e2e/run.ps1` (hỏng từ khi chuyển PostgreSQL, I-027). Lỗi 7 và 8 không có bước bấm bằng tài khoản khách trong e2e (seed không có mật khẩu khách); lỗi 7 có unit test `PostEventTests`, lỗi 8 kiểm tra trong bundle.

## Việc còn lại
- Commit khi người dùng yêu cầu.
- Sửa SRS M-03 (Phone: bắt đầu bằng 0, 10–11 chữ số) trong Report 3.
- Quyết định có xóa 4 đánh giá và hóa đơn tất toán của WPSM-2026-015 (tạo khi test trên code cũ, tất toán trước ngày cưới) trên database chung không.
- Viết lại `tools/e2e/run.ps1` cho PostgreSQL.
