import { PATHS } from "../../../routes/paths";

// Notifications written before the React client keep Razor links (/C03, /M04?id=...); map them to React routes.
const LEGACY = {
  C01: () => PATHS.customer.overview,
  C02: () => PATHS.customer.event,
  C03: () => PATHS.customer.event,
  C09: () => PATHS.customer.event,
  C10: () => PATHS.customer.payments,
  C11: () => PATHS.customer.livePhotos,
  M01: () => PATHS.manager.dashboard,
  M02: () => PATHS.manager.projects,
  M04: (id) => (id ? PATHS.manager.project(id) : PATHS.manager.projects),
  M23: () => PATHS.manager.overdueAlerts,
};

export function notificationTarget(link) {
  if (!link) return null;
  const match = /^\/([CM]\d{2})(?:\?id=([^&]+))?$/.exec(link);
  if (match) return LEGACY[match[1]]?.(match[2] && decodeURIComponent(match[2])) ?? null;
  return link.startsWith("/") ? link : null;
}

export const NOTIFICATION_TYPES = {
  "Stage reminder": { label: "Nhắc giai đoạn", color: "info" },
  Payment: { label: "Thanh toán", color: "success" },
  "Addon contract": { label: "Hợp đồng phát sinh", color: "primary" },
  BEO: { label: "Phiếu điều phối", color: "secondary" },
  Delegation: { label: "Ủy quyền", color: "default" },
  "Overdue alert": { label: "Quá hạn", color: "error" },
  Complaint: { label: "Khiếu nại", color: "warning" },
};
