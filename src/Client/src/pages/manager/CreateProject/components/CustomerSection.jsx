import { Alert, Card, CardContent, Stack, TextField, Typography } from "@mui/material";

export default function CustomerSection({ form, errors, onChange, disabled }) {
  const field = (key, label, extra = {}) => (
    <TextField id={key} label={label} value={form[key]} onChange={(e) => onChange(key, e.target.value)}
      error={!!errors[key]} helperText={errors[key]} disabled={disabled} fullWidth {...extra} />
  );
  return (
    <Card>
      <CardContent>
        <Typography variant="h6">1. Thông tin khách hàng</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>Người đại diện ký kết và nhận tài khoản dịch vụ.</Typography>
        <Stack spacing={2}>
          {field("coupleName", "Họ và tên đại diện")}
          {field("customerEmail", "Email", { type: "email" })}
          {field("customerPhone", "Số điện thoại", { inputMode: "tel" })}
          <Alert severity="info">Hệ thống tạo tài khoản Khách hàng và gửi mật khẩu tạm qua email sau khi khởi tạo.</Alert>
        </Stack>
      </CardContent>
    </Card>
  );
}
