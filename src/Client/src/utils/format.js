const number = new Intl.NumberFormat("vi-VN");

export const money = (value) => `${number.format(Number(value) || 0)} ₫`;
export const signedMoney = (value) => `${value > 0 ? "+" : ""}${money(value)}`;
export const shortMoney = (value) => {
  const v = Number(value) || 0;
  if (Math.abs(v) >= 1e9) return `${(v / 1e9).toLocaleString("vi-VN", { maximumFractionDigits: 1 })} tỷ`;
  if (Math.abs(v) >= 1e6) return `${(v / 1e6).toLocaleString("vi-VN", { maximumFractionDigits: 1 })} tr`;
  return v.toLocaleString("vi-VN");
};
export const percent = (value) => `${(Number(value) || 0).toLocaleString("vi-VN")} %`;
// Money inputs accept "12.000.000" as typed by users; keep only digits.
export const moneyInput = (text) => Number(String(text ?? "").replace(/[^0-9]/g, "")) || 0;

// API DateOnly values come as "YYYY-MM-DD"; parse them as local dates, not UTC.
const localDate = (value) => new Date(`${String(value).slice(0, 10)}T00:00:00`);
export const dateVi = (value) => (value ? new Intl.DateTimeFormat("vi-VN").format(localDate(value)) : "—");
// For UTC instants (CreatedAt/ApprovedAt) the browser converts to the local (UTC+7) calendar day.
export const dayVi = (value) => (value ? new Intl.DateTimeFormat("vi-VN").format(new Date(value)) : "—");
export const dateTimeVi = (value) =>
  value ? new Intl.DateTimeFormat("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" }).format(new Date(value)) : "—";
export const timeVi = (value) => new Intl.DateTimeFormat("vi-VN", { hour: "2-digit", minute: "2-digit" }).format(new Date(value));
export const weekdayVi = (value) => (value ? new Intl.DateTimeFormat("vi-VN", { weekday: "long" }).format(localDate(value)) : "");
export const isoDate = (d) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
export const monthVi = (month) => `${month.slice(5)}/${month.slice(0, 4)}`;

export const relativeTimeVi = (isoString) => {
  if (!isoString) return "";
  const date = new Date(isoString);
  const now = new Date();
  const diffMs = now - date;
  if (diffMs < 0) return dateTimeVi(isoString);
  const diffSec = Math.floor(diffMs / 1000);
  if (diffSec < 60) return "Vừa xong";
  const diffMin = Math.floor(diffSec / 60);
  if (diffMin < 60) return `${diffMin} phút trước`;
  const diffHours = Math.floor(diffMin / 60);
  if (diffHours < 24) return `${diffHours} giờ trước`;
  const diffDays = Math.floor(diffHours / 24);
  if (diffDays < 7) return `${diffDays} ngày trước`;
  return dateTimeVi(isoString);
};

export const daysUntil = (value) => {
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  return Math.ceil((localDate(value).getTime() - today.getTime()) / 86400000);
};
export const countdownLabel = (value) => {
  const days = daysUntil(value);
  return days > 0 ? `T-${days}` : days === 0 ? "Hôm nay" : "Đã diễn ra";
};

export const slotLabel = (slot) => (slot === "Evening" || slot === "Tối" ? "Tối" : "Trưa");

export const roleLabel = (role) => ({ Admin: "Quản trị viên", Manager: "Quản lý vận hành", Customer: "Khách hàng" })[role] ?? role;

export const initials = (name) =>
  (name || "WPSM").split(" ").filter(Boolean).slice(-2).map((w) => w[0]).join("").toUpperCase();

// API errors carry { message } (Vietnamese); fall back to a status-based text.
export const errorMessage = (error) => {
  const data = error?.response?.data;
  if (data?.message) return data.message;
  if (data?.error) return data.error;
  const status = error?.response?.status;
  if (status === 403) return "Bạn không có quyền thực hiện thao tác này.";
  if (status === 404) return "Không tìm thấy dữ liệu.";
  if (status === 409) return "Dữ liệu đã thay đổi. Vui lòng tải lại trang.";
  if (status === 429) return "Bạn thử quá nhiều lần. Vui lòng đợi một phút rồi thử lại.";
  if (status >= 500) return "Máy chủ gặp lỗi. Vui lòng thử lại sau.";
  if (!error?.response) return "Không kết nối được WPSM API. Hãy kiểm tra API đang chạy.";
  return error?.message || "Có lỗi xảy ra.";
};
