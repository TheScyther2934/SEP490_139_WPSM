import { money, signedMoney } from "../../utils/format";

// Labels and arithmetic shared by the FE-05 screens. The server recalculates every figure on save;
// these helpers only drive the live estimate C-06 shows before the ticket is sent (GBR-03).

export const CR_TYPES = [
  { value: "Menu change", label: "Đổi thực đơn" },
  { value: "Decor change", label: "Đổi trang trí" },
  { value: "Table count", label: "Điều chỉnh số bàn" },
  { value: "Script edit", label: "Sửa kịch bản" },
  { value: "Other", label: "Khác" },
];

export const typeLabel = (value) => CR_TYPES.find((t) => t.value === value)?.label ?? value ?? "—";

export const CR_STATUS_LABELS = {
  Draft: "Bản nháp",
  Negotiating: "Đang thương lượng",
  Approved: "Đã duyệt",
  Rejected: "Đã từ chối",
};

export const statusLabel = (status) => CR_STATUS_LABELS[status] ?? status ?? "—";

// CR History actions recorded by the API (append-only, GBR-13).
export const HISTORY_LABELS = {
  Created: "Khởi tạo bản nháp",
  Updated: "Cập nhật bản nháp",
  Submitted: "Gửi yêu cầu thương lượng",
  CounterProposal: "Đề xuất đối ứng",
  ManagerApproved: "Quản lý phê duyệt",
  CustomerApproved: "Khách hàng phê duyệt",
  Approved: "Hoàn tất – cả hai bên đồng thuận",
  Rejected: "Từ chối yêu cầu",
  ExceptionRequested: "Đề nghị Admin miễn phí phạt",
  AddendumGenerated: "Tạo phụ lục hợp đồng",
};

export const historyLabel = (action) => HISTORY_LABELS[action] ?? action;

export const isOpen = (status) => status === "Draft" || status === "Negotiating";

export const amount = (value) => Number(value) || 0;

// A zero delta is informative ("no cost change"), so it keeps the plain currency form.
export const signedAmount = (value) => (amount(value) === 0 ? "0 ₫" : signedMoney(amount(value)));

export const lineTotal = (quantity, unitPrice) => amount(quantity) * amount(unitPrice);

export const penaltyPercent = (rate) =>
  `${amount(rate).toLocaleString("vi-VN", { maximumFractionDigits: 2 })} %`;

// The baseline row for a CR line: quantity/unit price come from the frozen specification.
export const specOf = (form, row) =>
  form?.specItems.find((s) => String(s.specItemId) === String(row.specItemId)) ?? null;

export const lineDelta = (form, row) => {
  const spec = specOf(form, row);
  if (!spec || row.newQuantity === "" || row.newUnitPrice === "") return 0;
  return lineTotal(row.newQuantity, row.newUnitPrice) - lineTotal(spec.quantity, spec.unitPrice);
};

// GBR-03 preview: price delta + |delta| × T-day rate. Mirrors ChangeRequestRepository.RecalculateAsync.
export function previewCost(form, rows) {
  const priceDelta = rows.reduce((sum, row) => sum + lineDelta(form, row), 0);
  const penaltyRate = amount(form?.applicablePenaltyRate);
  const penaltyFee = penaltyRate > 0 ? Math.round((Math.abs(priceDelta) * penaltyRate) / 100 * 100) / 100 : 0;
  const currentTotal = amount(form?.currentTotal);
  return {
    priceDelta,
    penaltyRate,
    penaltyFee,
    totalImpact: priceDelta + penaltyFee,
    currentTotal,
    projectedTotal: currentTotal + priceDelta + penaltyFee,
    inPenaltyWindow: penaltyRate > 0,
  };
}

// Returns the SRS message for an invalid line, or null when the line can be sent.
export function rowIssue(form, rows, row) {
  if (!row.specItemId) return "Chọn hạng mục đặc tả cần thay đổi.";
  if (rows.filter((r) => r.specItemId && r.specItemId === row.specItemId).length > 1) return "Mỗi hạng mục chỉ được chọn một lần trong yêu cầu.";
  const spec = specOf(form, row);
  if (!spec) return "Hạng mục không thuộc baseline hiện tại của dự án.";
  const quantity = Number(row.newQuantity);
  if (row.newQuantity === "" || !Number.isInteger(quantity) || quantity < 0) return "Số lượng mới phải là số nguyên không âm.";
  const price = Number(row.newUnitPrice);
  if (row.newUnitPrice === "" || !Number.isFinite(price) || price < 0) return "Đơn giá mới không được âm.";
  if (!spec.hasPriceFrame) return `${spec.name} chưa có PriceFrame hiện hành nên chưa thể đổi giá (GBR-02).`;
  if (price < spec.minPrice || price > spec.maxPrice)
    return `Đơn giá phải nằm trong khoảng ${money(spec.minPrice)} – ${money(spec.maxPrice)} (MSG06).`;
  return null;
}

export const changedLines = (form, rows) =>
  rows.filter((row) => {
    const spec = specOf(form, row);
    return spec && (amount(row.newQuantity) !== spec.quantity || amount(row.newUnitPrice) !== spec.unitPrice);
  }).length;

export const toCommand = (rows) =>
  rows.map((row) => ({
    specItemId: Number(row.specItemId),
    newQuantity: Number(row.newQuantity),
    newUnitPrice: Number(row.newUnitPrice),
  }));

export const emptyRow = () => ({ specItemId: "", newQuantity: "", newUnitPrice: "" });
