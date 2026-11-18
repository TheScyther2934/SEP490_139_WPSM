import { Box, FormHelperText, IconButton, MenuItem, Stack, TextField, Typography } from "@mui/material";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import { money, signedMoney } from "../../utils/format";
import { lineTotal, specOf, rowIssue } from "./crMeta";

const digits = (text) => String(text ?? "").replace(/[^0-9]/g, "");

// One requested change inside C-06: pick a baseline Spec Item, then enter the new quantity/price.
// The current value stays read-only so the Customer compares against the frozen baseline (GBR-01).
export default function CrItemEditor({ form, row, index, rows, onChange, onRemove, showErrors = false }) {
  const spec = specOf(form, row);
  // An untouched row shows no error until the user tries to send or picks an item.
  const issue = form && (showErrors || row.specItemId) ? rowIssue(form, rows, row) : null;
  const used = rows.filter((r) => r.specItemId && r.specItemId !== row.specItemId).map((r) => Number(r.specItemId));
  const options = (form?.specItems ?? []).filter((s) => !used.includes(s.specItemId));
  const delta = spec && row.newQuantity !== "" && row.newUnitPrice !== ""
    ? lineTotal(row.newQuantity, row.newUnitPrice) - lineTotal(spec.quantity, spec.unitPrice)
    : 0;

  const pick = (specItemId) => {
    const picked = form?.specItems.find((s) => String(s.specItemId) === String(specItemId));
    onChange({
      specItemId: String(specItemId),
      newQuantity: picked ? String(picked.quantity) : "",
      newUnitPrice: picked ? String(picked.unitPrice) : "",
    });
  };

  return (
    <Box sx={{ border: 1, borderColor: issue ? "error.main" : "divider", borderRadius: 2, p: 2 }}>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
        <Typography variant="subtitle2" color="text.secondary">Hạng mục {index + 1}</Typography>
        <IconButton size="small" aria-label={`Xóa hạng mục ${index + 1}`} onClick={onRemove} disabled={rows.length === 1}>
          <DeleteOutlineIcon fontSize="small" />
        </IconButton>
      </Stack>
      <Stack spacing={1.5}>
        <TextField select id={`crItem-${index}`} label="Hạng mục đặc tả" value={row.specItemId} onChange={(e) => pick(e.target.value)}
          helperText={spec ? `${spec.category} · ${spec.description}` : "Chọn một hạng mục trong baseline đã chốt."}>
          {options.map((s) => (
            <MenuItem key={s.specItemId} value={String(s.specItemId)}>
              {s.name} – {s.quantity} {s.unit} × {money(s.unitPrice)}
            </MenuItem>
          ))}
        </TextField>

        <Box sx={{ display: "grid", gap: 1.5, gridTemplateColumns: { xs: "1fr", md: "1.2fr 1fr 1fr" } }}>
          <TextField label="Giá trị hiện tại" value={spec ? `${spec.quantity} ${spec.unit} × ${money(spec.unitPrice)}` : "—"}
            slotProps={{ htmlInput: { readOnly: true } }} helperText={spec ? `Thành tiền ${money(spec.quantity * spec.unitPrice)}` : " "} />
          <TextField id={`crQty-${index}`} label="Số lượng mới" inputMode="numeric" value={row.newQuantity} disabled={!row.specItemId}
            onChange={(e) => onChange({ newQuantity: digits(e.target.value) })}
            helperText={spec ? `Đơn vị: ${spec.unit}` : " "} />
          <TextField id={`crPrice-${index}`} label="Đơn giá mới (VND)" inputMode="numeric" value={row.newUnitPrice} disabled={!row.specItemId}
            onChange={(e) => onChange({ newUnitPrice: digits(e.target.value) })}
            helperText={spec ? `Khung giá ${money(spec.minPrice)} – ${money(spec.maxPrice)}` : " "} />
        </Box>

        <Stack direction="row" spacing={2} sx={{ justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1 }}>
          <Typography variant="body2" color="text.secondary">
            {spec ? `Thành tiền mới ${money(lineTotal(row.newQuantity, row.newUnitPrice))}` : "Chưa chọn hạng mục"}
          </Typography>
          <Typography variant="body2" fontWeight={600} color={delta > 0 ? "error.main" : delta < 0 ? "success.main" : "text.secondary"}>
            Chênh lệch {delta === 0 ? "0 ₫" : signedMoney(delta)}
          </Typography>
        </Stack>

        {issue && <FormHelperText error>{issue}</FormHelperText>}
      </Stack>
    </Box>
  );
}
