import { Link as RouterLink } from "react-router-dom";
import { Alert, Button, Card, CardContent, Stack, Typography } from "@mui/material";
import { PATHS } from "../../../../routes/paths";

// Red panel of overdue day-of tasks; each line opens the proof photos of that project.
export default function OverdueAlertPanel({ alerts, total }) {
  return (
    <Card sx={{ height: "100%" }}>
      <CardContent>
        <Typography variant="h6" gutterBottom>Cảnh báo quá hạn</Typography>
        {alerts.length === 0 ? (
          <Typography color="text.secondary">Không có nhiệm vụ quá hạn.</Typography>
        ) : (
          <Stack spacing={1}>
            {alerts.map((a) => (
              <Alert key={a.taskId} severity="error" action={
                <Button color="inherit" size="small" component={RouterLink} to={`${PATHS.admin.photoFeed}?projectCode=${encodeURIComponent(a.projectCode)}`}>Xem ảnh</Button>
              }>
                {a.projectCode} · {a.taskName} quá hạn {a.overdueMinutes} phút
              </Alert>
            ))}
            {total > alerts.length && (
              <Button component={RouterLink} to={PATHS.admin.overdueAlerts}>Xem tất cả {total} cảnh báo</Button>
            )}
          </Stack>
        )}
      </CardContent>
    </Card>
  );
}
