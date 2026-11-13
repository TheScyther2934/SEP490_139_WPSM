import { CORE_COMBO } from "../../../utils/catalog";
import { money } from "../../../utils/format";

// Pure M-05 rules shared by the table, the summary and the save/freeze buttons.
export const frameFor = (priceFrames, catalogItemId) => priceFrames.find((f) => Number(f.catalogItemId) === Number(catalogItemId));

// First problem of one row, or null. MSG06 when the unit price is outside the PriceFrame in force (GBR-02).
export function lineError(row, priceFrames, catalog) {
  if (!String(row.description).trim()) return "Trường Diễn giải là bắt buộc.";
  if (!Number.isInteger(Number(row.quantity)) || Number(row.quantity) < 1) return "Số lượng phải là số nguyên dương.";
  const frame = frameFor(priceFrames, row.catalogItemId);
  if (!frame) return "Dịch vụ chưa có khung giá.";
  if (row.unitPrice < frame.minPrice || row.unitPrice > frame.maxPrice) return `Giá phải nằm trong khoảng ${money(frame.minPrice)} và ${money(frame.maxPrice)}.`;
  if (catalog.find((c) => Number(c.id) === Number(row.catalogItemId))?.active === false) return "Dịch vụ đã ngừng hoạt động.";
  return null;
}

export const missingCoreCombo = (rows) => CORE_COMBO.filter((category) => !rows.some((r) => r.category === category));
export const specTotal = (rows) => rows.reduce((sum, r) => sum + Number(r.quantity) * Number(r.unitPrice), 0);

// API body built from the editing rows: they are the truth while editing (L-13), not the last loaded specification.
export const toSpecificationBody = (rows) => {
  const catering = rows.find((r) => r.category === "Ẩm thực");
  return {
    summary: `${rows.length} hạng mục đặc tả`,
    guestCount: catering ? Number(catering.quantity) : 0,
    items: rows.map((r) => ({ catalogItemId: Number(r.catalogItemId), description: String(r.description).trim(), quantity: Number(r.quantity), unitPrice: Number(r.unitPrice) })),
  };
};
