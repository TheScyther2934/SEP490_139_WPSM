import { money } from "../../../utils/format";

// Pure rules shared by the service table, the summary and the save button.
export const frameFor = (priceFrames, catalogItemId) => priceFrames.find((f) => Number(f.catalogItemId) === Number(catalogItemId));

// First problem of one row, or null. The unit price must stay inside the price frame in force.
export function lineError(row, priceFrames, catalog, isNew = true) {
  if (!String(row.description).trim()) return "Trường Diễn giải là bắt buộc.";
  if (!Number.isInteger(Number(row.quantity)) || Number(row.quantity) < 1) return "Số lượng phải là số nguyên dương.";
  const frame = frameFor(priceFrames, row.catalogItemId);
  if (frame && (row.unitPrice < frame.minPrice || row.unitPrice > frame.maxPrice)) return `Giá phải nằm trong khoảng ${money(frame.minPrice)} và ${money(frame.maxPrice)}.`;
  if (isNew && catalog.find((c) => Number(c.id) === Number(row.catalogItemId))?.active === false) return "Dịch vụ đã ngừng hoạt động.";
  return null;
}

export const specTotal = (rows) => rows.reduce((sum, r) => sum + Number(r.quantity) * Number(r.unitPrice), 0);

// API body built from the editing rows plus the required note of the adjustment.
export const toSpecificationBody = (rows, note, noteType) => {
  const catering = rows.find((r) => r.category === "Ẩm thực");
  return {
    summary: `${rows.length} dịch vụ`,
    guestCount: catering ? Number(catering.quantity) : 0,
    note,
    noteType,
    items: rows.map((r) => ({ catalogItemId: Number(r.catalogItemId), description: String(r.description).trim(), quantity: Number(r.quantity), unitPrice: Number(r.unitPrice) })),
  };
};
