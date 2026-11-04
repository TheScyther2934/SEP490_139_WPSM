import { isoDate, money, moneyInput } from "../../../utils/format";

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
export const RECEIPT_MAX_BYTES = 10 * 1024 * 1024;

export const requiredNrd = (form, policy) =>
  policy?.nrdRatio == null ? null : Math.round((moneyInput(form.baseValue) * policy.nrdRatio) / 100);

export function validateReceipt(file) {
  if (!file) return null;
  if (file.size <= 0 || file.size > RECEIPT_MAX_BYTES || !/\.(pdf|jpe?g|png)$/i.test(file.name)) return "Biên nhận phải là PDF/JPG/PNG, tối đa 10 MB.";
  return null;
}

// M-03 field rules (SRS 2.2). The receipt is required for bank transfers, as the API expects.
export function validateProject(form, policy, slots) {
  const errors = {};
  if (!form.coupleName.trim()) errors.coupleName = "Trường Họ và tên là bắt buộc.";
  else if (form.coupleName.trim().length > 100) errors.coupleName = "Họ và tên tối đa 100 ký tự.";
  if (!form.customerEmail.trim()) errors.customerEmail = "Trường Email là bắt buộc.";
  else if (!EMAIL.test(form.customerEmail.trim())) errors.customerEmail = "Email không đúng định dạng.";
  if (!form.customerPhone.trim()) errors.customerPhone = "Trường Số điện thoại là bắt buộc.";
  else if (!/^\d{9,11}$/.test(form.customerPhone.trim())) errors.customerPhone = "Số điện thoại gồm 9–11 chữ số.";
  if (!form.hall) errors.hall = "Trường Sảnh là bắt buộc.";
  if (!form.eventDate) errors.eventDate = "Trường Ngày tổ chức là bắt buộc.";
  else if (form.eventDate <= isoDate(new Date())) errors.eventDate = "Ngày tổ chức phải là ngày trong tương lai.";
  if (!form.slot) errors.slot = "Vui lòng chọn ca tiệc còn trống.";
  else if (!slots.some((s) => s.slotName === form.slot && s.available)) errors.slot = "Ca tiệc này đã được đặt vào ngày đã chọn.";
  const base = moneyInput(form.baseValue);
  const nrd = moneyInput(form.nrdAmount);
  const minimum = requiredNrd(form, policy);
  if (base <= 0) errors.baseValue = "Giá trị hợp đồng phải lớn hơn 0.";
  if (minimum == null) errors.nrdAmount = "Chưa tải được chính sách NRD. Vui lòng tải lại trang.";
  else if (nrd < minimum) errors.nrdAmount = `NRD cần tối thiểu ${money(minimum)}.`;
  else if (nrd > base) errors.nrdAmount = "NRD không được vượt giá trị hợp đồng.";
  if (form.paymentMethod === "Bank" && !form.receipt) errors.receipt = "Vui lòng chọn biên nhận NRD.";
  else if (validateReceipt(form.receipt)) errors.receipt = validateReceipt(form.receipt);
  return errors;
}
