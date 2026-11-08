import { Link as RouterLink } from "react-router-dom";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Divider,
  Stack,
  Typography,
} from "@mui/material";
import ErrorOutlinedIcon from "@mui/icons-material/ErrorOutlined";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import { PATHS } from "../../../../routes/paths";
import { dateTimeVi, dateVi } from "../../../../utils/format";

// M-01 Alert Panel: Prominently displays Overdue Tasks (Red) and Stale BEOs (Orange)
export default function AlertPanel({ overdueTasks = [], staleBeos = [] }) {
  const totalAlerts = overdueTasks.length + staleBeos.length;

  return (
    <Card sx={{ height: "100%", border: totalAlerts > 0 ? "1px solid" : undefined, borderColor: overdueTasks.length > 0 ? "error.main" : "warning.main" }}>
      <CardContent>
        <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mb: 2 }}>
          <Stack direction="row" alignItems="center" spacing={1}>
            <ErrorOutlinedIcon color={overdueTasks.length > 0 ? "error" : "warning"} />
            <Typography variant="h6">Cần xử lý ngay</Typography>
          </Stack>
          {overdueTasks.length > 0 && (
            <Button
              size="small"
              color="error"
              component={RouterLink}
              to={PATHS.manager.overdueAlerts}
              endIcon={<ArrowForwardIcon />}
            >
              Xem tất cả ({overdueTasks.length})
            </Button>
          )}
        </Stack>

        {totalAlerts === 0 ? (
          <Typography color="text.secondary">Không có cảnh báo khẩn cấp nào.</Typography>
        ) : (
          <Stack spacing={2}>
            {/* ── Group A: Overdue Day-of Tasks (Red / Error) ───────────── */}
            {overdueTasks.length > 0 && (
              <Box>
                <Typography variant="subtitle2" color="error.main" sx={{ fontWeight: "bold", mb: 1, display: "flex", alignItems: "center", gap: 0.5 }}>
                  <ErrorOutlinedIcon fontSize="small" /> Nhiệm vụ quá hạn trong ngày ({overdueTasks.length})
                </Typography>
                <Stack spacing={1}>
                  {overdueTasks.slice(0, 5).map((t) => (
                    <Alert
                      key={t.taskId}
                      severity="error"
                      sx={{ alignItems: "center" }}
                      action={
                        <Button
                          color="error"
                          size="small"
                          variant="contained"
                          component={RouterLink}
                          to={`${PATHS.manager.dayOf(t.projectCode)}?taskId=${t.taskId}`}
                        >
                          Xử lý
                        </Button>
                      }
                    >
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>
                        [{t.projectCode}] {t.taskName}
                      </Typography>
                      <Typography variant="caption" display="block">
                        {t.hall} · Hạn: {dateTimeVi(t.deadline)} · Quá hạn: <strong>{t.overdueMinutes} phút</strong>
                      </Typography>
                    </Alert>
                  ))}
                </Stack>
              </Box>
            )}

            {overdueTasks.length > 0 && staleBeos.length > 0 && <Divider />}

            {/* ── Group B: Stale BEOs (Orange / Warning) ────────────────── */}
            {staleBeos.length > 0 && (
              <Box>
                <Typography variant="subtitle2" color="warning.dark" sx={{ fontWeight: "bold", mb: 1, display: "flex", alignItems: "center", gap: 0.5 }}>
                  <WarningAmberIcon fontSize="small" /> BEO lỗi thời cần duyệt lại ({staleBeos.length})
                </Typography>
                <Stack spacing={1}>
                  {staleBeos.slice(0, 5).map((b) => (
                    <Alert
                      key={b.beoId}
                      severity="warning"
                      sx={{ alignItems: "center" }}
                      action={
                        <Button
                          color="warning"
                          size="small"
                          variant="outlined"
                          component={RouterLink}
                          to={PATHS.manager.beo(b.projectCode)}
                        >
                          Mở BEO
                        </Button>
                      }
                    >
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>
                        [{b.projectCode}] BEO v{b.version} (Trạng thái: Stale)
                      </Typography>
                      <Typography variant="caption" display="block">
                        {b.hallName} · Ngày tiệc: {dateVi(b.eventDate)}
                        {b.approvedCrCode && ` · Phát sinh từ ${b.approvedCrCode}`}
                      </Typography>
                    </Alert>
                  ))}
                </Stack>
              </Box>
            )}
          </Stack>
        )}
      </CardContent>
    </Card>
  );
}
