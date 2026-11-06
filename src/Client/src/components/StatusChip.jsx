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
const KINDS = { project: PROJECT, task: TASK, exception: EXCEPTION, payment: PAYMENT, cr: CHANGE_REQUEST };

export const statusLabel = (kind, status) => KINDS[kind]?.[status]?.label ?? status;

export default function StatusChip({ kind, status, size = "small" }) {
  const style = KINDS[kind]?.[status] ?? { label: status || "—", color: "default" };
  return <Chip label={style.label} color={style.color} size={size} variant={style.color === "default" ? "outlined" : "filled"} />;
}
