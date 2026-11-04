import { useState } from "react";
import { useDispatch } from "react-redux";
import { Box, Button, Card, CardContent, Stack, Typography } from "@mui/material";
import LockResetIcon from "@mui/icons-material/LockReset";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import ProfileDetails from "./components/ProfileDetails";
import ChangePasswordDialog from "./components/ChangePasswordDialog";
import { useAsync } from "../../../hooks/useAsync";
import { authService } from "../../../services/authService";
import { notify } from "../../../store/notifySlice";

// G-02 with the G-03 dialog.
export default function Profile() {
  const dispatch = useDispatch();
  const { data, loading, error, reload } = useAsync(() => authService.getProfile());
  const [dialogOpen, setDialogOpen] = useState(false);

  const saved = () => {
    setDialogOpen(false);
    dispatch(notify("Đổi mật khẩu thành công."));
  };

  return (
    <>
      <PageHeader code="G-02" title="Hồ sơ cá nhân" />
      <DataState loading={loading} error={error} onRetry={reload}>
        {data && (
          <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { md: "2fr 1fr" } }}>
            <ProfileDetails profile={data} />
            <Card>
              <CardContent>
                <Stack spacing={1}>
                  <Typography variant="h6">Bảo mật tài khoản</Typography>
                  <Typography color="text.secondary" variant="body2">Đổi mật khẩu sẽ đăng xuất các phiên khác của bạn.</Typography>
                  <Button variant="outlined" startIcon={<LockResetIcon />} onClick={() => setDialogOpen(true)}>Đổi mật khẩu</Button>
                </Stack>
              </CardContent>
            </Card>
          </Box>
        )}
      </DataState>
      <ChangePasswordDialog open={dialogOpen} onClose={() => setDialogOpen(false)} onSaved={saved} />
    </>
  );
}
