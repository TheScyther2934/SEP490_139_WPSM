import { useRef } from "react";
import { Alert, Button, Card, CardContent, FormHelperText, MenuItem, Stack, TextField, Typography } from "@mui/material";
import UploadFileIcon from "@mui/icons-material/UploadFile";
import { money } from "../../../../utils/format";

// Stage 1 deposit (30% by default). Kept by the venue if the customer cancels.
export default function DepositSection({ form, errors, onChange, minimum, ratio, disabled }) {
  const fileRef = useRef(null);
  return (
    <Card>
      <CardContent>
        <Typography variant="h6">4. Tiền đặt cọc</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Đặt cọc {ratio ?? 30}% giá trị hợp đồng: tối thiểu {money(minimum)}. Phần còn lại thanh toán một lần khi tất toán sau ngày cưới.
        </Typography>
        <Alert severity="warning" sx={{ mb: 2 }}>Khách hủy sự kiện thì không hoàn tiền đặt cọc. Hãy nói rõ với khách trước khi ký.</Alert>
        <Stack spacing={2}>
          <TextField id="depositAmount" label="Số tiền đặt cọc đã thu (VND)" inputMode="numeric" value={form.depositAmount} disabled={disabled}
            onChange={(e) => onChange("depositAmount", e.target.value)} error={!!errors.depositAmount}
            helperText={errors.depositAmount ?? "Để trống sẽ lấy đúng mức tối thiểu."} />
          <TextField id="depositPaymentMethod" select label="Hình thức thanh toán" value={form.paymentMethod} disabled={disabled}
            onChange={(e) => onChange("paymentMethod", e.target.value)}>
            <MenuItem value="Bank">Chuyển khoản</MenuItem>
            <MenuItem value="Cash">Tiền mặt</MenuItem>
          </TextField>
          <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap", gap: 1 }}>
            <input ref={fileRef} id="depositReceiptFile" type="file" accept=".pdf,.jpg,.jpeg,.png" hidden
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
