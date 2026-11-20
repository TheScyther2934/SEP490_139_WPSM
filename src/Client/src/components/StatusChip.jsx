import { Chip } from "@mui/material";

// Status colours always come with a text label (SDS: colour is never the only signal).
// Event statuses follow the five stages: adjusting (stage 2), locked (3-4), held (5), settled, closed, cancelled.
const PROJECT = {
  Adjusting: { label: "Đang điều chỉnh", color: "info" },
  Locked: { label: "Đã chốt", color: "primary" },
  Completed: { label: "Đã tổ chức", color: "warning" },
  Settled: { label: "Đã tất toán", color: "success" },
  Closed: { label: "Đã đóng", color: "default" },
  Cancelled: { label: "Đã hủy", color: "error" },
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
// Addon contracts: billed only once the customer confirms.
const ADDON = {
  PendingCustomer: { label: "Chờ khách xác nhận", color: "warning" },
  Confirmed: { label: "Khách đã xác nhận", color: "success" },
  Cancelled: { label: "Đã hủy", color: "default" },
};
const INVOICE = {
  Issued: { label: "Chờ thanh toán", color: "warning" },
  Paid: { label: "Đã thanh toán", color: "success" },
};
const BEO = {
  None: { label: "Chưa tạo", color: "default" },
  Draft: { label: "Bản nháp", color: "info" },
  Frozen: { label: "Đã chốt", color: "success" },
  Stale: { label: "Lỗi thời", color: "error" },
};
const COMPLAINT = {
  Open: { label: "Đang mở", color: "warning" },
  Escalated: { label: "Đã chuyển Admin", color: "error" },
  Resolved: { label: "Đã giải quyết", color: "success" },
};
// FE-07: resource allocations.
const ALLOCATION = {
  Reserved: { label: "Đã giữ", color: "success" },
  PendingException: { label: "Chờ Admin duyệt", color: "warning" },
  "Exception Approved": { label: "Admin đã duyệt", color: "info" },
  Released: { label: "Đã thu hồi", color: "default" },
};
const KINDS = { project: PROJECT, task: TASK, exception: EXCEPTION, payment: PAYMENT, addon: ADDON, invoice: INVOICE, beo: BEO, complaint: COMPLAINT, allocation: ALLOCATION };

export const statusLabel = (kind, status) => KINDS[kind]?.[status]?.label ?? status;

export default function StatusChip({ kind, status, size = "small" }) {
  const style = KINDS[kind]?.[status] ?? { label: status || "—", color: "default" };
  return <Chip label={style.label} color={style.color} size={size} variant={style.color === "default" ? "outlined" : "filled"} />;
}
