import { useState } from "react";
import { useDispatch } from "react-redux";
import { Box, Button, Card, CardContent, Chip, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle, Divider, Stack, Typography } from "@mui/material";
import ComputerIcon from "@mui/icons-material/Computer";
import SmartphoneIcon from "@mui/icons-material/Smartphone";
import LogoutIcon from "@mui/icons-material/Logout";
import DataState from "../../../../components/DataState";
import { useAsync } from "../../../../hooks/useAsync";
import { authService } from "../../../../services/authService";
import { notify } from "../../../../store/notifySlice";
import { dateTimeVi, errorMessage, relativeTimeVi } from "../../../../utils/format";

const isPhone = (name = "") => /iPhone|iPad|Android/i.test(name);

// One account can be signed in on several devices; each one can be signed out from here.
export default function DeviceSessions() {
  const dispatch = useDispatch();
  const { data, loading, error, reload } = useAsync(() => authService.listSessions());
  const [confirm, setConfirm] = useState(null);
  const [busy, setBusy] = useState(false);
  const sessions = data ?? [];
  const others = sessions.filter((s) => !s.isCurrent);

  const run = async () => {
    setBusy(true);
    try {
      if (confirm.all) await authService.revokeOtherSessions();
      else await authService.revokeSession(confirm.session.id);
      dispatch(notify(confirm.all ? "Đã đăng xuất mọi thiết bị khác." : `Đã đăng xuất ${confirm.session.deviceName}.`));
      setConfirm(null);
      reload();
    } catch (e) {
      dispatch(notify({ message: errorMessage(e), severity: "error" }));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card>
      <CardContent>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={1} sx={{ justifyContent: "space-between", alignItems: { sm: "center" }, mb: 1 }}>
          <div>
            <Typography variant="h6">Thiết bị đang đăng nhập</Typography>
            <Typography variant="body2" color="text.secondary">
              Một tài khoản dùng được trên nhiều thiết bị. Thấy thiết bị lạ thì đăng xuất nó và đổi mật khẩu.
            </Typography>
          </div>
          {others.length > 0 && (
            <Button color="error" variant="outlined" startIcon={<LogoutIcon />} onClick={() => setConfirm({ all: true })} sx={{ flexShrink: 0 }}>
              Đăng xuất các thiết bị khác
            </Button>
          )}
        </Stack>
        <DataState loading={loading} error={error} onRetry={reload} empty={!sessions.length} emptyText="Không có phiên đăng nhập nào.">
          <Stack divider={<Divider flexItem />}>
            {sessions.map((s) => (
              <Stack key={s.id} direction="row" spacing={2} sx={{ py: 1.5, alignItems: "center" }}>
                <Box sx={{ color: s.isCurrent ? "primary.main" : "text.secondary", display: "flex" }}>
                  {isPhone(s.deviceName) ? <SmartphoneIcon /> : <ComputerIcon />}
                </Box>
                <Box sx={{ flex: 1, minWidth: 0 }}>
                  <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }}>
                    <Typography sx={{ fontWeight: 600 }}>{s.deviceName}</Typography>
                    {s.isCurrent && <Chip size="small" color="primary" label="Thiết bị này" />}
                  </Stack>
                  <Typography variant="body2" color="text.secondary">
                    {s.isCurrent ? "Đang dùng" : `Hoạt động ${relativeTimeVi(s.lastSeenAt).toLowerCase()}`}
                    {s.ipAddress ? ` · IP ${s.ipAddress}` : ""} · Đăng nhập lúc {dateTimeVi(s.createdAt)}
                  </Typography>
                </Box>
                {!s.isCurrent && (
                  <Button size="small" color="error" onClick={() => setConfirm({ session: s })}>Đăng xuất</Button>
                )}
              </Stack>
            ))}
          </Stack>
        </DataState>
      </CardContent>
      <Dialog open={!!confirm} onClose={busy ? undefined : () => setConfirm(null)} maxWidth="xs" fullWidth>
        <DialogTitle>{confirm?.all ? "Đăng xuất các thiết bị khác?" : "Đăng xuất thiết bị này?"}</DialogTitle>
        <DialogContent>
          <DialogContentText>
            {confirm?.all
              ? `${others.length} thiết bị khác sẽ phải đăng nhập lại. Thiết bị bạn đang dùng vẫn giữ đăng nhập.`
              : `${confirm?.session?.deviceName ?? ""} sẽ bị đăng xuất ngay và phải đăng nhập lại.`}
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirm(null)} disabled={busy}>Hủy</Button>
          <Button color="error" variant="contained" onClick={run} disabled={busy}>{busy ? "Đang đăng xuất..." : "Đăng xuất"}</Button>
        </DialogActions>
      </Dialog>
    </Card>
  );
}
