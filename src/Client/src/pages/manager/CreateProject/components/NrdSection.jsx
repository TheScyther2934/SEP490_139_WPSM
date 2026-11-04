import { useRef } from "react";
import { Button, Card, CardContent, FormHelperText, MenuItem, Stack, TextField, Typography } from "@mui/material";
import UploadFileIcon from "@mui/icons-material/UploadFile";
import { money } from "../../../../utils/format";

export default function NrdSection({ form, errors, onChange, minimum, ratio, disabled }) {
  const fileRef = useRef(null);
  return (
    <Card>
      <CardContent>
        <Typography variant="h6">3. Đặt cọc NRD và chứng từ</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          NRD tối thiểu {ratio == null ? "(đang tải chính sách)" : `${ratio}% · ${money(minimum ?? 0)}`}
        </Typography>
        <Stack spacing={2}>
          <TextField id="nrdAmount" label="Số tiền NRD đã thu (VND)" inputMode="numeric" value={form.nrdAmount} disabled={disabled}
            onChange={(e) => onChange("nrdAmount", e.target.value)} error={!!errors.nrdAmount} helperText={errors.nrdAmount} />
          <TextField id="nrdPaymentMethod" select label="Hình thức thanh toán" value={form.paymentMethod} disabled={disabled}
            onChange={(e) => onChange("paymentMethod", e.target.value)}>
            <MenuItem value="Bank">Chuyển khoản</MenuItem>
            <MenuItem value="Cash">Tiền mặt</MenuItem>
          </TextField>
          <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap", gap: 1 }}>
            <input ref={fileRef} id="nrdReceiptFile" type="file" accept=".pdf,.jpg,.jpeg,.png" hidden
              onChange={(e) => onChange("receipt", e.target.files?.[0] ?? null)} />
            <Button variant="outlined" startIcon={<UploadFileIcon />} disabled={disabled} onClick={() => fileRef.current?.click()}>
              Chọn biên nhận{form.paymentMethod === "Bank" ? " *" : ""}
            </Button>
            {form.receipt && (
              <>
                <Typography variant="body2">{form.receipt.name} · {(form.receipt.size / 1024 / 1024).toFixed(2)} MB</Typography>
                <Button size="small" color="inherit" disabled={disabled} onClick={() => { if (fileRef.current) fileRef.current.value = ""; onChange("receipt", null); }}>Bỏ chọn</Button>
              </>
            )}
          </Stack>
          <FormHelperText error={!!errors.receipt}>{errors.receipt ?? "PDF, JPG hoặc PNG, tối đa 10 MB."}</FormHelperText>
        </Stack>
      </CardContent>
    </Card>
  );
}
