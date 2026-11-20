import { isoDate, money, moneyInput } from "../../../utils/format";

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
export const RECEIPT_MAX_BYTES = 10 * 1024 * 1024;

const addDays = (days) => { const d = new Date(); d.setDate(d.getDate() + days); return isoDate(d); };
// Stage 1: the wedding must be between BookingMinDays and BookingMaxDays away (15–90 by default).
export const bookingWindow = (policy) => ({ min: addDays(policy?.bookingMinDays ?? 15), max: addDays(policy?.bookingMaxDays ?? 90) });

export const contractValue = (lines) => lines.reduce((sum, l) => sum + Number(l.quantity || 0) * Number(l.unitPrice || 0), 0);
export const requiredDeposit = (lines, policy) => Math.round((contractValue(lines) * (policy?.depositRatio ?? 30)) / 100);

export function validateReceipt(file) {
  if (!file) return null;
  if (file.size <= 0 || file.size > RECEIPT_MAX_BYTES || !/\.(pdf|jpe?g|png|heic|heif|webp|avif)$/i.test(file.name))
    return "Biên nhận phải là PDF hoặc ảnh (JPG, PNG, HEIC của iPhone, WebP), tối đa 10 MB.";
  return null;
}

export const lineProblem = (line, frames) => {
  if (!Number.isInteger(Number(line.quantity)) || Number(line.quantity) < 1) return "Số lượng phải là số nguyên dương.";
  const frame = frames.find((f) => Number(f.catalogItemId) === Number(line.catalogItemId));
  if (frame && (line.unitPrice < frame.minPrice || line.unitPrice > frame.maxPrice)) return `Đơn giá trong khoảng ${money(frame.minPrice)} – ${money(frame.maxPrice)}.`;
  return null;
};

// New event (stage 1): customer, hall / date / slot inside the booking window, chosen services, deposit and receipt.
export function validateProject(form, policy, slots, frames) {
  const errors = {};
  if (!form.coupleName.trim()) errors.coupleName = "Trường Họ và tên là bắt buộc.";
  else if (form.coupleName.trim().length > 100) errors.coupleName = "Họ và tên tối đa 100 ký tự.";
  if (!form.customerEmail.trim()) errors.customerEmail = "Trường Email là bắt buộc.";
  else if (!EMAIL.test(form.customerEmail.trim())) errors.customerEmail = "Email không đúng định dạng.";
  if (!form.customerPhone.trim()) errors.customerPhone = "Trường Số điện thoại là bắt buộc.";
  else if (!/^\d{9,11}$/.test(form.customerPhone.trim())) errors.customerPhone = "Số điện thoại gồm 9–11 chữ số.";
  if (!form.hall) errors.hall = "Trường Sảnh là bắt buộc.";
  const window = bookingWindow(policy);
  if (!form.eventDate) errors.eventDate = "Trường Ngày cưới là bắt buộc.";
  else if (form.eventDate < window.min) errors.eventDate = `Ngày cưới phải cách hôm nay ít nhất ${policy?.bookingMinDays ?? 15} ngày.`;
  else if (form.eventDate > window.max) errors.eventDate = `Chỉ nhận ký hợp đồng trước ngày cưới tối đa ${policy?.bookingMaxDays ?? 90} ngày.`;
  if (!form.slot) errors.slot = "Vui lòng chọn ca tiệc còn trống.";
  else if (!slots.some((s) => s.slotName === form.slot && s.available)) errors.slot = "Ca tiệc này đã được đặt vào ngày đã chọn.";
  if (form.lines.length === 0) errors.lines = "Hãy chọn các dịch vụ khách đặt.";
  else if (form.lines.some((l) => lineProblem(l, frames))) errors.lines = "Có dòng dịch vụ chưa hợp lệ.";
  const value = contractValue(form.lines);
  const minimum = requiredDeposit(form.lines, policy);
  const deposit = moneyInput(form.depositAmount);
  if (value <= 0) errors.lines = errors.lines ?? "Giá trị hợp đồng phải lớn hơn 0.";
  else if (deposit < minimum) errors.depositAmount = `Tiền đặt cọc tối thiểu ${money(minimum)}.`;
  else if (deposit > value) errors.depositAmount = "Tiền đặt cọc không được vượt giá trị hợp đồng.";
  if (form.paymentMethod === "Bank" && !form.receipt) errors.receipt = "Vui lòng chọn biên nhận chuyển khoản.";
  else if (validateReceipt(form.receipt)) errors.receipt = validateReceipt(form.receipt);
  return errors;
}
