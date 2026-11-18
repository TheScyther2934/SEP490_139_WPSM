import { Link as RouterLink } from "react-router-dom";
import {
  Card,
  CardContent,
  Chip,
  LinearProgress,
  List,
  ListItemButton,
  ListItemText,
  Stack,
  Typography,
} from "@mui/material";
import EventIcon from "@mui/icons-material/Event";
import { PATHS } from "../../../../routes/paths";
import { dateVi } from "../../../../utils/format";

export default function UpcomingEvents({ milestones = [] }) {
  return (
    <Card sx={{ height: "100%" }}>
      <CardContent>
        <Stack direction="row" alignItems="center" spacing={1} sx={{ mb: 1.5 }}>
          <EventIcon color="primary" fontSize="small" />
          <Typography variant="h6">Sự kiện &amp; Mốc chuẩn bị sắp tới</Typography>
        </Stack>

        {milestones.length === 0 ? (
          <Typography color="text.secondary">Không có sự kiện sắp tới.</Typography>
        ) : (
          <List dense disablePadding>
            {milestones.map((p) => {
              const progress =
                p.totalTasks > 0 ? Math.round((p.completedTasks * 100) / p.totalTasks) : 0;
              return (
                <ListItemButton
                  key={p.projectCode}
                  component={RouterLink}
                  to={PATHS.manager.project(p.projectCode)}
                  sx={{ borderRadius: 1.5, mb: 1, border: "1px solid", borderColor: "divider", flexDirection: "column", alignItems: "stretch" }}
                >
                  <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ width: "100%", mb: 0.5 }}>
                    <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                      {p.projectCode} · {p.coupleName}
                    </Typography>
                    <Chip
                      size="small"
                      label={p.daysRemaining === 0 ? "Hôm nay" : `Còn ${p.daysRemaining} ngày`}
                      color={p.daysRemaining <= 7 ? "warning" : "default"}
                      sx={{ fontWeight: "bold" }}
                    />
                  </Stack>
                  <Typography variant="caption" color="text.secondary" sx={{ mb: 1 }}>
                    {p.hallName} · Ngày cưới: {dateVi(p.eventDate)} · Giai đoạn hiện tại: <strong>{p.milestone}</strong> (Đến hạn {dateVi(p.dueDate)})
                  </Typography>
                  <Stack direction="row" spacing={1} alignItems="center">
                    <LinearProgress
                      variant="determinate"
                      value={progress}
                      sx={{ flexGrow: 1, height: 6, borderRadius: 3 }}
                    />
                    <Typography variant="caption" color="text.secondary">
                      {progress}% ({p.completedTasks}/{p.totalTasks})
                    </Typography>
                  </Stack>
                </ListItemButton>
              );
            })}
          </List>
        )}
      </CardContent>
    </Card>
  );
}
