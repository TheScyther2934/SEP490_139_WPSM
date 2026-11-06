import { Button, IconButton, MenuItem, Stack, TextField, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import { money, moneyInput } from "../../utils/format";

// UC-08: pick current Spec Items and enter the new quantity / unit price.
// `lines` = [{ specItemId, newQuantity, newUnitPrice }]; each Spec Item at most once per CR.
export function priceError(spec, price) {
  if (!spec) return "";
  if (spec.minPrice != null && (price < spec.minPrice || price > spec.maxPrice))
    return `Đơn giá phải nằm trong khoảng ${money(spec.minPrice)} – ${money(spec.maxPrice)} (MSG06).`;
  return "";
}

export default function CrItemsEditor({ specItems, lines, onChange, showErrors }) {
  const byId = Object.fromEntries(specItems.map((s) => [s.specItemId, s]));
  const update = (index, patch) => onChange(lines.map((line, i) => (i === index ? { ...line, ...patch } : line)));
  const pick = (index, specItemId) => {
    const spec = byId[specItemId];
    update(index, { specItemId, newQuantity: spec.quantity, newUnitPrice: spec.unitPrice });
  };
  const used = new Set(lines.map((l) => l.specItemId));
  const free = specItems.filter((s) => !used.has(s.specItemId));

  return (
    <Stack spacing={2} id="crItems">
      {lines.map((line, index) => {
        const spec = byId[line.specItemId];
        const error = priceError(spec, line.newUnitPrice);
        return (
          <Stack key={index} direction={{ xs: "column", md: "row" }} spacing={2} sx={{ alignItems: { md: "flex-start" } }} data-line={index}>
            <TextField id={`crSpecItem-${index}`} select label="Hạng mục" value={line.specItemId ?? ""} required sx={{ minWidth: 240, flex: 2 }}
              onChange={(e) => pick(index, Number(e.target.value))} error={showErrors && !line.specItemId}
              helperText={spec ? `Hiện tại: ${spec.quantity} ${spec.unit} × ${money(spec.unitPrice)}` : showErrors ? "Trường Hạng mục là bắt buộc." : " "}
              slotProps={{ htmlInput: { "data-field": "specItem" } }}>
              {specItems.filter((s) => s.specItemId === line.specItemId || !used.has(s.specItemId)).map((s) => (
                <MenuItem key={s.specItemId} value={s.specItemId}>{s.category} · {s.name}</MenuItem>
              ))}
            </TextField>
            <TextField label="Số lượng mới" type="number" required value={line.newQuantity ?? ""} sx={{ width: { md: 140 } }}
              onChange={(e) => update(index, { newQuantity: Number(e.target.value) })}
              error={showErrors && !(line.newQuantity > 0)} helperText={showErrors && !(line.newQuantity > 0) ? "Phải lớn hơn 0." : " "}
              slotProps={{ htmlInput: { min: 1, "data-field": "newQuantity" } }} />
            <TextField label="Đơn giá mới (VND)" required value={line.newUnitPrice ? line.newUnitPrice.toLocaleString("vi-VN") : ""} sx={{ flex: 1 }}
              onChange={(e) => update(index, { newUnitPrice: moneyInput(e.target.value) })} error={!!error}
              helperText={error || (spec?.minPrice != null ? `Khung giá ${money(spec.minPrice)} – ${money(spec.maxPrice)}` : " ")}
              slotProps={{ htmlInput: { inputMode: "numeric", "data-field": "newUnitPrice" } }} />
            <IconButton aria-label="Bỏ hạng mục" onClick={() => onChange(lines.filter((_, i) => i !== index))} disabled={lines.length === 1} sx={{ mt: 1 }}>
              <DeleteIcon />
            </IconButton>
          </Stack>
        );
      })}
      <Stack direction="row" spacing={2} sx={{ alignItems: "center" }}>
        <Button startIcon={<AddIcon />} onClick={() => onChange([...lines, { specItemId: null, newQuantity: 1, newUnitPrice: 0 }])} disabled={free.length === 0}>
          Thêm hạng mục
        </Button>
        {specItems.length === 0 && <Typography color="text.secondary">Dự án chưa có hạng mục đặc tả.</Typography>}
      </Stack>
    </Stack>
  );
}

export const linesValid = (lines, specItems) => {
  const byId = Object.fromEntries(specItems.map((s) => [s.specItemId, s]));
  return lines.length > 0 && lines.every((l) => l.specItemId && l.newQuantity > 0 && !priceError(byId[l.specItemId], l.newUnitPrice));
};
