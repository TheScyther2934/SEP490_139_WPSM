import { Link as RouterLink } from "react-router-dom";
import { Box, Button, Card, CardContent, Link, Stack, Typography } from "@mui/material";
import { keyframes } from "@mui/system";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlineOutlined";
import { PATHS } from "../../../../routes/paths";

const blink = keyframes`50% { opacity: 0.35; }`;

// SRS 10.1 alert strip: overdue day-of tasks in red with a link to A-15; each line also opens the project's proof photos.
export default function OverdueAlertPanel({ alerts, total }) {
  const hasAlerts = alerts.length > 0;
  return (
    <Card id="a01Overdue" sx={hasAlerts ? { bgcolor: "#fff5f5", borderColor: "#f3c4c4" } : undefined}>
      <CardContent>
        <Stack direction="row" sx={{ alignItems: "center", justifyContent: "space-between", mb: hasAlerts ? 1.5 : 0 }}>
          <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
            {hasAlerts && <Box sx={{ width: 10, height: 10, borderRadius: "50%", bgcolor: "error.main", animation: `${blink} 1.2s ease-in-out infinite` }} />}
            <Typography variant="h6">Cảnh báo quá hạn (trực tiếp)</Typography>
          </Stack>
          <Button size="small" component={RouterLink} to={PATHS.admin.overdueAlerts}>
            {total > alerts.length ? `Xem tất cả ${total} cảnh báo` : "Mở danh sách"}
          </Button>
        </Stack>
        {!hasAlerts ? (
          <Stack direction="row" spacing={1} sx={{ alignItems: "center", color: "success.main", mt: 1 }}>
            <CheckCircleOutlineIcon fontSize="small" />
            <Typography>Không có nhiệm vụ quá hạn.</Typography>
          </Stack>
        ) : (
          <Stack spacing={1}>
            {alerts.map((a) => (
              <Box key={a.taskId} sx={{ border: 1, borderColor: "error.main", bgcolor: "#fdecea", borderRadius: 1, px: 2, py: 1,
                display: "flex", flexWrap: "wrap", gap: 1, alignItems: "center", justifyContent: "space-between" }}>
                <Typography sx={{ color: "error.main", fontWeight: 600 }}>
                  {a.projectCode} · {a.taskName} quá hạn {a.overdueMinutes} phút
                </Typography>
                <Link component={RouterLink} to={`${PATHS.admin.photoFeed}?projectCode=${encodeURIComponent(a.projectCode)}`}
                  sx={{ color: "error.main", fontWeight: 600 }}>
                  Xem ảnh minh chứng →
                </Link>
              </Box>
            ))}
          </Stack>
        )}
      </CardContent>
    </Card>
  );
}
