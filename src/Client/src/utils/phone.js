// Same rule as Domain/Rules/PhonePolicy.cs: digits only, a +84 / 84 prefix becomes 0,
// then it must start with 0 and have 10 digits (mobile) or 11 (old landline).
export const PHONE_MESSAGE = "Số điện thoại phải bắt đầu bằng số 0 và có 10–11 chữ số (ví dụ 0912345678).";

export function normalizePhone(value) {
  let digits = String(value ?? "").replace(/\D/g, "");
  if (digits.startsWith("84") && (digits.length === 11 || digits.length === 12)) digits = "0" + digits.slice(2);
  return digits;
}

export const isValidPhone = (value) => /^0\d{9,10}$/.test(normalizePhone(value));
