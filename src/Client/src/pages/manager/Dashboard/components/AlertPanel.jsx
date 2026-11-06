import { Link as RouterLink } from "react-router-dom";
import { Alert, Button, Card, CardContent, Stack, Typography } from "@mui/material";
import { PATHS } from "../../../../routes/paths";

// Red panel: overdue day-of tasks (UC-32) and Stale BEOs that need re-approval (UC-29).
export default function AlertPanel({ overdue, stale }) {
  const empty = overdue.length === 0 && stale.length === 0;
  return (
    <Card sx={{ height: "100%" }} id="alertPanel">
      <CardContent>
        <Typography variant="h6" gutterBottom>Cần xử lý ngay</Typography>
        {empty ? <Typography color="text.secondary">Không có cảnh báo.</Typography> : (
          <Stack spacing={1}>
            {overdue.map((t) => (
              <Alert key={`o-${t.taskId}`} severity="error" action={<Button color="inherit" size="small" component={RouterLink} to={PATHS.manager.dayOf(t.projectCode)}>Mở</Button>}>
                {t.projectCode} · "{t.taskName}" quá hạn {t.overdueMinutes} phút
              </Alert>
            ))}
            {stale.map((b) => (
              <Alert key={`s-${b.projectCode}`} severity="error" action={<Button color="inherit" size="small" component={RouterLink} to={PATHS.manager.beo(b.projectCode)}>Mở BEO</Button>}>
                {b.projectCode} · BEO v{b.version} lỗi thời, cần duyệt lại
              </Alert>
            ))}
          </Stack>
        )}
      </CardContent>
    </Card>
  );
}
