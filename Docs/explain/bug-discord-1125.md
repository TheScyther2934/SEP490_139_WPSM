# [BUG-DISCORD-1125] Các sửa đổi sau đợt báo lỗi trên Discord (22–24/11/2026)

**Trạng thái:** ✅ Xong · **Ngày:** 2026-11-25 · **Tài liệu:** UC-37 (A-02, A-03), M-03 (field Phone), M-08 (minh chứng nhà cung cấp), M-11/M-13 (phiếu điều phối), M-17 (nhiệm vụ điều phối), UC-14/GBR-10, MSG10

## Chức năng này làm gì
- Admin bật/tắt "Cần nhà cung cấp bên ngoài" cho từng dịch vụ. Dịch vụ bật cờ sẽ xuất hiện ở tab Nhà cung cấp của mọi sự kiện có dịch vụ đó, được nhắc hạn và cảnh báo trước tất toán.
- Manager xóa minh chứng nhà cung cấp tải nhầm, và mở lại nhiệm vụ điều phối lỡ bấm hoàn thành.
- Phiếu điều phối hiện đúng lịch sử các phiên bản; PDF mỗi bộ phận có thêm danh sách nhiệm vụ.
- Số điện thoại phải là số Việt Nam: bắt đầu bằng 0, 10–11 chữ số.

## Luồng xử lý
```text
Browser (React) -> services/*.js -> API endpoint -> Use case (Application) -> Domain (Policy / entity) -> Store (Infrastructure) -> PostgreSQL
```
- Xóa minh chứng: `EvidenceItem` (nút Xóa) → `eventFlowService.removeEvidence` → `DELETE /projects/{code}/suppliers/{id}` → `RemoveSupplierEvidence` → `EventStagePolicy.EnsureCanRemoveEvidence` → `EventWorkStore.Remove` → trả lại bảng nhà cung cấp.
- Mở lại nhiệm vụ: `TaskCard` (menu) → `dayOfService.reopen` → `POST /projects/{code}/day-of/{taskId}/reopen` → `ReopenCoordinationTask` → `CoordinationTaskPolicy.EnsureOpen` + `DayOfTask.Reopen()`.

## Giải thích code
| File | Vai trò | Điểm cần hiểu |
|---|---|---|
| `src/Domain/Rules/PhonePolicy.cs` | Quy tắc số điện thoại dùng chung | `Normalize` bỏ ký tự không phải số và đổi tiền tố `84` (11–12 số) thành `0`; `Require` cho trường bắt buộc, `Optional` cho trường có thể trống. `EventBookingPolicy` và `AccountPolicy` gọi vào đây |
| `src/Client/src/utils/phone.js` | Bản giống hệt ở React | Form tạo hợp đồng và hộp thoại tài khoản báo lỗi trước khi gửi |
| `src/Domain/Entities/Configuration.Domain.cs` | `ServiceCatalogItem.Create/Edit` nhận `requiresSupplier` | Khi sửa, `null` nghĩa là giữ nguyên: request cũ không gửi trường này sẽ không tắt cờ |
| `src/Application/UseCases/EventStages/EventStageUseCases.cs` | `RemoveSupplierEvidence` | Tìm minh chứng theo cả `ProjectId` lẫn `EvidenceId` nên không xóa được minh chứng của sự kiện khác; không xóa tệp trong kho (tệp giữ mãi), nhật ký kiểm toán tự ghi thao tác xóa |
| `src/Domain/Entities/DayOfTask.Domain.cs` | `Reopen()` | Đưa về `Pending`, xóa `CompletedAt/CompletedBy/OverdueFlaggedAt`, giữ minh chứng. Không đặt thẳng `Overdue` mà để job quá hạn đánh dấu lại, nên cảnh báo đi đúng một đường |
| `src/Infrastructure/Persistence/Repositories/BeoRepository.cs` | PDF bộ phận | Nạp thêm `DayOfTasks`, in mục "NHIEM VU CAN HOAN THANH" theo hạn (giờ Việt Nam). `SimplePdf` tự chia 45 dòng một trang; PDF chỉ dùng font Courier chuẩn nên chữ được bỏ dấu |
| `src/Client/src/pages/manager/Beo/index.jsx` | Lịch sử phiên bản | Đọc `beo.history` (record C# `History`). Trước đây đọc `versionHistory` không tồn tại |
| `src/Client/src/pages/customer/ComplaintStatus/index.jsx` | Nhãn "Thời gian gửi" | Chữ "gian" từng bị gõ thành ký tự Hàn "점" |

## Quy tắc nghiệp vụ và quyền
- Xóa minh chứng và mở lại nhiệm vụ: chỉ Manager điều hành sự kiện (nhóm endpoint `manager`, lọc theo người điều hành GBR-12), chỉ khi sự kiện **Đã khóa** hoặc **Đã diễn ra**, không được sau tất toán.
- Hoàn thành nhiệm vụ trước ngày cưới vẫn được phép (quyết định 20/11: nhiệm vụ mở từ lúc khóa dịch vụ).
- Đánh giá dịch vụ (GBR-10): chỉ sau tất toán, đã có từ commit `3fabe42`.
- Số điện thoại chặt hơn SRS M-03 ("9–11 digits"); ràng buộc CHECK của database (9–11 số) vẫn thỏa nên không cần migration.

## Cách kiểm tra
```powershell
dotnet build WPSM.sln -v q --nologo
dotnet test WPSM.sln              # 216 test, có BugReportFixTests
# e2e trên database tạm (xem L-26): tạo wpsm_verify, dotnet ef database update --connection ..., chạy API Development
# (Smtp/Cloudinary để trống), rồi:
$env:REACT_BASE="http://localhost:5310"; $env:TEST_PW="<mật khẩu seed>"; node tools/e2e/bug-reports.js
```
Đã kiểm tra: lưu và giữ cờ nhà cung cấp; nhãn và ô chọn trong A-02/A-03; xóa minh chứng bằng click thật, xóa lại báo 404, sự kiện đã tất toán không có nút xóa; mở lại nhiệm vụ bằng click thật, mở lại nhiệm vụ chưa xong báo 400; lịch sử phiếu hiện phiên bản đã chốt; PDF bộ phận có danh sách nhiệm vụ; số `1185028924` bị API và form chặn; bundle không còn "Thời점".

## Hạn chế và việc còn lại
- Chưa có bước bấm bằng tài khoản khách trong e2e (đánh giá, khiếu nại) vì seed không đặt mật khẩu khách.
- `tools/e2e/run.ps1` cần viết lại cho PostgreSQL (I-027).
- SRS M-03 cần cập nhật quy tắc số điện thoại.
