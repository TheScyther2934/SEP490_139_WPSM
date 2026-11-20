import { Link as RouterLink } from "react-router-dom";
import { Alert, AlertTitle, Box, Button, Stack, Typography } from "@mui/material";
import { PATHS } from "../../../../routes/paths";

const EMAIL_NOTE = {
  Sent: "Email tài khoản đã được gửi.",
  Pending: "Email tài khoản đang chờ gửi.",
  NotConfigured: "Máy chủ chưa cấu hình gửi email nên khách sẽ không nhận được email. Vui lòng chuyển thông tin đăng nhập cho khách.",
  Saved: "Máy chủ chưa cấu hình gửi email nên khách không nhận được email. Vui lòng chuyển thông tin đăng nhập cho khách.",
  Failed: "Gửi email chưa thành công. Vui lòng chuyển thông tin đăng nhập cho khách.",
  ExistingAccount: "Khách cũ: dùng lại tài khoản đã có.",
};

// The temporary password is shown once, only when the API returns it (development without SMTP).
export default function SuccessPanel({ project, email, temporaryPassword, emailStatus }) {
  return (
    <Alert id="createProjectSuccess" severity="success" sx={{ mb: 3 }}>
      <AlertTitle>Đã tạo {project.id}</AlertTitle>
      <Typography>{EMAIL_NOTE[emailStatus] ?? ""}</Typography>
      {temporaryPassword && (
        <Box sx={{ mt: 1 }}>
          <Typography>Email: {email}</Typography>
          <Typography>Mật khẩu tạm: <Box component="code" sx={{ fontWeight: 700 }}>{temporaryPassword}</Box></Typography>
          <Typography variant="body2">Vui lòng lưu thông tin này trước khi rời trang. Khách phải đổi mật khẩu khi đăng nhập lần đầu.</Typography>
        </Box>
      )}
      <Stack direction="row" spacing={1} sx={{ mt: 2 }}>
        <Button variant="contained" component={RouterLink} to={PATHS.manager.project(project.id)}>Mở sự kiện</Button>
        <Button component={RouterLink} to={PATHS.manager.projects}>Danh sách sự kiện</Button>
      </Stack>
    </Alert>
  );
}
