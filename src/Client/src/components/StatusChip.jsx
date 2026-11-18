import { Chip } from "@mui/material";

// Status colours always come with a text label (SDS: colour is never the only signal).
const PROJECT = {
  "Active Baseline": { label: "Đang soạn đặc tả", color: "info" },
  "Baseline Frozen": { label: "Đã chốt baseline", color: "primary" },
  "Event Completed": { label: "Đã tổ chức", color: "warning" },
  Settled: { label: "Đã quyết toán", color: "success" },
  Closed: { label: "Đã đóng", color: "default" },
};
const TASK = {
  Done: { label: "Đã hoàn thành", color: "success" },
  Overdue: { label: "Quá hạn", color: "error" },
  Pending: { label: "Chờ thực hiện", color: "warning" },
};
const EXCEPTION = {
  Pending: { label: "Chờ quyết định", color: "warning" },
  Approved: { label: "Đã chấp thuận", color: "success" },
  Rejected: { label: "Đã từ chối", color: "error" },
};
const PAYMENT = {
  Paid: { label: "Đã thu", color: "success" },
  Due: { label: "Đến hạn", color: "warning" },
  Upcoming: { label: "Chưa đến hạn", color: "default" },
  Overdue: { label: "Quá hạn", color: "error" },
};
// FE-05: CR tickets (C-05, C-07, M-07, M-08).
const CHANGE_REQUEST = {
  Draft: { label: "Bản nháp", color: "default" },
  Negotiating: { label: "Đang thương lượng", color: "warning" },
  Approved: { label: "Đã duyệt", color: "success" },
  Rejected: { label: "Đã từ chối", color: "error" },
};
const BEO = {
  None: { label: "Chưa tạo", color: "default" },
  Draft: { label: "Bản nháp", color: "info" },
  Frozen: { label: "Đã đóng băng", color: "success" },
  Stale: { label: "Lỗi thời", color: "error" },
};
const COMPLAINT = {
  Open: { label: "Đang mở", color: "warning" },
  Escalated: { label: "Đã chuyển Admin", color: "error" },
  Resolved: { label: "Đã giải quyết", color: "success" },
};
// FE-07: resource allocations (M-21, M-22).
const ALLOCATION = {
  Reserved: { label: "Đã giữ", color: "success" },
  PendingException: { label: "Chờ Admin ngoại lệ", color: "warning" },
  "Exception Approved": { label: "Ngoại lệ đã duyệt", color: "info" },
  Released: { label: "Đã thu hồi", color: "default" },
};
const KINDS = { project: PROJECT, task: TASK, exception: EXCEPTION, payment: PAYMENT, cr: CHANGE_REQUEST, beo: BEO, complaint: COMPLAINT, allocation: ALLOCATION };

export const statusLabel = (kind, status) => KINDS[kind]?.[status]?.label ?? status;

export default function StatusChip({ kind, status, size = "small" }) {
  const style = KINDS[kind]?.[status] ?? { label: status || "—", color: "default" };
  return <Chip label={style.label} color={style.color} size={size} variant={style.color === "default" ? "outlined" : "filled"} />;
}
