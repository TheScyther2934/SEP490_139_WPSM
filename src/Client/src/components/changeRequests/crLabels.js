// CR types stored in English (CK_CR_Type), shown in Vietnamese.
export const CR_TYPES = [
  { value: "Menu change", label: "Đổi thực đơn" },
  { value: "Decor change", label: "Đổi trang trí" },
  { value: "Table count", label: "Đổi số bàn" },
  { value: "Script edit", label: "Sửa kịch bản" },
  { value: "Other", label: "Khác" },
];
export const crTypeLabel = (value) => CR_TYPES.find((t) => t.value === value)?.label ?? value;

export const CR_STATUSES = [
  { value: "", label: "Tất cả" },
  { value: "Draft", label: "Nháp" },
  { value: "Negotiating", label: "Đang thương lượng" },
  { value: "Approved", label: "Đã duyệt" },
  { value: "Rejected", label: "Đã từ chối" },
];

const ACTIONS = {
  Created: "Tạo CR",
  Submitted: "Gửi thương lượng",
  DraftEdited: "Sửa bản nháp",
  CounterProposed: "Đề xuất lại",
  Commented: "Góp ý / đề nghị điều chỉnh",
  CustomerApproved: "Khách hàng duyệt",
  ManagerApproved: "Manager duyệt",
  Approved: "CR được duyệt",
  Rejected: "Từ chối",
  ExceptionRequested: "Yêu cầu ngoại lệ",
  ExceptionApproved: "Admin chấp thuận ngoại lệ",
  ExceptionRejected: "Admin từ chối ngoại lệ",
  BeoMarkedStale: "BEO bị đánh dấu lỗi thời",
  BeoDraftUpdated: "Cập nhật bản nháp BEO",
};
export const crActionLabel = (action) => ACTIONS[action] ?? action;
