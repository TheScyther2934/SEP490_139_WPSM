// Vietnamese labels of the audit log codes stored by the API.
export const ACTIONS = {
  Create: { label: "Thêm", color: "success" },
  Update: { label: "Sửa", color: "info" },
  Delete: { label: "Xóa", color: "error" },
  Login: { label: "Đăng nhập", color: "default" },
};

export const IMPACTS = {
  Project: "Sự kiện",
  Service: "Dịch vụ",
  Upload: "Upload",
  Account: "Tài khoản",
  Payment: "Thanh toán",
  Addon: "Hợp đồng phát sinh",
  Policy: "Chính sách",
  Complaint: "Khiếu nại & đánh giá",
  Exception: "Yêu cầu đặc biệt",
  Other: "Khác",
};

export const ROLES = { Admin: "Quản trị viên", Manager: "Manager", Customer: "Khách hàng", System: "Hệ thống" };

// Most frequent column names; anything else is shown as stored.
export const FIELDS = {
  Status: "Trạng thái", Amount: "Số tiền", PaymentMethod: "Hình thức", PaidDate: "Ngày thu", DueDate: "Hạn", RecordedBy: "Người ghi nhận",
  FullName: "Họ tên", Email: "Email", Phone: "Số điện thoại", Role: "Vai trò", PasswordHash: "Mật khẩu", MustChangePassword: "Buộc đổi mật khẩu",
  Name: "Tên", Category: "Nhóm", Unit: "Đơn vị", DefaultPrice: "Giá mặc định", MinPrice: "Giá tối thiểu", MaxPrice: "Giá tối đa",
  EffectiveFrom: "Hiệu lực từ", Value: "Giá trị", PolicyType: "Loại chính sách", Quantity: "Số lượng", UnitPrice: "Đơn giá",
  Description: "Mô tả", EventDate: "Ngày cưới", CurrentTotal: "Tổng hiện tại", BaseContractValue: "Giá trị lúc ký", DepositAmount: "Tiền đặt cọc", LockedAt: "Khóa dịch vụ lúc", CancelReason: "Lý do hủy", Content: "Nội dung", NoteType: "Loại ghi chú", ValueBefore: "Giá trị trước", ValueAfter: "Giá trị sau", SupplierName: "Nhà cung cấp", EvidenceType: "Loại minh chứng", ConfirmedAt: "Khách xác nhận lúc", SettingValue: "Giá trị cài đặt",
  ManagerId: "Manager", CustomerId: "Khách hàng", CompletedAt: "Hoàn thành lúc",
  CompletedBy: "Người hoàn thành", ImageFilePath: "Tệp ảnh", OriginalName: "Tên tệp", Reason: "Lý do", RemovedAt: "Gỡ lúc",
  RemovalReason: "Lý do gỡ", ResolutionNote: "Kết quả xử lý", EscalationNote: "Ghi chú chuyển Admin", DecisionNote: "Ghi chú quyết định",
  Title: "Tiêu đề", AmountDue: "Số tiền cần thu",
};

export const label = (map, key) => (typeof map[key] === "object" ? map[key]?.label : map[key]) ?? key;
